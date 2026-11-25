'use strict';
/*
 * 成语大挑战 · 全量断言
 *
 * 在无头 Chrome 里跑真实 index.html，用页面自己的 __cy 钩子：
 * 逐题验证「10 个字里一定能拼出答案」这条不变量，再把 答对 / 答错 / 提示 / 跳过 /
 * 存档 / 通关 走一遍。
 *
 * 不变量比快照重要：题库是脚本生成的，字段拼错、干扰字把答案字挤掉、
 * 简体化把两个字并成一个 —— 这些问题只会在某一题上出现，肉眼翻 420 条翻不出来。
 *
 * 用法：node Harmony/tools/test-chengyu.js
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

const GAME = path.join(__dirname, '..', 'entry/src/main/resources/rawfile/playland/chengyu/index.html');
const TMP = path.join(path.dirname(GAME), '_assert.html');
const PROBE_PORT = 8095;
const CDP_PORT = 9225;

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

const ASSERT_JS = `
(async function () {
  // 第二次打开页面（?p2）只是为了验「存档能不能接着做」，
  // 这时候再跑一遍断言会把刚存的进度 reset 掉。
  if (location.search.indexOf('p2') >= 0) return;
  var C = window.__cy;
  var R = [];
  var ERRORS = [];
  window.addEventListener('error', function (e) { ERRORS.push(String(e.message)); });
  function wait(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }
  function t(name, cond, got) {
    R.push({ name: name, ok: !!cond, got: got === undefined || got === null ? '' : String(got) });
  }

  try {
    // ── 1. 题库不变量（420 条全查）──
    var D = C.data;
    t('count420', D.length === 420, D.length);

    var badLen = 0, badPool = 0, badWord = 0, emptyTip = 0, badImg = 0, dupAnswer = 0;
    var noImg = [];
    D.forEach(function (r, k) {
      if (String(r.a).length !== 4 || String(r.w).length !== 6) badLen++;
      // 候选池 = 答案 + 干扰字，答案的每个字都必须能在里面找到「一块」对应的字块
      var pool = String(r.a) + String(r.w);
      var used = {};
      var ok = true;
      for (var i = 0; i < r.a.length; i++) {
        var ch = r.a.charAt(i), hit = -1;
        for (var j = 0; j < pool.length; j++) {
          if (pool.charAt(j) === ch && !used[j]) { hit = j; break; }
        }
        if (hit < 0) { ok = false; break; }
        used[hit] = 1;
      }
      if (!ok) badPool++;
      // 干扰字不能和答案字撞车，否则同一个字会有两块，答案就不唯一了
      var wa = String(r.w).split(''), aa = String(r.a);
      if (wa.some(function (c) { return aa.indexOf(c) >= 0; })) badWord++;
      if (!String(r.t || '').trim()) emptyTip++;
      if (!/^\\d+$/.test(String(r.i))) badImg++;
      if (noImg.indexOf(r.i) < 0) noImg.push(r.i);
    });
    t('answerLen', badLen === 0, badLen + ' 条长度不对');
    t('poolBuildable', badPool === 0, badPool + ' 条凑不出答案');
    t('noWordCollision', badWord === 0, badWord + ' 条干扰字撞答案');
    t('allHaveTip', emptyTip === 0, emptyTip + ' 条没释义');
    t('allHaveImg', badImg === 0, badImg + ' 条图片编号异常');
    t('uniqueImgs', noImg.length === D.length, noImg.length + ' 张不同插图');

    var seen = {}, dups = [];
    D.forEach(function (r) { if (seen[r.a]) dups.push(r.a); seen[r.a] = 1; });
    dupAnswer = dups.length;
    t('noDuplicateIdiom', dupAnswer <= 4, dups.join(',') || '无');

    // ── 2. 首页 ──
    C.reset();
    t('homeState', C.state === 'home', C.state);
    t('homeVisible', document.getElementById('home').classList.contains('on'), '');

    // ── 3. 开始 → 出题 ──
    C.start();
    t('playState', C.state === 'play', C.state);
    t('idx0', C.idx === 0, C.idx);
    var img = document.getElementById('qImg');
    t('imgSet', /img\\/\\d+\\.webp$/.test(img.getAttribute('src')), img.getAttribute('src'));
    if (!img.complete || !img.naturalWidth) {
      await new Promise(function (r) {
        var done = false;
        var fin = function () { if (!done) { done = true; r(); } };
        img.addEventListener('load', fin);
        img.addEventListener('error', fin);
        setTimeout(fin, 2500);
      });
    }
    t('imgLoaded', img.complete && img.naturalWidth > 0, img.naturalWidth + 'x' + img.naturalHeight);
    t('tenTiles', document.querySelectorAll('#tiles .tile').length === 10,
      document.querySelectorAll('#tiles .tile').length);
    t('fourSlots', document.querySelectorAll('#slots .slot').length === 4,
      document.querySelectorAll('#slots .slot').length);
    // 字块上显示的字，应当正好是答案 4 字 + 干扰 6 字
    var shown = [].map.call(document.querySelectorAll('#tiles .tile'), function (b) { return b.textContent; });
    t('tilesMatchPool', shown.slice().sort().join('') === (C.q.a + C.q.w).split('').sort().join(''),
      shown.join(''));

    // ── 4. 填字 / 撤字 ──
    C.pickChar(C.q.a.charAt(0));
    C.pickChar(C.q.a.charAt(1));
    t('twoPicked', C.picked === C.q.a.slice(0, 2), C.picked);
    t('slotFilled2', document.querySelectorAll('#slots .slot.filled').length === 2,
      document.querySelectorAll('#slots .slot.filled').length);
    C.clearSlot(0);
    t('clearReindex', C.picked === C.q.a.charAt(1), C.picked);

    // ── 5. 答错 → 清空 → 提示 ──
    C.goto(0);
    var q0 = C.q;
    C.answerWrong();
    await wait(700);
    t('wrongCleared', C.picked === '', C.picked);
    t('wrongCounted', C.wrong >= 1, C.wrong);
    t('noHintYet', C.hintShown === false, C.hintShown);
    C.answerWrong();
    await wait(700);
    t('hintAfter2', C.hintShown === true, C.hintShown);
    t('hintText', document.getElementById('hintLine').textContent === q0.t,
      document.getElementById('hintLine').textContent.slice(0, 12));

    // ── 6. 答对 → 卡片 → 下一题 ──
    C.goto(2);
    var q2 = C.q;
    var rightBefore = C.right;
    C.answerRight();
    await wait(900);
    t('cardState', C.state === 'card', C.state);
    t('cardVisible', document.getElementById('card').classList.contains('on'), '');
    t('cardIdiom', document.getElementById('cardIdiom').textContent === q2.a,
      document.getElementById('cardIdiom').textContent);
    t('cardMean', document.getElementById('cardMean').textContent === q2.t,
      document.getElementById('cardMean').textContent);
    t('rightIncremented', C.right === rightBefore + 1, rightBefore + ' → ' + C.right);
    document.getElementById('btnNext').click();
    await wait(200);
    t('advanced', C.idx === 3, C.idx);
    t('nextQuestionLoaded', C.q.a === C.data[3].a, C.q.a);
    t('slotsReset', document.querySelectorAll('#slots .slot.filled').length === 0,
      document.querySelectorAll('#slots .slot.filled').length);

    // ── 7. 跳过 ──
    var skipBefore = C.skipCount;
    C.skip();
    await wait(150);
    t('skipAdvanced', C.idx === 4, C.idx);
    t('skipCounted', C.skipCount === skipBefore + 1, C.skipCount);

    // ── 8. 存档 ──
    var raw = localStorage.getItem('cy.v1');
    t('persist', !!raw && /"idx"/.test(raw), raw ? raw.slice(0, 60) : 'null');
    var parsed = null;
    try { parsed = JSON.parse(raw); } catch (e) {}
    t('persistIdx', !!parsed && parsed.idx === C.idx, parsed ? parsed.idx : 'parse fail');

    // ── 10. 走到最后一题 → 通关页 ──
    C.goto(419);
    t('lastQuestion', C.idx === 419, C.idx);
    C.answerRight();
    await wait(900);
    document.getElementById('btnNext').click();
    await wait(300);
    t('doneState', C.state === 'done', C.state);
    t('doneVisible', document.getElementById('done').classList.contains('on'), '');
    t('doneStat', /答对/.test(document.getElementById('doneStat').textContent),
      document.getElementById('doneStat').textContent.replace(/\\s+/g, ' ').slice(0, 40));

    // ── 11. 再玩一次回到第一题 ──
    document.getElementById('btnAgain').click();
    await wait(200);
    t('againIdx0', C.idx === 0, C.idx);
    t('againRight0', C.right === 0, C.right);
    t('againPlaying', C.state === 'play', C.state);

    // ── 12. 布局 ──
    var tiles = document.querySelectorAll('#tiles .tile');
    var r0 = tiles[0].getBoundingClientRect();
    t('tileBounded', r0.width > 30 && r0.height > 30 && r0.height < 130,
      r0.width.toFixed(0) + 'x' + r0.height.toFixed(0));
    var play = document.getElementById('play').getBoundingClientRect();
    t('playFitsViewport', play.height <= window.innerHeight && play.width <= window.innerWidth,
      play.width.toFixed(0) + 'x' + play.height.toFixed(0) + ' in ' +
      window.innerWidth + 'x' + window.innerHeight);
    var ops = document.querySelector('.ops').getBoundingClientRect();
    t('opsVisible', ops.top < window.innerHeight && ops.bottom <= window.innerHeight + 1,
      ops.top.toFixed(0) + '..' + ops.bottom.toFixed(0) + ' / ' + window.innerHeight);

    // ── 13. 把进度停在第 5 题，交给外面的 reload 去验存档 ──
    // 必须放在最后：「通关 → 再玩一次」那两步会把存档清成 0。
    var beforeReload = 0;
    C.goto(4);
    await wait(150);
    beforeReload = C.idx;
    t('savedIdx4', JSON.parse(localStorage.getItem('cy.v1')).idx === 4,
      localStorage.getItem('cy.v1'));

    t('noError', ERRORS.length === 0, ERRORS.join(' | '));
    window.__RESULT = { results: R, beforeReload: beforeReload };
  } catch (e) {
    R.push({ name: '脚本异常', ok: false, got: String(e && e.stack || e) });
    window.__RESULT = { results: R, beforeReload: -1 };
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
  if (src.indexOf('window.__cy') < 0) throw new Error('游戏没有测试钩子');
  // 断言脚本自己跑完把结果放进 window.__RESULT，外面轮询去取。
  // 不用 Runtime.evaluate 的 awaitPromise 收返回值 —— 实测这条路上拿到的总是
  // undefined，而轮询对「脚本跑多久」完全不敏感，反而更稳。
  fs.writeFileSync(TMP, src.replace('</body>',
    '<script>setTimeout(function(){' + ASSERT_JS + '}, 300);</script></body>'), 'utf8');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'cyt-'));
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
  let beforeReload = -1;
  try {
    const cdp = await cdpConnect();
    // 断言里有两处 700ms 的等待和一串 DOM 操作，8 秒足够；超时就当失败
    for (let i = 0; i < 80 && !payload; i++) {
      await sleep(200);
      const r = await cdp.send('Runtime.evaluate', {
        expression: 'window.__RESULT ? JSON.stringify(window.__RESULT) : ""',
        returnByValue: true,
      });
      const v = r.result && r.result.result && r.result.result.value;
      if (v) {
        payload = JSON.parse(v);
        beforeReload = payload.beforeReload;
      }
    }

    // 真正 reload 一次，验证「接着上次做」不是靠内存里的变量
    if (beforeReload >= 0) {
      await cdp.send('Page.enable');
      await cdp.send('Page.navigate', { url: 'file:///' + TMP.replace(/\\/g, '/') + '?p2' });
      await sleep(1800);
      const r2 = await cdp.send('Runtime.evaluate', {
        expression: 'JSON.stringify({idx: window.__cy.idx, state: window.__cy.state, ' +
          'prog: document.getElementById("homeProg").textContent, ' +
          'btn: document.getElementById("btnStart").textContent, ' +
          'store: localStorage.getItem("cy.v1"), href: location.href.slice(-24)})',
        returnByValue: true,
      });
      const v = JSON.parse(r2.result.result.value);
      payload.results.push({
        name: 'reloadResumes',
        ok: v.idx === beforeReload && v.prog.indexOf(String(beforeReload + 1)) >= 0,
        got: 'idx=' + v.idx + ' 期望 ' + beforeReload + ' / ' + v.prog +
          ' / store=' + String(v.store).slice(0, 44) + ' / ' + v.href,
      });
      payload.results.push({
        name: 'reloadShowsContinue',
        ok: v.btn === '继续挑战',
        got: v.btn,
      });
    }
    cdp.close();
  } finally {
    kill();
    try { fs.unlinkSync(TMP); } catch (e) {}
  }

  console.log('\n成语大挑战 · 全量断言\n');
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
