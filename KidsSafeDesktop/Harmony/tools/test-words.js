'use strict';
/*
 * 词语组合（语文）/ 单词组合（英语）· 全量断言
 *
 * 两个游戏是分开的（独立目录、独立星星 key、独立存档），但判定规则是同一套：
 *   点一块接在后面 → 当前串必须是某个未完成词的前缀 → 等于某个词就消掉。
 * 这里把两边的规则、出题、牌面、连线、结算、配色各钉一遍，
 * 顺便验证「两边判定确实一致」——同一个用例分别喂五笔和字母。
 *
 * 用法：node Harmony/tools/test-words.js
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

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

/* =========================================================
 * 共用断言骨架（两个游戏注进来的差别只有：钩子名、字段数、配色）
 * ========================================================= */
function assertBody() {
  return `
  var R = [], ERRORS = [], AWARDS = [];
  window.addEventListener('error', function (e) { ERRORS.push(String(e.message)); });
  window.KidsSafe = { award: function (k, s, sc, sub) { AWARDS.push({ k: k, s: s, sc: sc, sub: sub }); } };
  function wait(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }
  function t(name, cond, got) { R.push({ name: name, ok: !!cond, got: got === undefined || got === null ? '' : String(got) }); }
  function allLen(a, n) { for (var i = 0; i < a.length; i++) if (a[i].length !== n) return false; return true; }
  function uniqN(a) { var m = {}; a.forEach(function (x) { m[x] = 1; }); return Object.keys(m).length; }
  function counts(a) { var m = {}; a.forEach(function (x) { m[x] = (m[x] || 0) + 1; }); return m; }
  /* 牌面上的字符多重集合必须等于所有词的字符合集 */
  function sameChars(words, cells) {
    var w = {};
    words.forEach(function (wd) { for (var k = 0; k < wd.length; k++) w[wd[k]] = (w[wd[k]] || 0) + 1; });
    var c = counts(cells);
    var keys = Object.keys(w);
    if (keys.length !== Object.keys(c).length) return false;
    for (var i = 0; i < keys.length; i++) if (w[keys[i]] !== c[keys[i]]) return false;
    return true;
  }
  /* 找一个「接不上」的块：它的字符不是任何未完成词的首字符 */
  function findBadStart() {
    var starts = T.remain().map(function (w) { return w.charAt(0); });
    var cs = T.cells();
    for (var i = 0; i < cs.length; i++) if (starts.indexOf(cs[i]) < 0) return i;
    return -1;
  }
  /* 拼完当前所有词，返回拼失败的数量 */
  async function clearLevel() {
    var left = T.remain().slice(), bad = 0;
    for (var i = 0; i < left.length; i++) {
      T.clear();
      if (!T.spell(left[i])) bad++;
      await wait(650);
    }
    await wait(250);
    return bad;
  }
`;
}

