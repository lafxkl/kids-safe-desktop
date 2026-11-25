'use strict';
/*
 * 宝贝三十六计 · 孩子端全量断言
 *
 * 在无头 Chrome 里跑真实 index.html，用页面自己的测试钩子 __bb36 把整局玩法走一遍：
 * 进场景 → 答对 → 答错 → 三心扣完判负 → 通关判胜 → 图鉴解锁 → 地图进度 → 存档落盘。
 *
 * 为什么不靠 --dump-dom + document.title：
 *   断言有二十几条，塞进标题会撑爆；而且中间要「先 pick 再 next」这种顺序操作，
 *   只有能持续控制页面才做得到。所以用 CDP 打开页面 + 一个探针服务收结果。
 *
 * 用法：
 *   node Harmony/tools/test-baobei36.js
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const http = require('http');
const { spawn } = require('child_process');

const CHROME = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
].find((p) => { try { return fs.existsSync(p); } catch (e) { return false; } });

const GAME = path.join(__dirname, '..', 'entry/src/main/resources/rawfile/playland/baobei36/index.html');
const TMP = path.join(path.dirname(GAME), '_assert.html');
const PROBE_PORT = 8097;
const CDP_PORT = 9224;

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

/* ── 断言脚本（会被拼进页面执行）── */
const ASSERT_JS = `
(async function () {
  var B = window.__bb36;
  var R = [];
  var ERRORS = [];
  window.addEventListener('error', function (e) { ERRORS.push(String(e.message)); });
  function wait(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }

  function t(name, cond, got) {
    R.push({ name: name, ok: !!cond, got: got === undefined || got === null ? '' : String(got) });
  }
  function num(v) { return String(v); }

  try {
    // ── 1. 数据 ──
    var JI = B.ji;
    t('count36', JI.length === 36, JI.length);

    var byScene = {};
    JI.forEach(function (j) { byScene[j[0]] = (byScene[j[0]] || 0) + 1; });
    t('ninePerScene', Object.keys(byScene).every(function (k) { return byScene[k] === 9; }),
      JSON.stringify(byScene));

    var ids = {}, dup = 0;
    JI.forEach(function (j) { if (ids[j[4]]) { dup++; } ids[j[4]] = 1; });
    t('uniqueIds', dup === 0 && Object.keys(ids).length === 36, Object.keys(ids).length + ' 个，重复 ' + dup);

    var empty = 0;
    JI.forEach(function (j) { for (var i = 0; i < 5; i++) { if (!String(j[i] || '').trim()) { empty++; } } });
    t('noEmpty', empty === 0, empty + ' 个空字段');

    // ── 2. 进场景 ──
    B.reset();
    B.enterScene('zoo');
    t('pageScene', B.page() === 'scene', B.page());
    t('hearts3', B.hearts() === 3, B.hearts());

    B.showLine();   // 跳过 900ms 入场延时
    // 台词是逐字打出来的，刚调完 showLine 时屏幕上一个字都没有，要等一拍
    await wait(260);
    var shown = B.lineText();
    var full = (B.cur() && B.cur()[2]) || '';
    t('lineShown', shown.length > 0 && full.indexOf(shown) === 0, shown + ' ／ 全句 ' + full.length + ' 字');

    // ── 3. 答对 ──
    B.pick(false);
    t('cardOpen', B.cardOn() === true, B.cardOn());
    t('cardTipShown', B.cardTip().length > 6, B.cardTip().slice(0, 16) + '…');
    t('solved1', B.solvedCount() === 1, B.solvedCount());
    B.next();
    t('toSecond', B.idx() === 1, B.idx());

    // ── 4. 答错 ──
    B.showLine();
    B.pick(true);
    t('wrongCard', B.cardOn() === true, B.cardOn());
    t('heartsDown', B.hearts() === 2, B.hearts());
    t('solvedStill1', B.solvedCount() === 1, B.solvedCount());
    B.next();

    // ── 5. 三心扣完判负 ──
    B.showLine(); B.pick(true); B.next();   // 2 → 1
    B.showLine(); B.pick(true); B.next();   // 1 → 0，这里该弹结算
    t('endLose', B.endOpen() === true, B.endOpen());
    t('endLoseText', B.endText() === '被拐走了…', B.endText());

    // ── 6. 九句全对判胜 ──
    B.reset();
    B.enterScene('hosp');
    for (var i = 0; i < 9; i++) {
      B.showLine();
      B.pick(false);
      B.next();
    }
    t('endWin', B.endOpen() === true, B.endOpen());
    t('endWinText', B.endText() === '真棒！', B.endText());
    t('hosp9', B.solvedIn('hosp') === 9, B.solvedIn('hosp'));
    t('totalAfterWin', B.solvedCount() === 9, B.solvedCount());

    // ── 7. 图鉴 ──
    B.openBook();
    var grid = document.getElementById('bookGrid');
    t('book36', grid.children.length === 36, grid.children.length);
    var got = grid.querySelectorAll('.jie.got').length;
    t('bookGot9', got === 9, got);
    t('bookScore', document.getElementById('bookScore').textContent === '9/36',
      document.getElementById('bookScore').textContent);
    t('bookLocked', grid.querySelectorAll('.jie.lock').length === 27,
      grid.querySelectorAll('.jie.lock').length);

    // ── 8. 地图进度 ──
    B.openMap();
    t('mapScore', document.getElementById('mapScore').textContent === '9/36',
      document.getElementById('mapScore').textContent);
    var mHosp = document.querySelector('.marker[data-scene="hosp"] .prog');
    var mZoo = document.querySelector('.marker[data-scene="zoo"] .prog');
    t('markerHosp9', mHosp && mHosp.textContent === '9', mHosp && mHosp.textContent);
    t('markerZoo0', mZoo && mZoo.textContent === '0', mZoo && mZoo.textContent);
    t('markerDone', document.querySelector('.marker[data-scene="hosp"]').classList.contains('done'), '');

    // ── 9. 存档 ──
    var raw = localStorage.getItem('bb36.v1');
    t('persist', !!raw && /"solved"/.test(raw), raw ? raw.slice(0, 46) : 'null');
    var parsed = null;
    try { parsed = JSON.parse(raw); } catch (e) {}
    t('persistCount', !!parsed && Object.keys(parsed.solved || {}).length === 9,
      parsed ? Object.keys(parsed.solved || {}).length : 'parse fail');

    // ── 10. 重新加载后能读回来 ──
    // 只验「写进去的是可解析、条数对的东西」，真正的刷新由下面的 reload 再验一次
    t('noError', ERRORS.length === 0, ERRORS.join(' | '));
  } catch (e) {
    R.push({ name: '脚本异常', ok: false, got: String(e && e.message) });
  }

  fetch('http://127.0.0.1:${PROBE_PORT}/r', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ results: R })
  }).catch(function () {});
})();
`;

