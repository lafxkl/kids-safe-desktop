'use strict';
/*
 * 宝贝三十六计 · 家长端服务
 * ─────────────────────────────────────────────────────────────
 * 只依赖 Node 内置模块，不需要 npm install，也不需要数据库。
 *
 * 为什么不用原工程的 Server/*.js：
 *   原工程用的是 nodejs-websocket + MySQL，端口 56。这套东西在鸿蒙 ArkWeb
 *   里跑不了（ArkWeb 没有原生 WebSocket 客户端封装到那个库），而且装 MySQL
 *   对「家长在自己手机上陪孩子玩五分钟」来说代价太高。
 *   所以这里换成 SSE（EventSource）+ fetch POST：
 *     - 下行（家长出题 → 孩子）：SSE 长连接，孩子端已经写好了 EventSource
 *     - 上行（孩子作答 → 家长）：普通 POST
 *   全部状态放在内存里，进程重启即清空——房间里本来就只有两个人在线几分钟。
 *
 * 接口：
 *   POST   /api/rooms                      建房间            → {code}
 *   GET    /api/rooms/:code                房间状态
 *   GET    /api/rooms/:code/events?role=   订阅（SSE）
 *   POST   /api/rooms/:code/act            动作（hello/say/answer/end/reset）
 *   DELETE /api/rooms/:code                销毁房间
 *   GET    /                               家长端网页
 */

const http = require('http');
const fs = require('fs');
const path = require('path');
const os = require('os');

const PORT = Number(process.env.PORT || 8099);
const PUBLIC = path.join(__dirname, 'public');
// 房间闲置多久回收：3 小时。家长陪玩撑死半小时，够用了。
const ROOM_TTL = 3 * 3600 * 1000;
// 心跳间隔：一是防中间设备掐长连接，二是能及时探出断线
const HB_MS = 20000;

/** code -> room */
const rooms = new Map();

/* ─────────────────────────── 房间 ─────────────────────────── */

function makeCode() {
  for (let i = 0; i < 500; i++) {
    const c = String(1000 + Math.floor(Math.random() * 9000));
    if (!rooms.has(c)) return c;
  }
  // 9000 个房号全占满是不可能的，真到了这一步就踢掉最久没动静的那个
  const oldest = [...rooms.values()].sort((a, b) => a.touched - b.touched)[0];
  if (oldest) rooms.delete(oldest.code);
  return String(1000 + Math.floor(Math.random() * 9000));
}

function createRoom() {
  const room = {
    code: makeCode(),
    created: Date.now(),
    touched: Date.now(),
    kid: new Set(),      // kid 端的 SSE res
    parent: new Set(),   // 家长端的 SSE res
    seq: 0,              // 出题序号，孩子端用 'link-<id>' 认领这一句
    log: [],             // 全程流水，家长端刷新后靠它复原界面
    score: { ok: 0, bad: 0 },
    last: null,          // 最近一句台词
  };
  rooms.set(room.code, room);
  return room;
}

function getRoom(code) {
  const room = rooms.get(String(code || ''));
  if (room) room.touched = Date.now();
  return room;
}

function presence(room) {
  return { kid: room.kid.size, parent: room.parent.size };
}

/* ─────────────────────────── SSE ─────────────────────────── */

function sseHead(res) {
  res.writeHead(200, {
    'Content-Type': 'text/event-stream; charset=utf-8',
    'Cache-Control': 'no-cache, no-transform',
    Connection: 'keep-alive',
    'X-Accel-Buffering': 'no',
    'Access-Control-Allow-Origin': '*',
  });
  // retry 交给浏览器自己重连，孩子端 onerror 里也有提示，双保险
  res.write('retry: 2000\n\n');
}

function push(res, obj) {
  try {
    res.write('data: ' + JSON.stringify(obj) + '\n\n');
    return true;
  } catch (e) {
    return false;
  }
}

function setOf(room, role) {
  return role === 'kid' ? room.kid : room.parent;
}

/** 推给房间里的某一端 */
function cast(room, role, obj) {
  const set = setOf(room, role);
  for (const res of [...set]) {
    if (!push(res, obj)) set.delete(res);
  }
}

/* ─────────────────────────── HTTP 小工具 ─────────────────────────── */

function cors(res) {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET,POST,DELETE,OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
  res.setHeader('Access-Control-Max-Age', '86400');
}

function json(res, obj, code) {
  const body = JSON.stringify(obj);
  cors(res);
  res.writeHead(code || 200, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(body),
  });
  res.end(body);
}

function readJson(req) {
  return new Promise((resolve) => {
    let buf = '';
    let done = false;
    const finish = (v) => { if (!done) { done = true; resolve(v); } };
    req.on('data', (c) => {
      buf += c;
      if (buf.length > 64 * 1024) { finish({}); req.destroy(); }
    });
    req.on('end', () => {
      try { finish(JSON.parse(buf || '{}')); } catch (e) { finish({}); }
    });
    req.on('error', () => finish({}));
  });
}

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
};

