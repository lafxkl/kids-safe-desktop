'use strict';
/*
 * 三三合成 · 全量断言
 *
 * 移植自 mr3（Threes! 玩法，原工程依赖宿主 opengl/component 模块，跑不起来）。
 * 重写后最容易错的是「一回合只能合一次」这条 —— 少了 merged 标记，
 * 6 6 6 会被连续合成 12，分数与手感全变。这里逐条钉死。
 *
 * 用法：node Harmony/tools/test-threes.js
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

const CHROME = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
].find((p) => { try { return fs.existsSync(p); } catch (e) { return false; } });

const GAME = path.join(__dirname, '..', 'entry/src/main/resources/rawfile/playland/threes/index.html');
const TMP = path.join(path.dirname(GAME), '_assert.html');
const CDP_PORT = 9233;

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

const ASSERT_JS = `
(async function () {
  var T = window.__t3;
  var R = [];
  var ERRORS = [];
  var AWARDS = [];
  window.addEventListener('error', function (e) { ERRORS.push(String(e.message)); });
  window.KidsSafe = { award: function (k, s, sc, sub) { AWARDS.push({ k: k, s: s, sc: sc, sub: sub }); } };
  function wait(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }
  function t(name, cond, got) {
    R.push({ name: name, ok: !!cond, got: got === undefined || got === null ? '' : String(got) });
  }
  function row(r) {
    var b = T.board()[r] || [];
    return b.filter(function (v) { return v !== 0; }).join(',');
  }

  try {
    // ── 1. 合并规则 ──
    t('merge1plus2', T.canMerge(1, 2) && T.canMerge(2, 1), '');
    t('mergeSameGe6', T.canMerge(3, 3) && T.canMerge(6, 6) && T.canMerge(768, 768), '');
    t('noMerge1plus1', !T.canMerge(1, 1), '');
    t('noMerge2plus2', !T.canMerge(2, 2), '');
    t('noMerge3plus6', !T.canMerge(3, 6), '');
    t('mergeCap', !T.canMerge(6144, 6144), '');

    // ── 2. 新块只会是 1/2/3（8% 的台阶牌要盘面有 48 才会出现）──
    var bad = 0, seen = {};
    for (var i = 0; i < 800; i++) {
      var v = T.newValue();
      seen[v] = (seen[v] || 0) + 1;
      if (v < 1 || v > 3) bad++;
    }
    t('newValueOnly123', bad === 0, bad + ' 次越界 ' + JSON.stringify(seen));
    t('newValueCoversAll', !!(seen[1] && seen[2] && seen[3]), JSON.stringify(seen));

    // ── 3. 左滑 1 2 3 → 3 3，得分 3 ──
    T.load([[1, 2, 3, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]]);
    var ok3 = T.slide('left');
    t('slideReturnsTrue', ok3 === true, ok3);
    await wait(200);
    // 每步会补一块新牌，新牌可能落在同一行，所以只比对行首
    t('merge123', row(0).indexOf('3,3') === 0, row(0));
    t('scoreAfter123', T.score() === 3, T.score());

    // ── 4. 一回合只合一次：6 6 6 → 12 6 ──
    T.load([[6, 6, 6, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]]);
    await wait(20);
    T.slide('left');
    await wait(200);
    t('singleMergePerTurn', row(0).indexOf('12,6') === 0, row(0));

    // ── 5. 3 3 3 3 → 6 6 ──
    T.load([[3, 3, 3, 3], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]]);
    await wait(20);
    T.slide('left');
    await wait(200);
    // 滑动之后会新加一张牌，有四分之一的概率落在第 0 行末尾（变成 '6,6,1'）。
    // 只比前两项，别让随机数决定这条断言过不过。
    t('fourThrees', row(0).indexOf('6,6') === 0, row(0));

    // ── 6. 1 1 不能合，但会往左靠 ──
    T.load([[0, 1, 0, 1], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]]);
    await wait(20);
    var moved = T.slide('left');
    await wait(200);
    t('onesDontMerge', moved === true && row(0).indexOf('1,1') === 0, row(0));
    t('noScoreOnPlainMove', T.score() === 0, T.score());

    // ── 7. 1 2 不贴边也不该动 ──
    T.load([[0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [3, 6, 12, 24]]);
    await wait(20);
    var noMove = T.slide('left');
    t('noMoveReturnsFalse', noMove === false, noMove);

    // ── 8. 每步必须补一块新牌 ──
    T.load([[0, 1, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]]);
    await wait(20);
    T.slide('left');
    await wait(250);
    var count = 0;
    T.board().forEach(function (r) { r.forEach(function (v) { if (v) count++; }); });
    t('addNewTileEachMove', count === 2, count + ' 块');

    // ── 9. 下一块预览必须跟着换 ──
    var previews = {};
    // 6 次太少：新牌的权重是 1/2/3 各三分之一，6 次全撞同一个值的概率约 0.4%，
    // 但实测就是会撞上，改成 12 次（约 0.0002%）。
    for (var k = 0; k < 12; k++) {
      T.load([[1, 1, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]]);
      await wait(20);
      T.slide('left');
      await wait(220);
      previews[document.getElementById('next').textContent] = 1;
    }
    t('previewUpdates', Object.keys(previews).length >= 2, Object.keys(previews).join(','));

    // ── 10. 分数累加（6+6=12 得 12 分，12+12=24 得 24 分，共 36）──
    T.load([[6, 6, 0, 0], [12, 12, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]]);
    await wait(20);
    T.slide('left');
    await wait(220);
    t('scoreDoubleMerge', T.score() === 36, T.score());

    // ── 11. 满盘且无可合 → 结束 + 上报 ──
    // 注意别摆出 1/2 相邻：1 和 2 是能合的，盘面就还有得走
    T.load([[3, 6, 12, 24], [48, 96, 192, 384], [6, 12, 24, 48], [96, 192, 384, 768]]);
    await wait(20);
    t('hasNoMove', T.hasMove() === false, '');
    T.setScore(700);
    AWARDS.length = 0;
    T.checkOver();
    await wait(60);
    t('overFlag', T.over() === true, T.over());
    t('overAward', AWARDS.length === 1, JSON.stringify(AWARDS));
    t('awardKeyThrees', AWARDS[0] && AWARDS[0].k === 'threes', AWARDS[0] && AWARDS[0].k);
    t('awardStars', AWARDS[0] && AWARDS[0].s >= 1 && AWARDS[0].s <= 5, AWARDS[0] && AWARDS[0].s);
    t('awardScore', AWARDS[0] && AWARDS[0].sc === 700, AWARDS[0] && AWARDS[0].sc);

    // ── 12. 结束之后滑动无效 ──
    var dead = T.slide('left');
    t('noSlideAfterOver', dead === false, dead);

    // ── 13. 重开后盘面清空、有 1 和 2 ──
    T.start();
    await wait(120);
    var vals = [];
    T.board().forEach(function (r) { r.forEach(function (v) { if (v) vals.push(v); }); });
    vals.sort();
    t('restartTwoTiles', vals.length === 2, vals.join(','));
    t('restartHasOnesTwos', vals[0] === 1 && vals[1] === 2, vals.join(','));
    t('restartScoreZero', T.score() === 0, T.score());

    // ── 14. 最高分落盘 ──
    var raw = localStorage.getItem('ks3.best');
    t('bestSaved', !!raw && parseInt(raw, 10) === T.best() && T.best() > 0, raw + ' / best=' + T.best());

    // ── 15. 布局 ──
    var boardEl = document.getElementById('board');
    var br = boardEl.getBoundingClientRect();
    t('boardSquare', Math.abs(br.width - br.height) < 2, br.width.toFixed(0) + 'x' + br.height.toFixed(0));
    t('boardOnScreen', br.top >= 0 && br.bottom <= window.innerHeight + 1,
      br.top.toFixed(0) + '..' + br.bottom.toFixed(0) + ' / ' + window.innerHeight);
    t('boardNotTooSmall', br.width > 200, br.width.toFixed(0));
    var tileEls = document.querySelectorAll('#board .tile');
    var outside = 0;
    for (var ti = 0; ti < tileEls.length; ti++) {
      var tr = tileEls[ti].getBoundingClientRect();
      if (tr.left < br.left - 1 || tr.right > br.right + 1 || tr.top < br.top - 1 || tr.bottom > br.bottom + 1) outside++;
    }
    t('tilesInsideBoard', outside === 0, outside + ' 块出界');
    t('tileHasText', tileEls.length > 0 && /[1-9]/.test(tileEls[0].textContent), tileEls.length ? tileEls[0].textContent : '无砖块');
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
  if (src.indexOf('window.__t3') < 0) throw new Error('游戏没有测试钩子');
  fs.writeFileSync(TMP, src.replace('</body>',
    '<script>setTimeout(function(){' + ASSERT_JS + '}, 250);</script></body>'), 'utf8');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 't3-'));
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

  console.log('\n三三合成 · 全量断言\n');
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
