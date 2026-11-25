'use strict';
/*
 * 布局回归：专盯两个「在 ArkWeb 上翻过车」的地方。
 *
 *  1. 24 点 —— 卡片不许带圆角裁剪。ArkWeb 上「border-radius + overflow:hidden」的裁剪掩码会画错，
 *     表现是四张牌只画得出一张数字，还在屏幕上拉一条斜线。
 *  2. 中国象棋 —— 棋盘必须吃满宽度。以前 layout() 只在 resize 时跑，WebView 的盒子还没稳定就量了一次，
 *     之后没人再叫它，棋盘就永远停在「八成宽」。
 *
 * 用法：node Harmony/tools/test-layout.js
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

const CHROME = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
].find((p) => { try { return fs.existsSync(p); } catch (e) { return false; } });

const ROOT = path.join(__dirname, '..', 'entry/src/main/resources/rawfile/playland');
const W = 390, H = 844;
const PORT = 9336;

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

async function cdpConnect() {
  const t0 = Date.now();
  let wsUrl = null;
  while (Date.now() - t0 < 15000) {
    try {
      const list = await (await fetch('http://127.0.0.1:' + PORT + '/json/list')).json();
      const p = list.find((x) => x.type === 'page');
      if (p) { wsUrl = p.webSocketDebuggerUrl; break; }
    } catch (e) {}
    await sleep(200);
  }
  if (!wsUrl) throw new Error('CDP 连不上');
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(wsUrl);
    let id = 0;
    const pending = new Map();
    ws.onmessage = (ev) => {
      const m = JSON.parse(ev.data);
      if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
    };
    ws.onerror = () => reject(new Error('WS 出错'));
    ws.onopen = () => resolve({
      send(method, params) {
        return new Promise((res) => {
          const i = ++id;
          pending.set(i, res);
          ws.send(JSON.stringify({ id: i, method, params: params || {} }));
        });
      },
      close() { try { ws.close(); } catch (e) {} },
    });
  });
}

/** 打开某个游戏，跑一段探测脚本，取回 window.__P。 */
async function probe(key, js) {
  const game = path.join(ROOT, key, 'index.html');
  const tmp = path.join(path.dirname(game), '_layout.html');
  const src = fs.readFileSync(game, 'utf8');
  if (src.indexOf('</body>') < 0) throw new Error(key + ' 没有 </body>');
  fs.writeFileSync(tmp, src.replace('</body>',
    '<script>setTimeout(function(){' + js + '}, 800);</script></body>'), 'utf8');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'lay-'));
  const chrome = spawn(CHROME, [
    '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--hide-scrollbars', '--allow-file-access-from-files',
    '--remote-debugging-port=' + PORT, '--remote-allow-origins=*',
    '--window-size=' + W + ',' + H, '--user-data-dir=' + profile,
    'file:///' + tmp.replace(/\\/g, '/'),
  ], { stdio: 'ignore' });

  let payload = null;
  try {
    const c = await cdpConnect();
    await c.send('Emulation.setDeviceMetricsOverride', {
      width: W, height: H, deviceScaleFactor: 2, mobile: true,
    });
    await sleep(1600);
    const r = await c.send('Runtime.evaluate', {
      expression: 'JSON.stringify(window.__P || null)', returnByValue: true,
    });
    const v = r.result && r.result.result && r.result.result.value;
    if (v) payload = JSON.parse(v);
    c.close();
  } finally {
    try { spawn('taskkill', ['/PID', String(chrome.pid), '/T', '/F'], { stdio: 'ignore' }); } catch (e) {}
    try { chrome.kill(); } catch (e) {}
    try { fs.unlinkSync(tmp); } catch (e) {}
  }
  if (!payload) throw new Error(key + ' 页面没有回报结果');
  return payload;
}

const results = [];
function t(name, ok, got) {
  results.push({ name, ok: !!ok, got: got === undefined || got === null ? '' : String(got) });
}

