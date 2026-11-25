'use strict';
/*
 * 病毒斗士 · 全量断言
 *
 * 在无头 Chrome 里跑真实 index.html，用页面自己的 __vf 钩子把一局走完：
 * 开图 → 蛇走 → 撞墙转向 → 球撞病毒 → 吃驰援 → 撞病毒被定住 → 过关 → 通关。
 *
 * 时间轴全靠 __vf.step(dt) 手动推进，不用真实时间 —— 无头里 setInterval 跑得快慢不定，
 * 依赖真实时钟的断言必然飘。
 *
 * 用法：node Harmony/tools/test-virusfighter.js
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

const GAME = path.join(__dirname, '..', 'entry/src/main/resources/rawfile/playland/virusfighter/index.html');
const TMP = path.join(path.dirname(GAME), '_assert.html');
const PROBE_PORT = 8096;
const CDP_PORT = 9223;

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

const ASSERT_JS = `
(async function () {
  var V = window.__vf;
  var R = [];
  var ERRORS = [];
  window.addEventListener('error', function (e) { ERRORS.push(String(e.message)); });

  function t(name, cond, got) {
    R.push({ name: name, ok: !!cond, got: got === undefined || got === null ? '' : String(got) });
  }
  // 推进 n 秒，按 1/60 秒切片，模拟 60fps
  function run(sec) {
    var n = Math.round(sec * 60);
    for (var i = 0; i < n; i++) V.step(1 / 60);
  }

  try {
    V.pause(true);

    // 图片是异步加载的，fit() 在加载完成回调里才跑。
    // 不等它，canvas 会停在内核默认的 300×150，后面所有尺寸断言都是假的。
    for (var w = 0; w < 120 && !(V.side > 0); w++) {
      await new Promise(function (r) { setTimeout(r, 50); });
    }

    // ── 1. 静态结构 ──
    t('canvasSquare', V.side > 100 && Math.abs(V.side % 1) < 0.01, V.side);
    t('unitPositive', V.unit > 0 && Math.abs(V.unit * 20 - V.side) < 0.01, V.unit.toFixed(2));

    // ── 2. 开局 ──
    V.start();
    t('stateLoading', V.state === 'loading', V.state);
    t('stage1', V.stage === 1, V.stage);
    t('virus14', V.virusCount() === 14, V.virusCount());

    var kr = V.kindRange();
    t('virusKinds', kr[0] >= 1 && kr[1] <= 10, kr.join('~'));

    run(1.6);
    t('statePlaying', V.state === 'playing', V.state);
    t('snakeLen8', V.snake.body.length === 8, V.snake.body.length);
    t('hudScore', document.getElementById('hScore').textContent === '0',
      document.getElementById('hScore').textContent);

    // ── 3. 蛇会走 ──
    var h0 = V.snake.body[0].slice();
    run(0.6);
    var h1 = V.snake.body[0].slice();
    t('snakeMoves', h0[0] !== h1[0] || h0[1] !== h1[1], h0 + ' → ' + h1);
    t('snakeLenKept', V.snake.body.length === 8, V.snake.body.length);
    t('headInBounds', V.headInBounds(), V.snake.body[0]);

    // ── 4. 跑 20 秒不越界、不散架 ──
    var okBounds = true, okLen = true;
    for (var i = 0; i < 20 * 60; i++) {
      V.step(1 / 60);
      if (!V.headInBounds()) okBounds = false;
      if (V.snake.body.length < 1 || V.snake.body.length > 50) okLen = false;
      if (V.state !== 'playing') break;
    }
    t('neverOutOfBounds', okBounds, '');
    t('lengthAlwaysSane', okLen, V.snake.body.length);

    // ── 5. 撞墙自动转向 ──
    // 摆一条贴着右墙、头朝右的队伍，一步之后必须离开墙面而不是出界
    V.placeSnake(19, 10, [1, 0], 4);
    V.step(0.3);
    t('wallTurnStaysInside', V.headInBounds(), V.snake.body[0]);
    t('wallTurnChangedDir', !(V.snake.dir[0] === 1 && V.snake.dir[1] === 0), V.snake.dir.join(','));

    // ── 6. 药片撞病毒：消一个、+5 分、反弹 ──
    // 第 1 关 y=3 那行只有 x=6 和 x=13 两个病毒
    V.start(); run(1.6);
    var before = V.virusCount(), sc0 = V.score;
    V.placeBall(6, 4, 0, -1);          // 球在 (6,4)，头朝上，(6,3) 有病毒
    var topBefore = V.grid[3][6];
    V.step(0.3);
    t('ballAteVirus', topBefore !== 0 && V.grid[3][6] === 0, 'before=' + topBefore + ' after=' + V.grid[3][6]);
    t('ballCountDown', V.virusCount() === before - 1, before + ' → ' + V.virusCount());
    t('ballScorePlus5', V.score === sc0 + 5, sc0 + ' → ' + V.score);
    t('ballBounced', V.ball.dy === 1, V.ball.dy);

    // ── 7. 吃驰援：+20 分，队伍变长 ──
    V.start(); run(1.6);
    var len0 = V.snake.body.length, sc1 = V.score;
    V.putFoodAtHead();
    run(0.4);
    t('cureGrew', V.snake.body.length === len0 + 1, len0 + ' → ' + V.snake.body.length);
    t('cureScore', V.score === sc1 + 20, sc1 + ' → ' + V.score);

    // ── 8. 撞病毒 → 定住闪烁 → 截断队伍 ──
    V.start(); run(1.6);
    // 先喂长一点，确认惩罚会截到 15
    for (var k = 0; k < 12; k++) { V.putFoodAtHead(); run(0.3); }
    var lenBig = V.snake.body.length;
    var crashed = V.crashIntoVirus();
    t('crashTriggered', crashed === true, crashed);
    t('punishOn', V.punish.on === true, V.punish.on);
    t('punishFlashVisible', typeof V.punish.visible === 'boolean', V.punish.visible);

    // 闪烁期间队伍不该继续前进
    var stay = JSON.stringify(V.snake.body[0]);
    run(0.2);
    t('punishFreezes', JSON.stringify(V.snake.body[0]) === stay, stay);

    run(2.0);
    t('punishEnds', V.punish.on === false, V.punish.on);
    t('punishTruncated', V.snake.body.length <= 15 && V.snake.body.length < lenBig,
      lenBig + ' → ' + V.snake.body.length);
    t('punishDirReset', V.snake.dir[0] === 1 && V.snake.dir[1] === 0, V.snake.dir.join(','));

    // ── 9. 时间奖励随时间掉 ──
    V.start(); run(1.6);
    var b1 = parseInt(document.getElementById('hBonus').textContent, 10);
    run(10);
    var b2 = parseInt(document.getElementById('hBonus').textContent, 10);
    t('bonusDecays', b2 < b1 && b1 <= 3000, b1 + ' → ' + b2);
    t('bonusFloor', b2 >= 0, b2);

    // ── 10. 清空病毒就过关 ──
    V.start(); run(1.6);
    var scBefore = V.score;
    V.clearVirus();
    run(0.1);
    t('stageAdvanced', V.stage === 2, V.stage);
    t('stageBonusAdded', V.score > scBefore + 1000, scBefore + ' → ' + V.score);
    run(1.6);
    t('stage2Playing', V.state === 'playing' && V.stage === 2, V.state + '/' + V.stage);
    t('stage2HasVirus', V.virusCount() === 37, V.virusCount());

    // ── 11. 五关走完进结算 ──
    for (var s = 2; s <= 5; s++) {
      if (V.state === 'loading') run(1.6);
      if (V.state !== 'playing') break;
      V.clearVirus();
      run(1.6);
    }
    t('winState', V.state === 'win', V.state);
    t('winScoreShown', document.getElementById('overScore').textContent === String(V.score),
      document.getElementById('overScore').textContent + ' vs ' + V.score);
    t('bestSaved', V.best === V.score, V.best);
    run(4.0);
    t('thanksState', V.state === 'thanks', V.state);
    t('thanksVisible', document.getElementById('thanks').classList.contains('on'), '');

    // ── 12. 重开能回到第一关 ──
    V.start();
    t('restartStage1', V.stage === 1, V.stage);
    t('restartScore0', V.score === 0, V.score);
    t('restartVirus14', V.virusCount() === 14, V.virusCount());

    // ── 13. 界面尺寸 ──
    var cv = document.getElementById('cv');
    var cr = cv.getBoundingClientRect();
    t('cvSquareRendered', Math.abs(cr.width - cr.height) < 1.5, cr.width.toFixed(1) + 'x' + cr.height.toFixed(1));
    t('cvFitsViewport', cr.width <= window.innerWidth + 1 && cr.height <= window.innerHeight + 1,
      cr.width.toFixed(0) + 'x' + cr.height.toFixed(0) + ' in ' + window.innerWidth + 'x' + window.innerHeight);
    var pad = document.getElementById('pad').getBoundingClientRect();
    t('padVisible', pad.height > 40, pad.height.toFixed(0));

    t('noError', ERRORS.length === 0, ERRORS.join(' | '));
  } catch (e) {
    R.push({ name: '脚本异常', ok: false, got: String(e && e.stack || e) });
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
    } catch (e) { /* Chrome 还没起来，继续等 */ }
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

  let resolveResult = null;
  const resultP = new Promise((r) => { resolveResult = r; });
  const probe = http.createServer((req, res) => {
    const h = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Headers': '*',
      'Access-Control-Allow-Methods': 'POST,OPTIONS',
    };
    if (req.method === 'OPTIONS') { res.writeHead(204, h); return res.end(); }
    let b = '';
    req.on('data', (c) => { b += c; });
    req.on('end', () => {
      res.writeHead(204, h); res.end();
      try { resolveResult(JSON.parse(b)); } catch (e) {}
    });
  });
  await new Promise((r) => probe.listen(PROBE_PORT, '127.0.0.1', r));

  const src = fs.readFileSync(GAME, 'utf8');
  if (src.indexOf('window.__vf') < 0) throw new Error('游戏没有测试钩子');
  fs.writeFileSync(TMP, src.replace('</body>', '<script>' + ASSERT_JS + '</script></body>'), 'utf8');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'vft-'));
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
    payload = await Promise.race([resultP, sleep(40000).then(() => null)]);
  } finally {
    kill();
    try { fs.unlinkSync(TMP); } catch (e) {}
    probe.close();
  }

  console.log('\n病毒斗士 · 全量断言\n');
  if (!payload) {
    console.log('  ✗ 页面没有回报结果（超时）');
    process.exit(1);
  }

  let pass = 0;
  const fails = [];
  payload.results.forEach((r) => {
    if (r.ok) { pass++; } else { fails.push(r); }
  });
  payload.results.forEach((r) => {
    if (!r.ok) console.log('  ✗ ' + r.name + '  ' + r.got);
  });
  console.log('  ' + pass + '/' + payload.results.length + ' 通过');
  process.exit(fails.length ? 1 : 0);
}

main().catch((e) => { console.error(e.message); process.exit(1); });
