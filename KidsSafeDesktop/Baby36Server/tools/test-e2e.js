'use strict';
/*
 * 宝贝三十六计 · 家长端服务 -- 端到端协议测试
 *
 * 这个脚本同时扮演「孩子端」和「家长端」两个客户端走完整一轮流程，
 * 不依赖浏览器，纯 http，方便改完 server.js 立刻回归。
 *
 * 用法：
 *   node Baby36Server/server.js      # 另开一个窗口
 *   node Baby36Server/tools/test-e2e.js
 */

const http = require('http');

const BASE = process.env.BASE || 'http://127.0.0.1:8099';
const HOST = BASE.replace(/^https?:\/\//, '').split(':')[0];
const PORT = Number(BASE.split(':').pop());

let pass = 0;
let fail = 0;
const fails = [];

function ok(name, cond, extra) {
  if (cond) { pass++; console.log('  ✓ ' + name); }
  else { fail++; fails.push(name + (extra ? '  → ' + extra : '')); console.log('  ✗ ' + name + (extra ? '  → ' + extra : '')); }
}

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

function req(method, path, body) {
  return new Promise((resolve, reject) => {
    const data = body == null ? null : JSON.stringify(body);
    const r = http.request({
      host: HOST, port: PORT, path, method,
      headers: data ? { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(data) } : {},
    }, (res) => {
      let buf = '';
      res.on('data', (c) => { buf += c; });
      res.on('end', () => {
        let j = null;
        try { j = JSON.parse(buf); } catch (e) { j = null; }
        resolve({ status: res.statusCode, body: j, raw: buf });
      });
    });
    r.on('error', reject);
    if (data) r.write(data);
    r.end();
  });
}

/**
 * 订阅 SSE。返回 { msgs, wait(type, ms), close() }。
 * wait 会从已收到的消息里找，找不到就等新消息 —— 顺序不对会超时，正是我们要的。
 */
function sse(role) {
  const msgs = [];
  const waiters = [];
  let buffer = '';
  const r = http.get({
    host: HOST, port: PORT,
    path: '/api/rooms/' + sse.code + '/events?role=' + role,
    headers: { Accept: 'text/event-stream' },
  }, (res) => {
    res.setEncoding('utf8');
    res.on('data', (chunk) => {
      buffer += chunk;
      let i;
      while ((i = buffer.indexOf('\n\n')) >= 0) {
        const block = buffer.slice(0, i);
        buffer = buffer.slice(i + 2);
        const line = block.split('\n').find((l) => l.startsWith('data: '));
        if (!line) continue;
        let m;
        try { m = JSON.parse(line.slice(6)); } catch (e) { continue; }
        msgs.push(m);
        for (let k = 0; k < waiters.length; k++) {
          if (waiters[k].type === m.type) {
            const w = waiters.splice(k, 1)[0];
            clearTimeout(w.timer);
            // 这条消息已经交出去了，标记一下，免得下一次 wait 又把它捞回来
            m.__used = true;
            w.resolve(m);
            break;
          }
        }
      }
    });
  });
  r.on('error', () => {});

  return {
    msgs,
    wait(type, ms) {
      const hit = msgs.find((m) => m.type === type && !m.__used);
      if (hit) { hit.__used = true; return Promise.resolve(hit); }
      return new Promise((resolve) => {
        const w = { type, resolve };
        w.timer = setTimeout(() => {
          const k = waiters.indexOf(w);
          if (k >= 0) waiters.splice(k, 1);
          resolve(null);
        }, ms || 2500);
        waiters.push(w);
      });
    },
    close() { try { r.destroy(); } catch (e) {} },
  };
}

async function main() {
  console.log('\n宝贝三十六计 · 家长端服务 端到端测试');
  console.log('  目标 ' + BASE + '\n');

  // ── 1. 建房 ──
  console.log('[1] 建房');
  const c = await req('POST', '/api/rooms');
  ok('POST /api/rooms 返回 200', c.status === 200, 'status=' + c.status);
  const code = c.body && c.body.code;
  ok('房号是 4 位数字', /^\d{4}$/.test(String(code)), 'code=' + code);
  ok('返回了局域网地址', Array.isArray(c.body.ips), 'ips=' + JSON.stringify(c.body && c.body.ips));

  sse.code = code;

  // ── 2. 孩子端先连 ──
  console.log('\n[2] 孩子端连上');
  const kid = sse('kid');
  await sleep(250);
  const st0 = await req('GET', '/api/rooms/' + code);
  ok('房间状态里 kid=1', st0.body && st0.body.presence.kid === 1, JSON.stringify(st0.body && st0.body.presence));

  // ── 3. 家长端连上 ──
  console.log('\n[3] 家长端连上');
  const par = sse('parent');
  const pSync = await par.wait('sync');
  ok('家长端收到 sync（刷新可复原）', !!pSync);
  const pPresence = await par.wait('presence');
  ok('家长端收到 presence 且 kid=1', !!pPresence && pPresence.presence.kid === 1,
    JSON.stringify(pPresence && pPresence.presence));

  const kJ = await kid.wait('joined');
  ok('孩子端收到 joined（家里来人了）', !!kJ && kJ.role === 'parent', JSON.stringify(kJ));

  // ── 4. hello ──
  console.log('\n[4] 家长点「开始」');
  await req('POST', '/api/rooms/' + code + '/act', { role: 'parent', type: 'hello', who: '陌生人' });
  const kH = await kid.wait('hello');
  ok('孩子端收到 hello（切进场景）', !!kH);

  // ── 5. say ──
  console.log('\n[5] 家长出题');
  const LINE = '小朋友，我有一个礼物给你，快和我到前面车里去看看吧。';
  const s1 = await req('POST', '/api/rooms/' + code + '/act',
    { role: 'parent', type: 'say', who: '陌生人', text: LINE });
  ok('say 返回 id=1', s1.body && s1.body.id === 1, JSON.stringify(s1.body));
  const kS1 = await kid.wait('say');
  ok('孩子端收到 say 且台词一字不差', !!kS1 && kS1.text === LINE, kS1 && kS1.text);
  ok('say 带上了序号', !!kS1 && kS1.id === 1, kS1 && String(kS1.id));

  const emptySay = await req('POST', '/api/rooms/' + code + '/act', { role: 'parent', type: 'say', text: '   ' });
  ok('空台词被拒绝（400）', emptySay.status === 400, 'status=' + emptySay.status);

  // ── 6. 孩子答错（勾 = 跟走）──
  console.log('\n[6] 孩子点了对勾（上当了）');
  await req('POST', '/api/rooms/' + code + '/act', { role: 'kid', type: 'answer', ok: false, text: LINE });
  const a1 = await par.wait('answer');
  ok('家长端收到 answer', !!a1);
  ok('answer.ok=false（上当了）', !!a1 && a1.ok === false, JSON.stringify(a1 && a1.ok));
  ok('记分 bad=1', !!a1 && a1.score.bad === 1, JSON.stringify(a1 && a1.score));
  ok('answer 关联到第 1 句', !!a1 && a1.id === 1, a1 && String(a1.id));

  // ── 7. 第二句，答对 ──
  console.log('\n[7] 第二题，孩子点叉（识破了）');
  const LINE2 = '我是你妈妈的同事，她让我来接你。';
  const s2 = await req('POST', '/api/rooms/' + code + '/act',
    { role: 'parent', type: 'say', who: '陌生人', text: LINE2 });
  ok('第二句 id=2', s2.body && s2.body.id === 2, JSON.stringify(s2.body));
  const kS2 = await kid.wait('say');
  ok('孩子端收到第二句', !!kS2 && kS2.text === LINE2, kS2 && kS2.text);

  await req('POST', '/api/rooms/' + code + '/act', { role: 'kid', type: 'answer', ok: true, text: LINE2 });
  const a2 = await par.wait('answer');
  ok('answer.ok=true（识破了）', !!a2 && a2.ok === true, JSON.stringify(a2 && a2.ok));
  ok('记分 ok=1 / bad=1', !!a2 && a2.score.ok === 1 && a2.score.bad === 1, JSON.stringify(a2 && a2.score));
  ok('answer 关联到第 2 句', !!a2 && a2.id === 2, a2 && String(a2.id));

  // ── 8. 结束 ──
  console.log('\n[8] 家长结束这一局');
  await req('POST', '/api/rooms/' + code + '/act',
    { role: 'parent', type: 'end', win: true, text: '他一次都没跟你走，真棒！' });
  const kE = await kid.wait('end');
  ok('孩子端收到 end', !!kE);
  ok('end.win=true', !!kE && kE.win === true, JSON.stringify(kE && kE.win));
  ok('end 带了结束语', !!kE && !!kE.text, kE && kE.text);

  // ── 9. 刷新后 sync 还原 ──
  console.log('\n[9] 家长刷新页面（新的 SSE 连接）');
  par.close();
  await sleep(200);
  const par2 = sse('parent');
  const sync2 = await par2.wait('sync');
  ok('sync 里有历史流水', !!sync2 && Array.isArray(sync2.log) && sync2.log.length >= 5,
    'log=' + (sync2 && sync2.log && sync2.log.length));
  const kinds = (sync2 && sync2.log || []).map((e) => e.type).join(',');
  ok('流水顺序正确 hello→say→answer→say→answer→end',
    kinds === 'hello,say,answer,say,answer,end', kinds);
  ok('sync 带上了分数', !!sync2 && sync2.score.ok === 1 && sync2.score.bad === 1,
    JSON.stringify(sync2 && sync2.score));

  // ── 10. 断线回落 ──
  console.log('\n[10] 孩子端断开');
  kid.close();
  await sleep(350);
  const st1 = await req('GET', '/api/rooms/' + code);
  ok('断线后 kid=0', st1.body && st1.body.presence.kid === 0, JSON.stringify(st1.body && st1.body.presence));

  // ── 11. 异常路径 ──
  console.log('\n[11] 异常路径');
  const badRoom = await req('POST', '/api/rooms/0000/act', { role: 'parent', type: 'say', text: 'x' });
  ok('不存在的房间返回 404', badRoom.status === 404, 'status=' + badRoom.status);
  const badType = await req('POST', '/api/rooms/' + code + '/act', { role: 'parent', type: 'nope' });
  ok('未知动作返回 400', badType.status === 400, 'status=' + badType.status);

  const html = await req('GET', '/');
  ok('家长端首页可访问', html.status === 200 && /宝贝三十六计/.test(html.raw || ''),
    'status=' + html.status);
  const data = await req('GET', '/data.js');
  ok('台词库可访问', data.status === 200 && /BB36_JI/.test(data.raw || ''), 'status=' + data.status);

  // ── 12. 家长先开始、孩子后连上 ──
  // 这是真实操作里最容易出问题的一步：家长手快先点了「开始」，
  // 孩子那台平板还在填房号。孩子一连上就必须补发 hello，否则他一直停在输入页。
  console.log('\n[12] 家长先开始，孩子后连上');
  const late = await req('POST', '/api/rooms');
  const lateCode = late.body.code;
  await req('POST', '/api/rooms/' + lateCode + '/act', { role: 'parent', type: 'hello', who: '陌生人' });
  sse.code = lateCode;
  const kidLate = sse('kid');
  const hLate = await kidLate.wait('hello', 2500);
  ok('孩子后连上也能收到补发的 hello', !!hLate, JSON.stringify(hLate));

  await req('POST', '/api/rooms/' + lateCode + '/act',
    { role: 'parent', type: 'say', who: '陌生人', text: '补发后的一句' });
  const sLate = await kidLate.wait('say', 2500);
  ok('补发之后台词正常到达', !!sLate && sLate.text === '补发后的一句', sLate && sLate.text);
  kidLate.close();

  // 结束过的一局不该把人再拽回场景
  await req('POST', '/api/rooms/' + lateCode + '/act', { role: 'parent', type: 'end', win: true, text: '完' });
  const kidLate2 = sse('kid');
  const hAgain = await kidLate2.wait('hello', 900);
  ok('结束之后再连不补 hello（不会又被拽进场景）', !hAgain, JSON.stringify(hAgain));
  kidLate2.close();
  await req('DELETE', '/api/rooms/' + lateCode);

  // ── 13. 多房间并发 ──
  console.log('\n[13] 多房间并发');
  const r2 = await req('POST', '/api/rooms');
  ok('第二个房间号不同', r2.body.code !== code, r2.body.code + ' vs ' + code);
  await req('POST', '/api/rooms/' + r2.body.code + '/act', { role: 'parent', type: 'say', text: '干扰句' });
  // kid 已经断开，这里必然收不到；真正要验证的是房间 1 的 seq 没被房间 2 影响
  await kid.wait('say', 400);
  const s3 = await req('POST', '/api/rooms/' + code + '/act', { role: 'parent', type: 'say', text: '第三句' });
  ok('房间 1 的序号独立递增（=3）', s3.body && s3.body.id === 3, JSON.stringify(s3.body));

  par2.close();
  await req('DELETE', '/api/rooms/' + code);
  await req('DELETE', '/api/rooms/' + r2.body.code);

  // ── 汇总 ──
  console.log('\n' + '─'.repeat(46));
  console.log('  通过 ' + pass + ' 项，失败 ' + fail + ' 项');
  if (fail) {
    console.log('\n  失败清单：');
    fails.forEach((f) => console.log('   · ' + f));
  }
  console.log('─'.repeat(46) + '\n');
  process.exit(fail ? 1 : 0);
}

main().catch((e) => {
  console.error('\n测试脚本自己崩了：', e);
  process.exit(2);
});