async function checkGame24() {
  const p = await probe('game24', `
    (function () {
      var cards = document.querySelectorAll('#table .card');
      var nums = 0, clip = 0, overflow = 0;
      for (var i = 0; i < cards.length; i++) {
        var cs = getComputedStyle(cards[i]);
        if (cs.overflow !== 'visible') clip++;
        var n = cards[i].querySelector('.num');
        if (n && /[0-9]/.test(n.textContent)) nums++;
        var r = cards[i].getBoundingClientRect();
        if (r.right > innerWidth + 1 || r.bottom > innerHeight + 1 || r.left < -1) overflow++;
      }
      window.__P = { count: cards.length, nums: nums, clip: clip, overflow: overflow,
                     vw: innerWidth, vh: innerHeight };
    })();
  `);

  t('24点/四张牌', p.count === 4, p.count + ' 张');
  t('24点/都有数字', p.nums === 4, p.nums + ' 张有数字');
  t('24点/卡片不裁剪', p.clip === 0, p.clip + ' 张带 overflow 裁剪');
  t('24点/不出屏', p.overflow === 0, p.overflow + ' 张出界');
}

async function checkXiangqi() {
  const p = await probe('xiangqi', `
    (function () {
      function R(id) { var e = document.getElementById(id); if (!e) return null;
        var r = e.getBoundingClientRect();
        return { x: r.x, y: r.y, w: r.width, h: r.height, right: r.right, bottom: r.bottom }; }
      var body = document.getElementById('rulesBody');
      var rows = document.querySelectorAll('#rules .rRow');
      window.__P = {
        vw: innerWidth, vh: innerHeight,
        board: R('board'), area: R('boardArea'), rules: R('rules'),
        rows: rows.length,
        rowsVisible: rows.length ? rows[rows.length - 1].getBoundingClientRect().bottom <= (R('rules').bottom + 1) : false,
        bodyH: body ? body.clientHeight : 0,
        bodyScroll: body ? body.scrollHeight : 0,
        barTop: document.querySelector('.bar').getBoundingClientRect().top,
        pageOverflow: document.documentElement.scrollHeight - innerHeight
      };
    })();
  `);

  const ratio = p.board ? p.board.w / p.vw : 0;
  t('象棋/棋盘满宽', ratio >= 0.92, (ratio * 100).toFixed(1) + '% of ' + p.vw);
  t('象棋/棋盘不变形', Math.abs(p.board.w / p.board.h - 767 / 842) < 0.02,
    p.board.w.toFixed(0) + 'x' + p.board.h.toFixed(0));
  t('象棋/棋盘居中', Math.abs((p.board.x + p.board.w / 2) - p.area.x - p.area.w / 2) < 3,
    '中心 ' + (p.board.x + p.board.w / 2).toFixed(0));
  t('象棋/规则条目', p.rows === 8, p.rows + ' 条');
  t('象棋/规则能放下或可滚动', p.bodyH > 40 && (p.bodyScroll <= p.bodyH + 1 || p.bodyH > 60),
    '可视 ' + p.bodyH + ' / 内容 ' + p.bodyScroll);
  t('象棋/棋盘不压到底栏', p.board.bottom <= p.barTop + 1,
    p.board.bottom.toFixed(0) + ' <= ' + p.barTop.toFixed(0));
  t('象棋/页面不溢出', p.pageOverflow <= 2, p.pageOverflow + 'px');
}

async function main() {
  if (!CHROME) throw new Error('找不到 Chrome');

  await checkGame24();
  await checkXiangqi();

  console.log('\n布局回归 · 24 点 / 中国象棋\n');
  let pass = 0;
  results.forEach((r) => {
    console.log('  ' + (r.ok ? '✓' : '✗') + ' ' + r.name + (r.got ? '  ' + r.got : ''));
    if (r.ok) pass++;
  });
  const fails = results.filter((r) => !r.ok).length;
  console.log('\n  ' + pass + '/' + results.length + ' 通过\n');
  process.exit(fails ? 1 : 0);
}

main().catch((e) => { console.error(e.message); process.exit(1); });
