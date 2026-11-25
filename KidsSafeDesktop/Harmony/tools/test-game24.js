'use strict';
/*
 * 24 点 · 全量断言
 *
 * 重点验三件容易悄悄坏掉的事：
 *   1. 分数运算（3 8 3 8 的唯一解是 8/(3-8/3)，浮点会算出 23.999999999999996）；
 *   2. 出题器「保证有解」—— 随机 60 副牌全部可解，且难度档的数量约束成立；
 *   3. 上报星星（KidsSafe.award）真的被调用，且分数是「已解题数」。
 *
 * 用法：node Harmony/tools/test-game24.js
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

const CHROME = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
].find((p) => { try { return fs.existsSync(p); } catch (e) { return false; } });

const GAME = path.join(__dirname, '..', 'entry/src/main/resources/rawfile/playland/game24/index.html');
const TMP = path.join(path.dirname(GAME), '_assert.html');
const CDP_PORT = 9227;

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

const ASSERT_JS = `
(async function () {
  var G = window.__g24;
  var R = [];
  var ERRORS = [];
  var AWARDS = [];
  window.addEventListener('error', function (e) { ERRORS.push(String(e.message)); });
  // 扮演 ArkTS 侧注入的桥：记录上报，不真发星星
  window.KidsSafe = { award: function (k, s, sc, sub) { AWARDS.push({ k: k, s: s, sc: sc, sub: sub }); } };
  function wait(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }
  function t(name, cond, got) {
    R.push({ name: name, ok: !!cond, got: got === undefined || got === null ? '' : String(got) });
  }

  try {
    // ── 1. 分数运算 ──
    var a = G.frac(8, 3);
    t('fracReduce', a.n === 8 && a.d === 3, a.n + '/' + a.d);
    var b = G.apply(G.frac(3, 1), a, '-');      // 3 - 8/3 = 1/3
    t('fracSub', b.n === 1 && b.d === 3, b.n + '/' + b.d);
    var c = G.apply(G.frac(8, 1), b, '/');      // 8 / (1/3) = 24
    t('fracDivTo24', c.n === 24 && c.d === 1, c.n + '/' + c.d);
    t('is24True', G.is24(c) === true, '');
    // 约分后 48/2 就是 24，所以它该判「是」。真正的边界是 23.5 这种非整数：
    t('is24Half', G.is24(G.frac(47, 2)) === false, '47/2=23.5 不应判是');
    t('divZeroNull', G.apply(G.frac(1, 1), G.frac(0, 1), '/') === null, '');
    t('negSign', (function () { var x = G.apply(G.frac(1, 2), G.frac(3, 4), '-'); return x.n === -1 && x.d === 4; })(), '');

    // ── 2. 求解器 ──
    var s1 = G.solve([3, 8, 3, 8]);
    t('solveHard', !!s1, s1);
    // 1,1,1,1 怎么算都是 4，是最干净的「无解」样本。
    // （1,1,1,13 有解：(1+1)*(13-1)=24 —— 第一版断言就写错过这个。）
    t('solveNone', G.solve([1, 1, 1, 1]) === null, G.solve([1, 1, 1, 1]));
    t('solveSimple', !!G.solve([6, 6, 6, 6]), G.solve([6, 6, 6, 6]));
    t('countMany', G.count([6, 6, 6, 6]) >= 2, G.count([6, 6, 6, 6]));

    // ── 3. 出题器：连出 60 副，全部必须有解 ──
    var noSol = 0, badLen = 0, hardOk = true, easyOk = true;
    for (var i = 0; i < 20; i++) {
      var p0 = G.make(0), p1 = G.make(1), p2 = G.make(2);
      [p0, p1, p2].forEach(function (p) {
        if (!p || !p.nums || p.nums.length !== 4) { badLen++; return; }
        if (!G.solve(p.nums)) noSol++;
        for (var q = 0; q < 4; q++) {
          if (!(p.nums[q] >= 1 && p.nums[q] <= 13)) badLen++;
        }
      });
      if (p0.count < 3) easyOk = false;
      if (p2.count > 2) hardOk = false;
    }
    t('allSolvable', noSol === 0, noSol + ' 副无解');
    t('numsInRange', badLen === 0, badLen + ' 处异常');
    t('easyManySolutions', easyOk, '简单档要求 >= 3 种解法');
    t('hardFewSolutions', hardOk, '挑战档要求 <= 2 种解法');

    // ── 4. 摆一副 6 6 6 6，真玩一局 ──
    G.load([6, 6, 6, 6]);
    await wait(60);
    var st = G.state();
    t('loadFourCards', st.cards.length === 4, st.cards.length);
    var ids = st.cards.map(function (x) { return x.id; });
    G.pick(ids[0]); G.setOp('+'); G.pick(ids[1]);
    await wait(40);
    st = G.state();
    t('mergeToOne', st.cards.length === 3, st.cards.length);
    t('mergeValue12', st.cards.some(function (x) { return x.n === 12 && x.d === 1; }),
      JSON.stringify(st.cards.map(function (x) { return x.n + '/' + x.d; })));

    // 撤销回到 4 张
    G.undo();
    await wait(40);
    t('undoRestores', G.state().cards.length === 4, G.state().cards.length);

    // 6+6=12, 12+6=18, 18+6=24
    var cur = G.state().cards.map(function (x) { return x.id; });
    G.pick(cur[0]); G.setOp('+'); G.pick(cur[1]);
    await wait(30);
    var cur2 = G.state().cards;
    var v12 = cur2.filter(function (x) { return x.n === 12 && x.d === 1; })[0];
    var rest = cur2.filter(function (x) { return x.id !== v12.id; });
    G.pick(v12.id); G.setOp('+'); G.pick(rest[0].id);
    await wait(30);
    var cur3 = G.state().cards;
    var v18 = cur3.filter(function (x) { return x.n === 18 && x.d === 1; })[0];
    var rest2 = cur3.filter(function (x) { return x.id !== v18.id; });
    G.pick(v18.id); G.setOp('+'); G.pick(rest2[0].id);
    await wait(120);
    t('winState', G.state().finished === true, G.state().finished);
    t('winOverlay', document.getElementById('win').classList.contains('on'), '');
    t('winEqText', /24/.test(document.getElementById('winEq').textContent),
      document.getElementById('winEq').textContent);
    t('solvedInc', G.state().solved === 1, G.state().solved);
    t('awardCalled', AWARDS.length === 1, JSON.stringify(AWARDS));
    t('awardKey', AWARDS[0] && AWARDS[0].k === 'game24', AWARDS[0] && AWARDS[0].k);
    t('awardStars2', AWARDS[0] && AWARDS[0].s === 2, AWARDS[0] && AWARDS[0].s);
    t('awardScore', AWARDS[0] && AWARDS[0].sc === 1, AWARDS[0] && AWARDS[0].sc);

    // ── 5. 用了提示只给 1 颗 ──
    AWARDS.length = 0;
    G.load([4, 6, 2, 3]);
    G.hint();
    await wait(30);
    t('hintFlag', G.state().hintUsed === true, '');
    t('hintTrail', /先算/.test(document.getElementById('trail').textContent),
      document.getElementById('trail').textContent);
    t('hintHighlight', document.querySelectorAll('#table .card.hintA').length >= 2,
      document.querySelectorAll('#table .card.hintA').length);
    var st5 = G.state();
    var ids5 = st5.cards.map(function (x) { return x.id; });
    // 4*6=24, 然后 2-3 之类的补 1…… 直接按提示无法保证顺序，这里只验提示后能正常合并
    G.pick(ids5[0]); G.setOp('*'); G.pick(ids5[1]);
    await wait(40);
    t('mergeAfterHint', G.state().cards.length === 3, G.state().cards.length);

    // ── 6. 合完不是 24 → 不给过 ──
    G.load([1, 1, 1, 1]);
    var ids6 = G.state().cards.map(function (x) { return x.id; });
    G.pick(ids6[0]); G.setOp('+'); G.pick(ids6[1]);
    await wait(30);
    G.pick(G.state().cards[0].id); G.setOp('+'); G.pick(G.state().cards[1].id);
    await wait(30);
    G.pick(G.state().cards[0].id); G.setOp('+'); G.pick(G.state().cards[1].id);
    await wait(60);
    t('not24Rejected', G.state().finished === false, G.state().finished);
    t('not24HintText', /不是 24/.test(document.getElementById('trail').textContent),
      document.getElementById('trail').textContent);

    // ── 7. 换一题 / 切难度 ──
    G.next();
    await wait(60);
    t('nextFourCards', G.state().cards.length === 4, G.state().cards.length);
    t('nextNotFinished', G.state().finished === false, G.state().finished);
    G.setLevel(2);
    await wait(60);
    t('levelSwitch', G.state().level === 2, G.state().level);
    t('levelOnBtn', document.querySelector('.lv.on').dataset.lv === '2',
      document.querySelector('.lv.on').dataset.lv);

    // ── 8. 布局：牌和运算符都在屏幕内 ──
    var cards = document.querySelectorAll('#table .card');
    var r0 = cards[0].getBoundingClientRect();
    t('cardSquareish', r0.width > 48 && r0.height > 48 && r0.height < window.innerHeight * 0.6,
      r0.width.toFixed(0) + 'x' + r0.height.toFixed(0));

    // 四张牌的卡面都不能被裁剪，数字也不能空 —— ArkWeb 上「圆角 + overflow:hidden」
    // 会把数字整块丢掉，四张牌只画得出一张。这里钉死「不许裁」。
    var clipped = 0, blank = 0;
    for (var ci = 0; ci < cards.length; ci++) {
      var cs = getComputedStyle(cards[ci]);
      if (cs.overflow !== 'visible' || cs.overflowX !== 'visible' || cs.overflowY !== 'visible') clipped++;
      var numEl = cards[ci].querySelector('.num');
      // 注意：这段脚本是拼进 <script> 的模板字符串，正则里写 \d 会被吃掉转义变成 d，
      // 所以一律用 [0-9] 这种字符类，别用反斜杠简写。
      if (!numEl || !/[0-9]/.test(numEl.textContent)) blank++;
    }
    t('noCardClip', clipped === 0, clipped + ' 张牌带裁剪');
    t('allCardsShowNumber', blank === 0, blank + ' 张白板');

    // 长数字（100+）必须自动缩字号，否则没有裁剪兜底就会溢出到邻牌
    G.load([13, 13, 13, 13]);
    await wait(40);
    var i7 = G.state().cards.map(function (x) { return x.id; });
    G.pick(i7[0]); G.setOp('*'); G.pick(i7[1]);          // 169
    await wait(40);
    var big = Array.prototype.filter.call(document.querySelectorAll('#table .num'), function (n) {
      return n.textContent === '169';
    })[0];
    t('bigNumberShrinks', !!big && /w3/.test(big.className), big ? big.className : '没找到 169');
    if (big) {
      var bw = big.getBoundingClientRect();
      var pw = big.parentNode.getBoundingClientRect();
      t('bigNumberFits', bw.width <= pw.width - 6 && bw.height <= pw.height - 6,
        bw.width.toFixed(0) + 'x' + bw.height.toFixed(0) + ' / ' + pw.width.toFixed(0) + 'x' + pw.height.toFixed(0));
    }

    var ops = document.getElementById('ops').getBoundingClientRect();
    t('opsOnScreen', ops.bottom <= window.innerHeight + 1 && ops.top > 0,
      ops.top.toFixed(0) + '..' + ops.bottom.toFixed(0) + ' / ' + window.innerHeight);
    var app = document.getElementById('app').getBoundingClientRect();
    t('noBodyOverflow', document.documentElement.scrollHeight <= window.innerHeight + 2,
      document.documentElement.scrollHeight + ' / ' + window.innerHeight);
    t('appFills', app.height >= window.innerHeight - 2, app.height.toFixed(0) + ' / ' + window.innerHeight);

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
  if (src.indexOf('window.__g24') < 0) throw new Error('游戏没有测试钩子');
  fs.writeFileSync(TMP, src.replace('</body>',
    '<script>setTimeout(function(){' + ASSERT_JS + '}, 250);</script></body>'), 'utf8');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'g24-'));
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
    for (let i = 0; i < 80 && !payload; i++) {
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

  console.log('\n24 点 · 全量断言\n');
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