/* ---------- 语文 ---------- */
const ZH_JS = `
(async function () {
  var T = window.__wz;
  ${assertBody()}
  try {
    // ── 1. 词库：只有两字词和四字成语 ──
    t('pool2AllTwoChars', allLen(T.words2, 2), T.words2.length + ' 个');
    t('pool4AllFourChars', allLen(T.words4, 4), T.words4.length + ' 个');
    t('poolSize', T.words2.length >= 60 && T.words4.length >= 40, T.words2.length + '/' + T.words4.length);
    t('poolUnique2', uniqN(T.words2) === T.words2.length, uniqN(T.words2) + '/' + T.words2.length);
    t('poolUnique4', uniqN(T.words4) === T.words4.length, uniqN(T.words4) + '/' + T.words4.length);

    // ── 2. 出题：第 1 关四个两字词，第 4 关起混成语 ──
    var w1 = T.makeWords(1);
    t('lvl1FourWords', w1.length === 4, w1.join(','));
    t('lvl1AllTwo', allLen(w1, 2), w1.join(','));
    t('lvl1Unique', uniqN(w1) === 4, w1.join(','));
    var w5 = T.makeWords(5);
    t('lvl5OneIdiom', w5.filter(function (x) { return x.length === 2; }).length === 3 &&
      w5.filter(function (x) { return x.length === 4; }).length === 1, w5.join(','));
    var w8 = T.makeWords(8);
    t('lvl8TwoIdioms', w8.filter(function (x) { return x.length === 2; }).length === 2 &&
      w8.filter(function (x) { return x.length === 4; }).length === 2, w8.join(','));

    // ── 3. 开局牌面 ──
    T.start(1);
    await wait(150);
    t('lvl1Tiles8', T.cells().length === 8, T.cells().join(''));
    t('tilesMatchWords', sameChars(T.remain(), T.cells()), T.cells().join(''));
    t('cols4', T.cols() === 4, T.cols());

    // ── 4. 接不上的字：不接、只计数 ──
    var bad = findBadStart();
    if (bad >= 0) {
      var before = T.picked();
      T.tap(bad);
      t('rejectKeepsString', T.picked() === before, '点 ' + T.cells()[bad] + ' → "' + T.picked() + '"');
      t('rejectCounts', T.wrong() === 1, T.wrong());
      T.clear();
    } else {
      t('rejectKeepsString', true, 'skipped（牌面全是首字）');
      t('rejectCounts', true, 'skipped');
    }

    // ── 5. 再来一次点已选的字 = 取消 ──
    var w = T.remain()[0];
    T.clear();
    var idx0 = T.tapLetter(w.charAt(0));
    t('prefixAccepted', T.picked() === w.charAt(0), T.picked());
    T.tap(idx0);
    t('cancelByTap', T.picked() === '', T.picked());

    // ── 6. 拼完一个词：消掉、加分、计数 ──
    T.clear();
    var okSpell = T.spell(w);
    t('spellWordOk', okSpell && T.picked() === w, T.picked());
    if (w.length >= 2) t('lineDrawn', T.lineNodes() > 0, T.lineNodes());
    await wait(700);
    t('wordRemoved', T.remain().indexOf(w) < 0, T.remain().join(','));
    t('scoreForWord', T.score() === w.length * 10, T.score());
    t('doneCounter', document.getElementById('done').textContent === '1',
      document.getElementById('done').textContent);

    // ── 7. 重选按钮 ──
    T.clear();
    T.tapLetter(T.remain()[0].charAt(0));
    document.getElementById('btnClear').click();
    t('clearBtn', T.picked() === '', T.picked());
    t('clearNoWrongCount', T.wrong() === 1, T.wrong());

    // ── 8. 全部拼完 → 结算 + 三颗星 ──
    T.start(2);
    await wait(150);
    AWARDS.length = 0;
    var badSpell = await clearLevel();
    t('allSpelled', badSpell === 0, badSpell + ' 个失败');
    t('levelClearSheet', T.isSheetOpen() === true, '');
    t('awardOnce', AWARDS.length === 1, JSON.stringify(AWARDS));
    t('awardKeyZh', AWARDS[0] && AWARDS[0].k === 'wordzh', AWARDS[0] && AWARDS[0].k);
    t('awardStars3', AWARDS[0] && AWARDS[0].s === 3, AWARDS[0] && AWARDS[0].s);
    t('bestSaved', parseInt(localStorage.getItem('ksword.zh.best'), 10) >= 3,
      localStorage.getItem('ksword.zh.best'));

    // ── 9. 点错三次 → 只剩一颗星 ──
    T.start(3);
    await wait(150);
    var bad2 = findBadStart();
    for (var i = 0; i < 3; i++) if (bad2 >= 0) T.tap(bad2);
    AWARDS.length = 0;
    await clearLevel();
    t('starsDropOnWrong', AWARDS[0] && AWARDS[0].s === 1,
      (AWARDS[0] && AWARDS[0].s) + ' / wrong=' + T.wrong());

    // ── 10. 布局（重新开一关，牌面才是完整的；10 张牌应排 5 列）──
    T.start(4);
    await wait(200);
    t('lvl4Tiles10', T.cells().length === 10, T.cells().length);
    t('lvl4Cols5', T.cols() === 5, T.cols());
    var gridEl = document.getElementById('grid');
    var gr = gridEl.getBoundingClientRect();
    var tiles = document.querySelectorAll('#grid .tile');
    t('gridOnScreen', gr.top >= 0 && gr.bottom <= window.innerHeight + 1,
      gr.top.toFixed(0) + '..' + gr.bottom.toFixed(0) + ' / ' + window.innerHeight);
    t('tilesNotEmpty', tiles.length > 0, tiles.length);
    var r0 = tiles[0].getBoundingClientRect();
    var r1 = tiles[1] ? tiles[1].getBoundingClientRect() : null;
    t('tileSquare', Math.abs(r0.width - r0.height) < 2, r0.width.toFixed(0) + 'x' + r0.height.toFixed(0));
    t('tileNotTiny', r0.width > 40, r0.width.toFixed(0));
    t('tilesNoOverlap', !r1 || r1.left >= r0.right - 1, r1 ? r0.right.toFixed(0) + ' vs ' + r1.left.toFixed(0) : '');
    t('tileFontOk', parseFloat(tiles[0].style.fontSize) >= 15, tiles[0].style.fontSize);
    t('noOverflow', document.documentElement.scrollHeight <= window.innerHeight + 2,
      document.documentElement.scrollHeight + ' / ' + window.innerHeight);

    // ── 11. 绿色配色 ──
    // 语文的字牌是米白渐变（不是纯色），所以要查 backgroundImage
    var tileBg = getComputedStyle(tiles[0]).backgroundImage;
    t('tileCream', tileBg.indexOf('255, 253, 243') >= 0, tileBg.slice(0, 70));
    var bg = getComputedStyle(document.getElementById('app')).backgroundImage;
    t('bgIsGreen', bg.indexOf('30, 92, 59') >= 0, bg.slice(0, 70));
    var ink = getComputedStyle(tiles[0]).color;
    t('inkIsDeepGreen', ink === 'rgb(20, 67, 44)', ink);

    t('noError', ERRORS.length === 0, ERRORS.join(' | '));
    window.__RESULT = { results: R };
  } catch (e) {
    R.push({ name: '脚本异常', ok: false, got: String((e && e.stack) || e) });
    window.__RESULT = { results: R };
  }
})();
`;