function serveStatic(res, rel) {
  // 防目录穿越：拼完必须还在 PUBLIC 里面
  const full = path.join(PUBLIC, rel);
  if (!full.startsWith(PUBLIC)) return notFound(res);
  fs.readFile(full, (err, buf) => {
    if (err) return notFound(res);
    res.writeHead(200, {
      'Content-Type': MIME[path.extname(full).toLowerCase()] || 'application/octet-stream',
      'Cache-Control': 'no-cache',
    });
    res.end(buf);
  });
}

function notFound(res) {
  res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
  res.end('404');
}

/* ─────────────────────────── 动作 ─────────────────────────── */

function clip(s, n) {
  return String(s == null ? '' : s).slice(0, n);
}

async function handleAct(req, res, room) {
  const body = await readJson(req);
  const role = body.role === 'parent' ? 'parent' : 'kid';
  const type = String(body.type || '');
  room.touched = Date.now();

  if (type === 'hello') {
    // 家长按下「开始陪练」：孩子端收到 hello 就切进场景页
    const who = clip(body.who || '家长', 12);
    room.seq = 0;
    room.last = null;
    room.score = { ok: 0, bad: 0 };
    room.log.push({ t: Date.now(), role, type: 'hello', who });
    cast(room, 'kid', { type: 'hello', who });
    cast(room, 'parent', { type: 'hello', who });
    return json(res, { ok: true });
  }

  if (type === 'say') {
    const text = clip(body.text, 200).trim();
    if (!text) return json(res, { ok: false, err: '台词不能为空' }, 400);
    room.seq += 1;
    const who = clip(body.who || '家长', 12);
    const item = { id: room.seq, who, text, t: Date.now() };
    room.last = item;
    room.log.push({ t: item.t, role, type: 'say', id: item.id, who, text });
    cast(room, 'kid', { type: 'say', id: item.id, who, text });
    cast(room, 'parent', { type: 'said', id: item.id, who, text });
    return json(res, { ok: true, id: item.id });
  }

  if (type === 'answer') {
    // 孩子端送上来的是 ok: !agree —— 按了叉（不跟他走）就是 ok=true，即「识破」
    const ok = !!body.ok;
    if (ok) room.score.ok += 1;
    else room.score.bad += 1;
    const item = {
      t: Date.now(),
      role: 'kid',
      type: 'answer',
      ok,
      id: room.last ? room.last.id : 0,
      text: clip(body.text || (room.last && room.last.text) || '', 200),
    };
    room.log.push(item);
    cast(room, 'parent', {
      type: 'answer',
      ok,
      id: item.id,
      text: item.text,
      score: room.score,
    });
    return json(res, { ok: true });
  }

  if (type === 'end') {
    const win = !!body.win;
    const text = clip(body.text, 120);
    room.log.push({ t: Date.now(), role, type: 'end', win, text });
    cast(room, 'kid', { type: 'end', win, text });
    cast(room, 'parent', { type: 'end', win, text });
    return json(res, { ok: true });
  }

  if (type === 'reset') {
    room.seq = 0;
    room.last = null;
    room.log = [];
    room.score = { ok: 0, bad: 0 };
    cast(room, 'parent', { type: 'reset' });
    cast(room, 'kid', { type: 'reset' });
    return json(res, { ok: true });
  }

  return json(res, { ok: false, err: '未知动作：' + type }, 400);
}

/* ─────────────────────────── 路由 ─────────────────────────── */

function lanIps() {
  const out = [];
  const nets = os.networkInterfaces();
  for (const name of Object.keys(nets)) {
    for (const ni of nets[name] || []) {
      if (ni.family === 'IPv4' && !ni.internal) out.push(ni.address);
    }
  }
  return out;
}