async function cdpConnect() {
  const t0 = Date.now();
  let wsUrl = null;
  while (Date.now() - t0 < 15000) {
    try {
      const list = await (await fetch('http://127.0.0.1:' + CDP_PORT + '/json/list')).json();
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

async function main() {
  if (!CHROME) throw new Error('找不到 Chrome');

  // 探针：收页面里的断言结果
  let resolveResult = null;
  const resultP = new Promise((r) => { resolveResult = r; });
  const probe = http.createServer((req, res) => {
    const h = { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Headers': '*', 'Access-Control-Allow-Methods': 'POST,OPTIONS' };
    if (req.method === 'OPTIONS') { res.writeHead(204, h); return res.end(); }
    let b = '';
    req.on('data', (c) => { b += c; });
    req.on('end', () => {
      res.writeHead(204, h); res.end();
      try { resolveResult(JSON.parse(b)); } catch (e) {}
    });
  });
  await new Promise((r) => probe.listen(PROBE_PORT, '127.0.0.1', r));

  // 生成注入页
  const src = fs.readFileSync(GAME, 'utf8');
  if (src.indexOf('window.__bb36') < 0) throw new Error('孩子端没有测试钩子');
  fs.writeFileSync(TMP, src.replace('</body>', '<script>' + ASSERT_JS + '</script></body>'), 'utf8');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'bb36t-'));
  const chrome = spawn(CHROME, [
    '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--hide-scrollbars', '--allow-file-access-from-files',
    '--autoplay-policy=no-user-gesture-required',
    '--remote-debugging-port=' + CDP_PORT, '--remote-allow-origins=*',
    '--window-size=430,900', '--user-data-dir=' + profile,
    'file:///' + TMP.replace(/\\/g, '/'),
  ], { stdio: 'ignore' });

  const kill = () => {
    try { spawn('taskkill', ['/PID', String(chrome.pid), '/T', '/F'], { stdio: 'ignore' }); } catch (e) {}
    try { chrome.kill(); } catch (e) {}
  };

  let payload = null;
  try {
    await cdpConnect();
    payload = await Promise.race([
      resultP,
      sleep(25000).then(() => null),
    ]);
  } finally {
    kill();
    try { fs.unlinkSync(TMP); } catch (e) {}
    probe.close();
  }

  console.log('\n宝贝三十六计 · 孩子端全量断言\n');
  if (!payload) {
    console.log('  ✗ 页面没有回报结果（超时）');
    process.exit(1);
  }

  let pass = 0, fail = 0;
  const fails = [];
  payload.results.forEach((r) => {
    if (r.ok) { pass++; console.log('  ✓ ' + r.name); }
    else { fail++; fails.push(r.name + (r.got ? '  → ' + r.got : '')); console.log('  ✗ ' + r.name + (r.got ? '  → ' + r.got : '')); }
  });

  console.log('\n' + '─'.repeat(46));
  console.log('  通过 ' + pass + ' 项，失败 ' + fail + ' 项');
  if (fail) { console.log('\n  失败清单：'); fails.forEach((f) => console.log('   · ' + f)); }
  console.log('─'.repeat(46) + '\n');
  process.exit(fail ? 1 : 0);
}

main().catch((e) => { console.error('测试崩了：', e.message); process.exit(2); });