/* ---------- 英语 ---------- */
const EN_JS = `
(async function () {
  var T = window.__we;
  ${assertBody()}
  try {
    // ── 1. 词库 ──
    t('pool3AllThree', allLen(T.words3, 3), T.words3.length + ' 个');
    t('pool4AllFour', allLen(T.words4, 4), T.words4.length + ' 个');
    t('pool5AllFive', allLen(T.words5, 5), T.words5.length + ' 个');
    t('poolSize', T.words3.length >= 40 && T.words4.length >= 40 && T.words5.length >= 25,
      T.words3.length + '/' + T.words4.length + '/' + T.words5.length);
    t('poolUnique3', uniqN(T.words3) === T.words3.length, uniqN(T.words3) + '/' + T.words3.length);
    t('poolUnique4', uniqN(T.words4) === T.words4.length, uniqN(T.words4) + '/' + T.words4.length);
    t('poolUnique5', uniqN(T.words5) === T.words5.length, uniqN(T.words5) + '/' + T.words5.length);
    t('poolLowercase', T.words3.concat(T.words4, T.words5).every(function (w) { return w === w.toLowerCase(); }), '');

    // ── 2. 出题 ──
    var w1 = T.makeWords(1);
    t('lvl1FourWords', w1.length === 4, w1.join(','));
    t('lvl1AllThree', allLen(w1, 3), w1.join(','));
    t('lvl1Unique', uniqN(w1) === 4, w1.join(','));
    var w4 = T.makeWords(4);
    t('lvl4Mix', w4.filter(function (x) { return x.length === 3; }).length === 2 &&
      w4.filter(function (x) { return x.length === 4; }).length === 2, w4.join(','));
    var w7 = T.makeWords(7);
    t('lvl7HasFive', w7.filter(function (x) { return x.length === 5; }).length === 1 &&
      w7.filter(function (x) { return x.length === 4; }).length === 2, w7.join(','));

    // ── 3. 开局牌面 ──
    T.start(1);
    await wait(150);
    t('lvl1Tiles12', T.cells().length === 12, T.cells().join(''));
    t('tilesMatchWords', sameChars(T.remain(), T.cells()), T.cells().join(''));
    t('cols4OnSmallBoard', T.cols() === 4, T.cols());

    // ── 4. 接不上的字母 ──
    var bad = findBadStart();
    if (bad >= 0) {
      var before = T.picked();
      T.tap(bad);
      t('rejectKeepsString', T.picked() === before, '点 ' + T.cells()[bad] + ' → "' + T.picked() + '"');
      t('rejectCounts', T.wrong() === 1, T.wrong());
      T.clear();
    } else {
      t('rejectKeepsString', true, 'skipped');
      t('rejectCounts', true, 'skipped');
    }

    // ── 5. 取消 ──
    var w = T.remain()[0];
    T.clear();
    var idx0 = T.tapLetter(w.charAt(0));
    t('prefixAccepted', T.picked() === w.charAt(0), T.picked());
    T.tap(idx0);
    t('cancelByTap', T.picked() === '', T.picked());

    // ── 6. 拼完一个词 ──
    T.clear();
    var okSpell = T.spell(w);
    t('spellWordOk', okSpell && T.picked() === w, T.picked());
    if (w.length >= 2) t('lineDrawn', T.lineNodes() > 0, T.lineNodes());
    await wait(700);
    t('wordRemoved', T.remain().indexOf(w) < 0, T.remain().join(','));
    t('scoreForWord', T.score() === w.length * 10, T.score());
    t('doneCounter', document.getElementById('done').textContent === '1',
      document.getElementById('done').textContent);

    // ── 7. 重选按钮 ──
    T.clear();
    T.tapLetter(T.remain()[0].charAt(0));
    document.getElementById('btnClear').click();
    t('clearBtn', T.picked() === '', T.picked());

    // ── 8. 结算 + 三颗星 ──
    T.start(2);
    await wait(150);
    AWARDS.length = 0;
    var badSpell = await clearLevel();
    t('allSpelled', badSpell === 0, badSpell + ' 个失败');
    t('levelClearSheet', T.isSheetOpen() === true, '');
    t('awardOnce', AWARDS.length === 1, JSON.stringify(AWARDS));
    t('awardKeyEn', AWARDS[0] && AWARDS[0].k === 'worden', AWARDS[0] && AWARDS[0].k);
    t('awardStars3', AWARDS[0] && AWARDS[0].s === 3, AWARDS[0] && AWARDS[0].s);
    t('bestSaved', parseInt(localStorage.getItem('ksword.en.best'), 10) >= 3,
      localStorage.getItem('ksword.en.best'));
    t('separateBestKey', localStorage.getItem('ksword.zh.best') === null,
      String(localStorage.getItem('ksword.zh.best')));

    // ── 9. 列数：16 张排 4 列（排 5 列会剩 5/5/5/1）──
    T.start(7);
    await wait(150);
    t('lvl7Tiles16', T.cells().length === 16, T.cells().length);
    t('lvl7Cols4', T.cols() === 4, T.cols());
    T.start(4);
    await wait(200);
    t('lvl4Tiles14', T.cells().length === 14, T.cells().length);
    t('lvl4Cols5', T.cols() === 5, T.cols());

    // ── 10. 布局 ──
    var gridEl = document.getElementById('grid');
    var gr = gridEl.getBoundingClientRect();
    var tiles = document.querySelectorAll('#grid .tile');
    t('gridOnScreen', gr.top >= 0 && gr.bottom <= window.innerHeight + 1,
      gr.top.toFixed(0) + '..' + gr.bottom.toFixed(0) + ' / ' + window.innerHeight);
    var r0 = tiles[0].getBoundingClientRect();
    var r1 = tiles[1] ? tiles[1].getBoundingClientRect() : null;
    t('tileSquare', Math.abs(r0.width - r0.height) < 2, r0.width.toFixed(0) + 'x' + r0.height.toFixed(0));
    t('tileNotTiny', r0.width > 40, r0.width.toFixed(0));
    t('tilesNoOverlap', !r1 || r1.left >= r0.right - 1, r1 ? r0.right.toFixed(0) + ' vs ' + r1.left.toFixed(0) : '');
    t('tileFontOk', parseFloat(tiles[0].style.fontSize) >= 15, tiles[0].style.fontSize);
    t('noOverflow', document.documentElement.scrollHeight <= window.innerHeight + 2,
      document.documentElement.scrollHeight + ' / ' + window.innerHeight);

    // ── 11. 概念图配色：白底 + 彩色字块 ──
    t('tileWhite', getComputedStyle(tiles[0]).backgroundColor === 'rgb(255, 255, 255)',
      getComputedStyle(tiles[0]).backgroundColor);
    var bg = getComputedStyle(document.getElementById('app')).backgroundImage;
    t('bgIsLight', bg.indexOf('255, 255, 255') >= 0, bg.slice(0, 70));
    var colors = {};
    for (var k = 0; k < tiles.length; k++) colors[getComputedStyle(tiles[k]).color] = 1;
    t('tilesColorful', Object.keys(colors).length >= 5, Object.keys(colors).join(' '));

    t('noError', ERRORS.length === 0, ERRORS.join(' | '));
    window.__RESULT = { results: R };
  } catch (e) {
    R.push({ name: '脚本异常', ok: false, got: String((e && e.stack) || e) });
    window.__RESULT = { results: R };
  }
})();
`;

