'use strict';
/*
 * 老鼠吃奶酪 · 全量断言
 *
 * 原版（mice_eat_cheese_android-master，Qt + C++）的规则是从 map.h / game.h / maincall.cpp 读出来的，
 * 这里逐条钉住，重点是三条原本没有、但不加就没法玩的保证：
 *   1. 所有奶酪都从起点可达（原版随机撒猫不管连通性，经常把奶酪围死）
 *   2. 起点 2 格内没有猫和陷阱（原版掉命回起点，四周可能就是猫，能连掉到死）
 *   3. 猫和陷阱的数量是「按关卡配的固定值」，不是原版那种「非路径格一半」
 *
 * 用法：node Harmony/tools/test-mouse.js
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
const PORT = 9251;

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

/* =========================================================
 * 注入页面的断言体
 * ========================================================= */
const BODY = `
(async function () {
  var T = window.__mc;
  var R = [], ERRORS = [], AWARDS = [];
  window.addEventListener('error', function (e) { ERRORS.push(String(e.message)); });
  window.KidsSafe = { award: function (k, s, sc, sub) { AWARDS.push({ k: k, s: s, sc: sc, sub: sub }); } };
  function wait(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }
  function t(name, cond, got) {
    R.push({ name: name, ok: !!cond, got: got === undefined || got === null ? '' : String(got) });
  }
  var ST = T.state;

  try {
    /* ─────────────────────────────────────────────
     * 一、地图生成
     * ───────────────────────────────────────────── */
    var g1 = T.gen(T.cfgOf(1));
    t('gen1Ok', !!g1, g1 ? '' : 'null');
    t('gen1Size13', g1 && g1.N === 13, g1 && g1.N);
    t('gen1GridShape', g1 && g1.grid.length === 13 && g1.grid[0].length === 13,
      g1 && g1.grid.length + 'x' + g1.grid[0].length);

    // 起点必须能站人
    t('startOpen', g1 && g1.grid[g1.sy][g1.sx] === 1, g1 && g1.grid[g1.sy][g1.sx]);

    // 起点 3×3 全挖开：原版起点四周可能是猫，掉命回起点又撞
    var areaBad = 0;
    if (g1) {
      for (var dy = -1; dy <= 1; dy++) {
        for (var dx = -1; dx <= 1; dx++) {
          var gx = g1.sx + dx, gy = g1.sy + dy;
          if (gx < 0 || gy < 0 || gx >= g1.N || gy >= g1.N) continue;
          if (g1.grid[gy][gx] === 0) areaBad++;
        }
      }
    }
    t('startAreaNoWall', areaBad === 0, areaBad + ' 面墙');

    // 起点 2 格内不许有猫/陷阱
    var nearDanger = 0;
    if (g1) {
      for (var y = 0; y < g1.N; y++) {
        for (var x = 0; x < g1.N; x++) {
          var v = g1.grid[y][x];
          if (v !== 3 && v !== 4) continue;
          if (Math.max(Math.abs(x - g1.sx), Math.abs(y - g1.sy)) <= 2) nearDanger++;
        }
      }
    }
    t('startSafeRadius2', nearDanger === 0, nearDanger + ' 只');

    // 奶酪数 == 配置数（关掉 extraCheeseProb 之后是严格相等）
    var cnt = function (grid, N, v) {
      var n = 0;
      for (var y = 0; y < N; y++) for (var x = 0; x < N; x++) if (grid[y][x] === v) n++;
      return n;
    };
    t('cheeseEqualsCfg', g1 && cnt(g1.grid, g1.N, 2) === 4, g1 && cnt(g1.grid, g1.N, 2));

    // 猫和陷阱不超过配置（BFS 撤回时可能少放）
    t('catsWithinCfg', g1 && g1.cats <= 7, g1 && g1.cats);
    t('trapsWithinCfg', g1 && g1.traps <= 3, g1 && g1.traps);

    // 不能全是空地，得有墙
    t('hasWalls', g1 && cnt(g1.grid, g1.N, 0) > 20, g1 && cnt(g1.grid, g1.N, 0));

    /* ─────────────────────────────────────────────
     * 二、连通性：这是原版最大的坑
     * ───────────────────────────────────────────── */
    T.start(1);
    await wait(120);
    var rc = T.cheesesReachable();
    t('allCheeseReachable', rc.reachable === rc.total && rc.total > 0,
      rc.reachable + '/' + rc.total);

    // 连生 30 张，全部要可达 —— 漏一张就说明 BFS 撤回有洞
    var badGen = 0, badReach = 0, sizes = {};
    for (var k = 0; k < 30; k++) {
      var lv = 1 + (k % 5);
      var cc = T.cfgOf(lv);
      var gg = T.gen(cc);
      if (!gg) { badGen++; continue; }
      sizes[gg.N] = (sizes[gg.N] || 0) + 1;
      // 自己算一遍可达（不依赖游戏内的 reachable）
      var seen = {}, q = [[gg.sx, gg.sy]];
      seen[gg.sy + ',' + gg.sx] = 1;
      while (q.length) {
        var c = q.shift();
        var dd = [[0,-1],[0,1],[-1,0],[1,0]];
        for (var i2 = 0; i2 < 4; i2++) {
          var nx = c[0] + dd[i2][0], ny = c[1] + dd[i2][1];
          if (nx < 0 || ny < 0 || nx >= gg.N || ny >= gg.N) continue;
          if (seen[ny + ',' + nx]) continue;
          var vv = gg.grid[ny][nx];
          if (vv === 0 || vv === 3 || vv === 4) continue;
          seen[ny + ',' + nx] = 1;
          q.push([nx, ny]);
        }
      }
      var miss = 0;
      for (var y2 = 0; y2 < gg.N; y2++) {
        for (var x2 = 0; x2 < gg.N; x2++) {
          if (gg.grid[y2][x2] === 2 && !seen[y2 + ',' + x2]) miss++;
        }
      }
      if (miss) badReach++;
    }
    t('gen30NeverNull', badGen === 0, badGen + ' 次失败');
    t('gen30AllReachable', badReach === 0, badReach + ' 张堵死');
    t('gen30SizeMix', Object.keys(sizes).length >= 2, JSON.stringify(sizes));

    /* ─────────────────────────────────────────────
     * 三、移动：撞墙不罚
     * ───────────────────────────────────────────── */
    T.start(1);
    await wait(100);
    T.set(1, 1, 'open');
    T.set(2, 1, 'wall');
    T.teleport(1, 1);
    var life0 = ST().life;
    var rWall = T.move('right');
    var pWall = T.pos();
    t('wallBlocks', rWall === 'wall' && pWall[0] === 1 && pWall[1] === 1, rWall + ' @' + pWall);
    t('wallNoDamage', ST().life === life0, ST().life + '/' + life0);

    // 出界也算撞墙
    T.teleport(0, 0);
    T.set(0, 0, 'open');
    var rEdge = T.move('left');
    t('edgeBlocks', rEdge === 'wall' && T.pos()[0] === 0, rEdge);

    // 走到空地：只挪一格
    T.set(1, 1, 'open');
    T.set(2, 1, 'open');
    T.teleport(1, 1);
    var rMove = T.move('right');
    t('stepOneCell', rMove === 'move' && T.pos()[0] === 2 && T.pos()[1] === 1,
      rMove + ' @' + T.pos());

    /* ─────────────────────────────────────────────
     * 四、撞猫 / 撞陷阱：掉命 + 回起点
     * ───────────────────────────────────────────── */
    T.start(1);
    await wait(100);
    T.clearInvuln();
    T.set(1, 1, 'open');
    T.set(2, 1, 'cat');
    T.teleport(1, 1);
    var lp0 = ST().life, h0 = ST().hurt;
    var rCat = T.move('right');
    var s1 = ST();
    t('catHurts', rCat === 'cat' && s1.life === lp0 - 1, rCat + ' life ' + s1.life + '/' + lp0);
    t('catSendsHome', s1.x === s1.sx && s1.y === s1.sy, s1.x + ',' + s1.y);
    t('catCountsHurt', s1.hurt === h0 + 1, s1.hurt);

    // 无敌窗口内再撞同一只猫，不掉命
    // 注意：撞猫会被弹回起点，所以每次都要先 teleport 回 (1,1)，否则测的是「从起点随便走一步」
    var lp1 = ST().life;
    t('invulnOn', T.invuln() === true, T.invuln());
    T.teleport(1, 1);
    var rAgain = T.move('right');
    t('invulnGuards', rAgain === 'guarded' && ST().life === lp1, rAgain + ' life ' + ST().life);

    // 无敌结束后恢复扣命
    T.clearInvuln();
    T.teleport(1, 1);
    var lp2 = ST().life;
    var rAfter = T.move('right');
    t('invulnExpires', rAfter === 'cat' && ST().life === lp2 - 1, rAfter + ' life ' + ST().life);

    // 陷阱同理
    T.start(1);
    await wait(100);
    T.clearInvuln();
    T.set(1, 1, 'open');
    T.set(2, 1, 'trap');
    T.teleport(1, 1);
    var lp3 = ST().life;
    var rTrap = T.move('right');
    t('trapHurts', rTrap === 'trap' && ST().life === lp3 - 1, rTrap + ' life ' + ST().life);
    t('trapSendsHome', T.pos()[0] === ST().sx && T.pos()[1] === ST().sy, T.pos());

    /* ─────────────────────────────────────────────
     * 五、吃奶酪
     * ───────────────────────────────────────────── */
    T.start(1);
    await wait(100);
    T.clearInvuln();
    T.set(1, 1, 'open');
    T.set(2, 1, 'cheese');
    T.teleport(1, 1);
    var got0 = ST().got, sc0 = ST().score;
    var rCheese = T.move('right');
    var s2 = ST();
    t('cheeseCollects', rCheese === 'cheese' && s2.got === got0 + 1, rCheese + ' ' + s2.got);
    t('cheeseScores', s2.score === sc0 + 10, s2.score + ' / ' + sc0);
    t('cheeseBecomesOpen', T.map()[1][2] === 1, T.map()[1][2]);
    t('cheeseKeepsPos', s2.x === 2 && s2.y === 1, s2.x + ',' + s2.y);

    /* ─────────────────────────────────────────────
     * 六、结束条件
     * ───────────────────────────────────────────── */
    // 吃满 -> 过关弹窗 + 上报
    T.start(1);
    await wait(100);
    var total = ST().total;
    T.set(1, 1, 'open');
    T.teleport(1, 1);
    // 把老鼠周围摆成一排奶酪，逐个吃过去
    for (var i3 = 2; i3 <= total + 1; i3++) T.set(i3, 1, 'cheese');
    AWARDS.length = 0;
    for (var j3 = 0; j3 < total; j3++) { T.clearInvuln(); T.move('right'); }
    var s3 = ST();
    t('allCheeseWins', s3.over === true && s3.win === true && s3.got === total,
      'over=' + s3.over + ' win=' + s3.win + ' got=' + s3.got + '/' + total);
    t('winSheetOpen', T.isSheetOpen() === true, T.isSheetOpen());
    t('awardKey', AWARDS.length === 1 && AWARDS[0].k === 'mouse',
      AWARDS.length + ' ' + (AWARDS[0] && AWARDS[0].k));
    // 一次没被撞 -> 3 星
    t('awardStars3', AWARDS[0] && AWARDS[0].s === 3, AWARDS[0] && AWARDS[0].s);
    t('awardLevel', AWARDS[0] && AWARDS[0].sc === 1, AWARDS[0] && AWARDS[0].sc);

    // 命归零 -> 失败弹窗，不上报
    T.start(1);
    await wait(100);
    var maxL = ST().maxLife;
    T.set(1, 1, 'open');
    T.set(2, 1, 'cat');
    T.teleport(1, 1);
    AWARDS.length = 0;
    for (var m3 = 0; m3 < maxL + 2; m3++) { T.teleport(1, 1); T.clearInvuln(); T.move('right'); }
    var s4 = ST();
    t('lifeZeroLoses', s4.life === 0 && s4.over === true && s4.win === false,
      'life=' + s4.life + ' over=' + s4.over);
    t('loseNoAward', AWARDS.length === 0, AWARDS.length);
    t('loseSheetOpen', T.isSheetOpen() === true, T.isSheetOpen());

    /* ─────────────────────────────────────────────
     * 七、星星分级：按受伤次数
     * ───────────────────────────────────────────── */
    function starsWithHurt(hurtN) {
      T.start(1);
      var tt = ST().total;
      // 受伤走「下」：起点下面摆一只猫；吃奶酪走「右」：一路摆过去。两条路不打架。
      T.set(1, 1, 'open');
      T.set(1, 2, 'cat');
      for (var h = 0; h < hurtN; h++) {
        T.teleport(1, 1);
        T.clearInvuln();
        T.move('down');
      }
      for (var q2 = 0; q2 < tt; q2++) {
        T.set(2, 1, 'cheese');
        T.teleport(1, 1);
        T.clearInvuln();
        T.move('right');
      }
      return AWARDS.length ? AWARDS[AWARDS.length - 1].s : -1;
    }
    AWARDS.length = 0;
    starsWithHurt(1);
    var s1star = AWARDS.length ? AWARDS[AWARDS.length - 1].s : -1;
    t('awardStars2', s1star === 2, s1star);
    AWARDS.length = 0;
    starsWithHurt(3);
    var s2star = AWARDS.length ? AWARDS[AWARDS.length - 1].s : -1;
    t('awardStars1', s2star === 1, s2star);

    /* ─────────────────────────────────────────────
     * 八、难度曲线
     * ───────────────────────────────────────────── */
    var c1 = T.cfgOf(1), c3 = T.cfgOf(3), c5 = T.cfgOf(5), c9 = T.cfgOf(9), c30 = T.cfgOf(30);
    t('cfgGrows', c5.cats > c1.cats, c1.cats + ' -> ' + c5.cats);
    t('cfgLifeDrops', c1.life > c5.life, c1.life + ' -> ' + c5.life);
    t('cfgSizeGrows', c1.size < c3.size && c3.size <= c5.size, c1.size + '/' + c3.size + '/' + c5.size);
    t('cfgSizeCapped', c30.size === 17 && c9.size === 17, c9.size + '/' + c30.size);
    t('cfgCatCapped', c30.cats >= c9.cats, c9.cats + ' -> ' + c30.cats);

    /* ─────────────────────────────────────────────
     * 九、布局
     * ───────────────────────────────────────────── */
    T.start(3);
    await wait(200);
    var cell = T.cell(), size = T.canvasSize();
    t('cellBigEnough', cell >= 20, cell + 'vp');
    t('canvasSquare', size[0] === size[1], size.join('x'));
    t('canvasIsGridMultiple', size[0] % cell === 0, size[0] + ' % ' + cell);
    var cvEl = document.getElementById('cv');
    var box = cvEl.getBoundingClientRect();
    t('canvasFitsViewport', box.right <= innerWidth + 1 && box.bottom <= innerHeight + 1,
      Math.round(box.right) + ',' + Math.round(box.bottom) + ' / ' + innerWidth + ',' + innerHeight);
    t('noScroll', document.documentElement.scrollHeight <= innerHeight + 1,
      document.documentElement.scrollHeight + ' / ' + innerHeight);

    // 方向键四个都在，且不互相重叠
    var pads = document.querySelectorAll('#pad button');
    t('padFourButtons', pads.length === 4, pads.length);
    var rects = [].map.call(pads, function (b) { return b.getBoundingClientRect(); });
    var overlap = 0;
    for (var a4 = 0; a4 < rects.length; a4++) {
      for (var b4 = a4 + 1; b4 < rects.length; b4++) {
        var A = rects[a4], B = rects[b4];
        if (A.left < B.right - 0.5 && B.left < A.right - 0.5 &&
            A.top < B.bottom - 0.5 && B.top < A.bottom - 0.5) overlap++;
      }
    }
    t('padNoOverlap', overlap === 0, overlap + ' 对重叠');

    /* ─────────────────────────────────────────────
     * 十、存档
     * ───────────────────────────────────────────── */
    T.resetBest();
    t('bestDefault1', T.best() === 1, T.best());
    // 打第 1 关通关后 best 应该是 2
    T.start(1);
    var tot = ST().total;
    T.set(1, 1, 'open');
    T.set(2, 1, 'open');
    T.teleport(1, 1);
    for (var z = 0; z < tot; z++) {
      T.set(2, 1, 'cheese');
      T.teleport(1, 1);
      T.clearInvuln();
      T.move('right');
    }
    var raw = localStorage.getItem('ksmouse.best');
    t('bestSaved', parseInt(raw, 10) === 2, raw);

    /* ─────────────────────────────────────────────
     * 十一、无报错
     * ───────────────────────────────────────────── */
    t('noRuntimeErrors', ERRORS.length === 0, ERRORS.join(' | '));

  } catch (e) {
    R.push({ name: 'EXCEPTION', ok: false, got: (e && e.message || String(e)) + ' @ ' + (e && e.stack || '').split('\\n')[1] });
  }

  window.__RESULT = { results: R };
})();
`;

