'use strict';
/*
 * 记住它 · 全量断言
 *
 * 移植自 kidrememberit（Cocos2d-x / C++）。这类「玩法重写」最容易悄悄坏的是三处：
 *   1. 难度约束（种类 ≥ 张数/3、≤ 张数/2）算错 → 出题时某一种牌永远不出现；
 *   2. 时间因子公式抄错 → 满级也给孩子 25 秒，或者 5 张牌只给 3 秒；
 *   3. 记时到点直接判负 —— 原版流程是「记忆阶段到点 = 该翻面了」，不是失败。
 *
 * 用法：node Harmony/tools/test-memory.js
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

const CHROME = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
].find((p) => { try { return fs.existsSync(p); } catch (e) { return false; } });

const GAME = path.join(__dirname, '..', 'entry/src/main/resources/rawfile/playland/memory/index.html');
const TMP = path.join(path.dirname(GAME), '_assert.html');
const CDP_PORT = 9231;

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

const ASSERT_JS = `
(async function () {
  var M = window.__mem;
  var R = [];
  var ERRORS = [];
  var AWARDS = [];
  window.addEventListener('error', function (e) { ERRORS.push(String(e.message)); });
  window.KidsSafe = { award: function (k, s, sc, sub) { AWARDS.push({ k: k, s: s, sc: sc, sub: sub }); } };
  function wait(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }
  function t(name, cond, got) {
    R.push({ name: name, ok: !!cond, got: got === undefined || got === null ? '' : String(got) });
  }

  try {
    M.reset();
    M.set({ tutorial: true });
    M.closeSheet();

    // ── 1. 难度约束 ──
    var badFix = [];
    for (var n = 5; n <= 14; n++) {
      for (var ty = 1; ty <= 12; ty++) {
        var f = M.fixTypes(n, ty);
        if (f < Math.ceil(n / 3) || f > Math.floor(n / 2)) badFix.push(n + '/' + ty + '->' + f);
        if (f < 2 || f > 9) badFix.push(n + '/' + ty + '=范围外' + f);
      }
    }
    t('fixTypesBounds', badFix.length === 0, badFix.slice(0, 6).join(' '));

    // ── 2. 时间因子 ──
    t('timeFastest', Math.abs(M.timeOf(5, 255) - 5) < 0.001, M.timeOf(5, 255).toFixed(2));
    t('timeSlowest', Math.abs(M.timeOf(5, 0) - 25) < 0.001, M.timeOf(5, 0).toFixed(2));
    t('timeCap', Math.abs(M.timeOf(14, 0) - 70) < 0.001, M.timeOf(14, 0).toFixed(2));
    t('timeMid', M.timeOf(5, 128) > 5 && M.timeOf(5, 128) < 25, M.timeOf(5, 128).toFixed(2));
    // 因子越大给的时间越短
    t('timeMonotonic', M.timeOf(8, 200) < M.timeOf(8, 100), 'f200=' + M.timeOf(8, 200).toFixed(1) + ' f100=' + M.timeOf(8, 100).toFixed(1));

    // ── 3. 升降级 ──
    // state() 返回的是内部对象的引用，得先拍快照再比对，否则前后两次数值会一起变
    function snap() { return JSON.parse(JSON.stringify(M.state())); }
    M.set({ level: 1, numbers: 5, types: 2, remF: 0, repF: 0 });
    M.harder();
    var s1 = snap();
    t('harderLevelUp', s1.level === 2, s1.level);
    t('harderTighten', s1.remF > 0 && s1.repF > 0, s1.remF + '/' + s1.repF);
    t('harderKeepCardsOnOdd', s1.numbers === 5 && s1.types === 2, s1.numbers + '张' + s1.types + '种');
    M.harder();
    var s2 = snap();
    t('harderCardsEveryTwo', s2.types > s1.types || s2.numbers > s1.numbers,
      s1.numbers + '张' + s1.types + '种 -> ' + s2.numbers + '张' + s2.types + '种');

    M.set({ level: 9, numbers: 8, types: 4, remF: 100, repF: 100 });
    M.easier();
    t('easierLevelDown', M.state().level === 8, M.state().level);
    t('easierLoosen', M.state().remF < 100, M.state().remF);
    // 一直降不会掉到 5 张以下
    for (var k = 0; k < 12; k++) M.easier();
    t('easierFloor', M.state().numbers >= 5 && M.state().level >= 1, M.state().numbers + '张 / 第' + M.state().level);

    // 满级再升不炸
    M.set({ level: 60, numbers: 14, types: 9, remF: 255, repF: 255 });
    M.harder();
    t('maxNoCrash', M.state().numbers <= 14 && M.state().types <= 9,
      M.state().numbers + '张' + M.state().types + '种');

    // ── 4. 出题不变量 ──
    M.set({ level: 1, numbers: 7, types: 3, remF: 0, repF: 0 });
    var badLen = 0, badRange = 0, badTypes = 0;
    for (var round = 0; round < 40; round++) {
      M.closeSheet();
      M.startLevel();
      var nums = M.nums();
      if (nums.length !== 7) badLen++;
      var uniq = {};
      nums.forEach(function (v) {
        if (!(v >= 1 && v <= 9)) badRange++;
        uniq[v] = 1;
      });
      if (Object.keys(uniq).length !== 3) badTypes++;
      M.game().left = 0.01;   // 下一帧就归零，用来跳过记忆阶段
      await wait(60);
    }
    t('dealLength', badLen === 0, badLen + ' 局长度不对');
    t('dealRange', badRange === 0, badRange + ' 张值越界');
    t('dealAllTypesUsed', badTypes === 0, badTypes + ' 局种类没用满（应是 3）');

    // ── 5. 记忆到点 = 进还原阶段，不是判负 ──
    M.set({ numbers: 5, types: 2, remF: 255, repF: 255 });
    M.closeSheet();
    M.startLevel();
    await wait(40);
    t('phaseRemember', M.game().phase === 'remember', M.game().phase);
    M.game().left = 0.01;
    await wait(600);
    t('phaseRepresent', M.game().phase === 'represent', M.game().phase);
    t('representPoolShown', !document.getElementById('pool').classList.contains('hide'), '');
    t('noSheetAfterRemember', !M.isSheetOpen(), '记忆到点不该弹结算');
    t('poolCount', document.querySelectorAll('#pool .card').length === 5,
      document.querySelectorAll('#pool .card').length);

    // ── 6. 放错要计数、放对要推进 ──
    var nums5 = M.nums();
    var pool = M.pool();
    var wrongIdx = -1;
    for (var wi = 0; wi < pool.length; wi++) if (pool[wi] !== nums5[0]) { wrongIdx = wi; break; }
    if (wrongIdx >= 0) {
      M.tapPool(wrongIdx);
      await wait(30);
      t('wrongNoProgress', M.filled() === 0 && M.game().errors === 1,
        M.filled() + '/' + M.game().errors);
    } else {
      t('wrongNoProgress', false, '牌堆里没找到不同的牌');
    }

    // 按正确顺序点完
    for (var step = 0; step < 5; step++) {
      var want = M.nums()[M.filled()];
      var p = M.pool();
      var hit = p.indexOf(want);
      M.tapPool(hit);
      await wait(30);
    }
    t('allFilled', M.filled() === 5, M.filled());
    await wait(80);
    t('sheetAfterClear', M.isSheetOpen(), '');
    t('awardOnce', AWARDS.length === 1, JSON.stringify(AWARDS));
    t('awardKey', AWARDS[0] && AWARDS[0].k === 'memory', AWARDS[0] && AWARDS[0].k);
    t('awardLevel', AWARDS[0] && AWARDS[0].sc === 1, AWARDS[0] && AWARDS[0].sc);
    t('awardStarsRange', AWARDS[0] && AWARDS[0].s >= 2 && AWARDS[0].s <= 4, AWARDS[0] && AWARDS[0].s);
    t('awardSub', AWARDS[0] && AWARDS[0].sub.length > 0, AWARDS[0] && AWARDS[0].sub);
    t('levelUpAfterWin', M.state().level === 2, M.state().level);

    // ── 7. 还原超时 → 判负 + 降级 ──
    AWARDS.length = 0;
    var lvBefore = M.state().level;
    M.set({ numbers: 5, types: 2, remF: 255, repF: 255 });
    M.closeSheet();
    M.startLevel();
    await wait(30);
    M.game().left = 0.01;
    await wait(500);                       // 记忆 → 还原
    M.game().left = 0.01;
    await wait(120);
    t('timeoutSheet', M.isSheetOpen(), '');
    t('timeoutNoAward', AWARDS.length === 0, AWARDS.length);
    t('timeoutLevelDown', M.state().level === Math.max(1, lvBefore - 1), M.state().level);

    // ── 8. 每第 3 关是配对 ──
    M.set({ level: 3 });
    M.closeSheet();
    M.startLevel();
    await wait(40);
    t('pairPhase', M.game().phase === 'pair', M.game().phase);
    t('kindOf3IsPair', M.kindOf(3) === 'pair' && M.kindOf(4) === 'remember', M.kindOf(3) + '/' + M.kindOf(4));
    var pairSlots = document.querySelectorAll('#board .slot');
    t('pairBoardSize', pairSlots.length >= 8 && pairSlots.length % 2 === 0, pairSlots.length);

    // 按值分组，一对一对点掉
    var byVal = {};
    for (var q = 0; q < pairSlots.length; q++) {
      var vq = pairSlots[q].dataset.v;
      (byVal[vq] = byVal[vq] || []).push(q);
    }
    var pairsLeft = Object.keys(byVal).length;
    for (var key in byVal) {
      var idxs = byVal[key];
      M.tapPair(idxs[0]);
      await wait(30);
      M.tapPair(idxs[1]);
      await wait(420);
    }
    await wait(200);
    t('pairAllMatched', M.game().matched === pairsLeft, M.game().matched + '/' + pairsLeft);
    t('pairSheet', M.isSheetOpen(), '');

    // ── 9. 存档落到 localStorage ──
    var raw = localStorage.getItem('kidsafe.memory.v1');
    t('savedBlob', !!raw && /"level"/.test(raw), raw ? raw.slice(0, 60) : '没写');

    // ── 10. 布局：牌不裁剪、数字可读、不溢出 ──
    M.set({ level: 1, numbers: 8, types: 3, remF: 0, repF: 0 });
    M.closeSheet();
    M.startLevel();
    await wait(60);
    var cards = document.querySelectorAll('#board .card');
    var clipped = 0, blank = 0;
    for (var ci = 0; ci < cards.length; ci++) {
      var cs = getComputedStyle(cards[ci]);
      if (cs.overflow !== 'visible' || cs.overflowY !== 'visible') clipped++;
      if (!/[1-9]/.test(cards[ci].textContent)) blank++;
    }
    t('noCardClip', clipped === 0, clipped + ' 张带裁剪');
    t('allCardsShowNumber', blank === 0 && cards.length === 8, blank + ' 张白板 / 共 ' + cards.length);
    var r0 = cards[0].getBoundingClientRect();
    t('cardSquareish', r0.width > 40 && Math.abs(r0.width - r0.height) < 4,
      r0.width.toFixed(0) + 'x' + r0.height.toFixed(0));
    t('boardOnScreen', document.getElementById('board').getBoundingClientRect().bottom <= window.innerHeight + 1,
      document.getElementById('board').getBoundingClientRect().bottom.toFixed(0) + ' / ' + window.innerHeight);
    t('noBodyOverflow', document.documentElement.scrollHeight <= window.innerHeight + 2,
      document.documentElement.scrollHeight + ' / ' + window.innerHeight);

    t('noError', ERRORS.length === 0, ERRORS.join(' | '));
    window.__RESULT = { results: R };
  } catch (e) {
    R.push({ name: '脚本异常', ok: false, got: String(e && e.stack || e) });
    window.__RESULT = { results: R };
  }
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

  const src = fs.readFileSync(GAME, 'utf8');
  if (src.indexOf('window.__mem') < 0) throw new Error('游戏没有测试钩子');
  fs.writeFileSync(TMP, src.replace('</body>',
    '<script>setTimeout(function(){' + ASSERT_JS + '}, 250);</script></body>'), 'utf8');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'mem-'));
  const chrome = spawn(CHROME, [
    '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--hide-scrollbars', '--allow-file-access-from-files',
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
    const cdp = await cdpConnect();
    for (let i = 0; i < 90 && !payload; i++) {
      await sleep(200);
      const r = await cdp.send('Runtime.evaluate', {
        expression: 'window.__RESULT ? JSON.stringify(window.__RESULT) : ""',
        returnByValue: true,
      });
      const v = r.result && r.result.result && r.result.result.value;
      if (v) payload = JSON.parse(v);
    }
    cdp.close();
  } finally {
    kill();
    try { fs.unlinkSync(TMP); } catch (e) {}
  }

  console.log('\n记住它 · 全量断言\n');
  if (!payload) {
    console.log('  ✗ 页面没有回报结果');
    process.exit(1);
  }
  let pass = 0;
  const fails = [];
  payload.results.forEach((r) => { if (r.ok) { pass++; } else { fails.push(r); } });
  payload.results.forEach((r) => {
    if (!r.ok) console.log('  ✗ ' + r.name + '  ' + r.got);
  });
  console.log('  ' + pass + '/' + payload.results.length + ' 通过');
  process.exit(fails.length ? 1 : 0);
}

main().catch((e) => { console.error(e.message); process.exit(1); });