/* =========================================================
 * 无头 Chrome 驱动
 * ========================================================= */
async function cdpConnect(port) {
  const t0 = Date.now();
  let wsUrl = null;
  while (Date.now() - t0 < 15000) {
    try {
      const list = await (await fetch('http://127.0.0.1:' + port + '/json/list')).json();
      const p = list.find((x) => x.type === 'page');
      if (p) { wsUrl = p.webSocketDebuggerUrl; break; }
    } catch (e) {}
    await sleep(200);
  }
  if (!wsUrl) throw new Error('CDP 连不上（端口 ' + port + '）');
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

async function runGame(title, dir, hook, port, assertJs) {
  const game = path.join(ROOT, dir, 'index.html');
  const tmp = path.join(ROOT, dir, '_assert.html');
  const src = fs.readFileSync(game, 'utf8');
  if (src.indexOf(hook) < 0) throw new Error(dir + ' 没有测试钩子 ' + hook);
  fs.writeFileSync(tmp, src.replace('</body>',
    '<script>setTimeout(function(){' + assertJs + '}, 250);</script></body>'), 'utf8');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'word-'));
  const chrome = spawn(CHROME, [
    '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--hide-scrollbars', '--allow-file-access-from-files',
    '--remote-debugging-port=' + port, '--remote-allow-origins=*',
    '--window-size=430,900', '--user-data-dir=' + profile,
    'file:///' + tmp.replace(/\\/g, '/'),
  ], { stdio: 'ignore' });

  const kill = () => {
    try { spawn('taskkill', ['/PID', String(chrome.pid), '/T', '/F'], { stdio: 'ignore' }); } catch (e) {}
    try { chrome.kill(); } catch (e) {}
  };

  let payload = null;
  try {
    const cdp = await cdpConnect(port);
    // 用例里有大量 await，留够时间（约 25 秒预算）
    for (let i = 0; i < 160 && !payload; i++) {
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
    try { fs.unlinkSync(tmp); } catch (e) {}
  }

  console.log('\n' + title + '\n');
  if (!payload) { console.log('  ✗ 页面没有回报结果'); return 1; }
  let pass = 0;
  const fails = [];
  payload.results.forEach((r) => { if (r.ok) pass++; else fails.push(r); });
  fails.forEach((r) => console.log('  ✗ ' + r.name + '  ' + r.got));
  console.log('  ' + pass + '/' + payload.results.length + ' 通过');
  return fails.length;
}

async function main() {
  if (!CHROME) throw new Error('找不到 Chrome');
  let bad = 0;
  bad += await runGame('词语组合（语文）· 全量断言', 'wordzh', 'window.__wz', 9241, ZH_JS);
  bad += await runGame('单词组合（英语）· 全量断言', 'worden', 'window.__we', 9242, EN_JS);
  console.log('');
  process.exit(bad ? 1 : 0);
}

main().catch((e) => { console.error(e.message); process.exit(1); });