/* =========================================================
 * CDP 驱动
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

async function main() {
  if (!CHROME) throw new Error('找不到 Chrome');

  const game = path.join(ROOT, 'mouse', 'index.html');
  const tmp = path.join(ROOT, 'mouse', '_assert.html');
  const src = fs.readFileSync(game, 'utf8');
  if (src.indexOf('window.__mc') < 0) throw new Error('mouse/index.html 没有测试钩子 window.__mc');
  fs.writeFileSync(tmp, src.replace('</body>',
    '<script>setTimeout(function(){' + BODY + '}, 250);</script></body>'), 'utf8');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'mouse-'));
  const chrome = spawn(CHROME, [
    '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--hide-scrollbars', '--allow-file-access-from-files',
    '--remote-debugging-port=' + PORT, '--remote-allow-origins=*',
    '--window-size=430,900', '--user-data-dir=' + profile,
    'file:///' + tmp.replace(/\\/g, '/'),
  ], { stdio: 'ignore' });

  const kill = () => {
    try { spawn('taskkill', ['/PID', String(chrome.pid), '/T', '/F'], { stdio: 'ignore' }); } catch (e) {}
    try { chrome.kill(); } catch (e) {}
  };

  let payload = null;
  try {
    const cdp = await cdpConnect(PORT);
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

  console.log('\n老鼠吃奶酪 · 全量断言\n');
  if (!payload) { console.log('  ✗ 页面没有回报结果'); process.exit(1); }
  let pass = 0;
  const fails = [];
  payload.results.forEach((r) => { if (r.ok) pass++; else fails.push(r); });
  fails.forEach((r) => console.log('  ✗ ' + r.name + '  ' + r.got));
  console.log('  ' + pass + '/' + payload.results.length + ' 通过\n');
  process.exit(fails.length ? 1 : 0);
}

main().catch((e) => { console.error(e.message); process.exit(1); });