const server = http.createServer((req, res) => {
  let u;
  try {
    u = new URL(req.url, 'http://localhost');
  } catch (e) {
    return notFound(res);
  }
  const p = u.pathname;

  if (req.method === 'OPTIONS') {
    cors(res);
    res.writeHead(204);
    return res.end();
  }

  // ── 服务信息（家长端要靠它显示「孩子在平板上该填什么」）──
  if (p === '/api/info' && req.method === 'GET') {
    const ips = lanIps();
    // 虚拟网卡（VMware / VirtualBox / WSL / Hyper-V）的地址家长根本用不上，
    // 排到后面去，把真实网卡顶到第一位。
    const virtual = /^(172\.(1[6-9]|2\d|3[01])\.|10\.0\.2\.|169\.254\.)/;
    ips.sort((a, b) => (virtual.test(a) ? 1 : 0) - (virtual.test(b) ? 1 : 0));
    return json(res, { ok: true, ips, port: PORT });
  }

  // ── 建房间 ──
  if (p === '/api/rooms' && req.method === 'POST') {
    const room = createRoom();
    console.log('[房间] 新建 ' + room.code + '，当前共 ' + rooms.size + ' 个');
    return json(res, {
      ok: true,
      code: room.code,
      ips: lanIps(),
      port: PORT,
      origin: 'http://' + (lanIps()[0] || '127.0.0.1') + ':' + PORT,
    });
  }

  // ── 房间列表（调试用）──
  if (p === '/api/rooms' && req.method === 'GET') {
    return json(res, {
      ok: true,
      rooms: [...rooms.values()].map((r) => ({
        code: r.code,
        presence: presence(r),
        score: r.score,
        age: Math.round((Date.now() - r.created) / 1000),
      })),
    });
  }

  const m = p.match(/^\/api\/rooms\/(\d{4})(\/events|\/act)?$/);
  if (m) {
    const code = m[1];
    const tail = m[2] || '';

    if (tail === '/events' && req.method === 'GET') {
      const role = u.searchParams.get('role') === 'parent' ? 'parent' : 'kid';
      const room = getRoom(code);
      if (!room) return notFound(res);

      sseHead(res);
      setOf(room, role).add(res);

      // 先给家长端补一份现场状态，这样刷新页面不会丢上下文
      if (role === 'parent') {
        push(res, {
          type: 'sync',
          code: room.code,
          presence: presence(room),
          score: room.score,
          log: room.log,
        });
      }
      // 家长先点「开始」、孩子后连上（或者孩子断线重连）时，补一个 hello，
      // 否则孩子的平板会一直停在输入房号那一页，家长那边怎么念台词都没反应。
      // 但已经 end 过的那一局不补 —— 那局已经结束了，别把人又拽回场景里。
      if (role === 'kid') {
        const lastIdx = (t) => {
          for (let i = room.log.length - 1; i >= 0; i--) if (room.log[i].type === t) return i;
          return -1;
        };
        if (lastIdx('hello') > lastIdx('end')) {
          push(res, { type: 'hello', who: '陌生人' });
        }
      }
      // 告诉对面「有人进来了」
      cast(room, role === 'kid' ? 'parent' : 'kid', {
        type: 'joined',
        role,
        presence: presence(room),
      });
      cast(room, role, { type: 'presence', presence: presence(room) });

      const hb = setInterval(() => {
        try {
          res.write(': hb\n\n');
        } catch (e) {
          clearInterval(hb);
        }
      }, HB_MS);

      const bye = () => {
        clearInterval(hb);
        setOf(room, role).delete(res);
        cast(room, role === 'kid' ? 'parent' : 'kid', {
          type: 'left',
          role,
          presence: presence(room),
        });
        cast(room, role, { type: 'presence', presence: presence(room) });
      };
      req.on('close', bye);
      res.on('close', bye);
      res.on('error', bye);
      return;
    }

    if (tail === '/act' && req.method === 'POST') {
      const room = getRoom(code);
      if (!room) return json(res, { ok: false, err: '房间不存在或已过期' }, 404);
      return handleAct(req, res, room).catch((e) => {
        console.error('[动作] 出错', e);
        json(res, { ok: false, err: '服务端出错' }, 500);
      });
    }

    if (req.method === 'DELETE') {
      rooms.delete(code);
      return json(res, { ok: true });
    }

    if (req.method === 'GET') {
      const room = getRoom(code);
      if (!room) return json(res, { ok: false, err: '房间不存在' }, 404);
      return json(res, {
        ok: true,
        code: room.code,
        presence: presence(room),
        score: room.score,
        last: room.last,
      });
    }
  }

  // ── 静态 ──
  if (p === '/' || p === '/index.html') return serveStatic(res, 'index.html');
  if (req.method === 'GET') return serveStatic(res, decodeURIComponent(p).replace(/^\/+/, ''));

  notFound(res);
});

/* ─────────────────────────── 回收 & 启动 ─────────────────────────── */

setInterval(() => {
  const now = Date.now();
  for (const [code, room] of rooms) {
    const empty = room.kid.size === 0 && room.parent.size === 0;
    if (now - room.touched > ROOM_TTL && empty) {
      rooms.delete(code);
      console.log('[房间] 回收 ' + code);
    }
  }
}, 60 * 1000).unref();

server.listen(PORT, '0.0.0.0', () => {
  const ips = lanIps();
  console.log('');
  console.log('  宝贝三十六计 · 家长端服务已启动');
  console.log('  ─────────────────────────────────────');
  console.log('  家长在本机打开：  http://127.0.0.1:' + PORT);
  ips.forEach((ip) => {
    console.log('  孩子在平板上填：  http://' + ip + ':' + PORT);
  });
  if (!ips.length) console.log('  （没找到局域网地址，检查一下 Wi-Fi）');
  console.log('  ─────────────────────────────────────');
  console.log('  按 Ctrl+C 停止');
  console.log('');
});

process.on('SIGINT', () => {
  console.log('\n再见。');
  process.exit(0);
});
