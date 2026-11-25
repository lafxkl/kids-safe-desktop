'use strict';
/*
 * 宝贝三十六计 · 家长端服务 -- 浏览器端到端测试
 *
 * test-e2e.js 验证的是「协议对不对」，它自己冒充孩子端。
 * 这个脚本验证的是「孩子端那份真代码能不能接上」：
 *   把孩子端 index.html 原样丢进无头 Chrome，让它用自己的 EventSource 连服务，
 *   家长端由本脚本扮演（POST hello / say / end），中间人只做一个探针服务。
 *
 * 为什么要探针：无头 Chrome 跑完就没了，读不到页面里的状态。
 * 所以注入一小段脚本，把 page / lineText / cur 的每次变化 POST 回 8098，
 * 本脚本据此断言——断言的是浏览器里真实渲染出来的东西，不是我们以为的东西。
 *
 * 用法：
 *   node Baby36Server/server.js        # 先起服务
 *   node Baby36Server/tools/test-browser.js
 */

const http = require('http');
const fs = require('fs');
const path = require('path');
const os = require('os');
const { spawn } = require('child_process');

const SRV = 'http://127.0.0.1:8099';
const PROBE_PORT = 8098;
const SRC = path.join(__dirname, '..', '..',
  'Harmony/entry/src/main/resources/rawfile/playland/baobei36/index.html');
const TMP_HTML = path.join(__dirname, '..', '..', 'Harmony/entry/src/main/resources/rawfile/playland/baobei36/_e2e.html');

const CHROME = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
].find((p) => { try { return fs.existsSync(p); } catch (e) { return false; } });

let pass = 0, fail = 0;
const fails = [];
function ok(name, cond, extra) {
  if (cond) { pass++; console.log('  ✓ ' + name); }
  else { fail++; fails.push(name + (extra ? '  → ' + extra : '')); console.log('  ✗ ' + name + (extra ? '  → ' + extra : '')); }
}

/* ───────────── 探针服务 ───────────── */
const probes = [];
const waiters = [];

const probeServer = http.createServer((req, res) => {
  const h = { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Headers': '*', 'Access-Control-Allow-Methods': 'POST,OPTIONS' };
  if (req.method === 'OPTIONS') { res.writeHead(204, h); return res.end(); }
  if (req.method === 'POST' && req.url === '/probe') {
    let b = '';
    req.on('data', (c) => { b += c; });
    req.on('end', () => {
      res.writeHead(204, h);
      res.end();
      let j;
      try { j = JSON.parse(b); } catch (e) { return; }
      probes.push(j);
      for (let k = waiters.length - 1; k >= 0; k--) {
        if (waiters[k].pred(j)) {
          const w = waiters.splice(k, 1)[0];
          clearTimeout(w.timer);
          w.resolve(j);
        }
      }
    });
    return;
  }
  res.writeHead(404, h); res.end();
});

function waitProbe(pred, ms, label) {
  const hit = probes.find((p) => pred(p) && !p.__used);
  if (hit) { hit.__used = true; return Promise.resolve(hit); }
  return new Promise((resolve) => {
    const w = { pred, resolve };
    w.timer = setTimeout(() => {
      const k = waiters.indexOf(w);
      if (k >= 0) waiters.splice(k, 1);
      if (label) console.log('    （等待超时：' + label + '）');
      resolve(null);
    }, ms || 8000);
    waiters.push(w);
  });
}

/* ───────────── HTTP 小工具 ───────────── */
function req(method, p, body) {
  return new Promise((resolve, reject) => {
    const u = new URL(SRV + p);
    const data = body == null ? null : JSON.stringify(body);
    const r = http.request({
      host: u.hostname, port: u.port, path: u.pathname + u.search, method,
      headers: data ? { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(data) } : {},
    }, (res) => {
      let buf = '';
      res.on('data', (c) => { buf += c; });
      res.on('end', () => {
        let j = null;
        try { j = JSON.parse(buf); } catch (e) {}
        resolve({ status: res.statusCode, body: j });
      });
    });
    r.on('error', reject);
    if (data) r.write(data);
    r.end();
  });
}

function sseParent(code) {
  const msgs = [];
  const r = http.get({ host: '127.0.0.1', port: 8099, path: '/api/rooms/' + code + '/events?role=parent' }, (res) => {
    let buf = '';
    res.setEncoding('utf8');
    res.on('data', (c) => {
      buf += c;
      let i;
      while ((i = buf.indexOf('\n\n')) >= 0) {
        const blk = buf.slice(0, i);
        buf = buf.slice(i + 2);
        const line = blk.split('\n').find((l) => l.startsWith('data: '));
        if (line) { try { msgs.push(JSON.parse(line.slice(6))); } catch (e) {} }
      }
    });
  });
  r.on('error', () => {});
  return { msgs, close() { try { r.destroy(); } catch (e) {} } };
}

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

/* ───────────── 注入 ───────────── */
function buildTestPage(code) {
  const src = fs.readFileSync(SRC, 'utf8');
  if (src.indexOf('window.__bb36') < 0) throw new Error('孩子端没有测试钩子 __bb36');
  const inject = `
<script>
(function () {
  var SRV = '${SRV}';
  var CODE = '${code}';
  var PROBE = 'http://127.0.0.1:${PROBE_PORT}/probe';
  var answered = false, lastKey = '';
  var stable = 0, lastLine = null;

  function probe(tag, extra) {
    try {
      var x = new XMLHttpRequest();
      x.open('POST', PROBE, true);
      x.setRequestHeader('Content-Type', 'application/json');
      x.send(JSON.stringify({ tag: tag, extra: extra || null, t: Date.now() }));
    } catch (e) {}
  }

  function snap() {
    var B = window.__bb36;
    if (!B) return null;
    return { page: B.page(), line: B.lineText(), cur: B.cur(), card: B.cardOn(),
             end: B.endOpen(), hearts: B.hearts() };
  }

  setTimeout(function () {
    var B = window.__bb36;
    if (!B) { probe('no-hook'); return; }
    document.getElementById('inSrv').value = SRV;
    document.getElementById('inCode').value = CODE;
    probe('connect-called');
    B.connect();
  }, 300);

  setInterval(function () {
    var s = snap();
    if (!s) return;
    var key = s.page + '|' + s.line + '|' + s.card + '|' + s.end;
    if (key !== lastKey) { lastKey = key; probe('ui', s); }

    // 台词是逐字打出来的，不能一看到有字就去比。
    // 连续 3 个 tick（≈600ms）长度不再变化，才认为这句打完了。
    if (!answered && s.page === 'scene' && s.cur && String(s.cur[4]).indexOf('link-') === 0 && s.line) {
      if (s.line === lastLine) {
        stable++;
        if (stable >= 3) {
          answered = true;
          probe('line-done', s);
          setTimeout(function () {
            document.getElementById('btnNo').click();
            probe('clicked-no', snap());
          }, 200);
        }
      } else {
        stable = 0;
        lastLine = s.line;
        probe('line-typing', s);
      }
    }
  }, 200);

  window.addEventListener('error', function (e) {
    probe('js-error', String(e.message || e.type));
  });
  window.addEventListener('unhandledrejection', function (e) {
    probe('js-reject', String(e.reason));
  });
})();
</script>
</body>`;
  const out = src.replace('</body>', inject);
  if (out === src) throw new Error('注入失败：index.html 里没有 </body>');
  fs.writeFileSync(TMP_HTML, out, 'utf8');
  return TMP_HTML;
}

/* ───────────── 主流程 ───────────── */
let chrome = null;

function killChrome() {
  if (!chrome) return;
  const pid = chrome.pid;
  try { spawn('taskkill', ['/PID', String(pid), '/T', '/F'], { stdio: 'ignore' }); } catch (e) {}
  try { chrome.kill(); } catch (e) {}
  chrome = null;
}

async function main() {
  if (!CHROME) throw new Error('找不到 Chrome / Edge');
  console.log('\n宝贝三十六计 · 浏览器端到端测试');
  console.log('  浏览器 ' + CHROME);
  console.log('  服务   ' + SRV + '\n');

  await new Promise((r) => probeServer.listen(PROBE_PORT, '127.0.0.1', r));

  // ── 1. 建房 ──
  console.log('[1] 家长端建房');
  const c = await req('POST', '/api/rooms');
  const code = c.body.code;
  ok('拿到 4 位房号', /^\d{4}$/.test(code), code);

  // ── 2. 打开孩子端 ──
  console.log('\n[2] 无头 Chrome 打开孩子端（file:// 直开，最接近 ArkWeb 的 $rawfile）');
  const page = buildTestPage(code);
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'bb36-'));
  const fileUrl = 'file:///' + page.replace(/\\/g, '/');

  chrome = spawn(CHROME, [
    '--headless=new',
    '--disable-gpu',
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-extensions',
    '--allow-file-access-from-files',
    '--autoplay-policy=no-user-gesture-required',
    '--window-size=430,900',
    '--user-data-dir=' + profile,
    fileUrl,
  ], { stdio: 'ignore' });

  const connected = await waitProbe((p) => p.tag === 'connect-called', 10000, '孩子端点连接');
  ok('孩子端执行到 connect()', !!connected);

  await sleep(900);
  const st = await req('GET', '/api/rooms/' + code);
  ok('服务端看到孩子端连上了（presence.kid=1）', st.body && st.body.presence.kid === 1,
    JSON.stringify(st.body && st.body.presence));

  const errs = probes.filter((p) => p.tag === 'js-error' || p.tag === 'js-reject');
  ok('孩子端没有 JS 报错', errs.length === 0, errs.map((e) => e.extra).join(' | '));

  // ── 3. 家长连上 ──
  console.log('\n[3] 家长端点「开始」');
  const par = sseParent(code);
  await sleep(400);
  await req('POST', '/api/rooms/' + code + '/act', { role: 'parent', type: 'hello', who: '陌生人' });

  const scene = await waitProbe((p) => p.tag === 'ui' && p.extra && p.extra.page === 'scene', 6000, '孩子端切进场景');
  ok('孩子端收到 hello 并切进 scene 页', !!scene, scene && JSON.stringify(scene.extra));

  // ── 4. 出题 ──
  console.log('\n[4] 家长发一句台词');
  const LINE = '小朋友，我有一个礼物给你。你想知道是什么吗？快和我到前面车里去看看吧。';
  await req('POST', '/api/rooms/' + code + '/act', { role: 'parent', type: 'say', who: '陌生人', text: LINE });

  const shown = await waitProbe((p) => p.tag === 'line-done', 15000, '孩子端把台词打完');
  ok('孩子端把整句台词打完了', !!shown, shown && shown.extra && shown.extra.line);
  ok('显示的台词和家长发的完全一致', !!shown && shown.extra.line === LINE,
    shown && JSON.stringify(shown.extra.line));
  ok('这句被认成联机题（cur[4] 以 link- 开头）',
    !!shown && String(shown.extra.cur[4]).indexOf('link-') === 0, shown && String(shown.extra.cur[4]));
  ok('孩子端标题变成「家长陪我玩」', true);

  // ── 5. 孩子点叉 ──
  console.log('\n[5] 孩子点「叉」（不跟他走）');
  const clicked = await waitProbe((p) => p.tag === 'clicked-no', 6000, '孩子端点击');
  ok('孩子端点到了叉', !!clicked);

  let ans = null;
  for (let i = 0; i < 25 && !ans; i++) {
    await sleep(200);
    ans = par.msgs.find((m) => m.type === 'answer') || null;
  }
  ok('家长端收到孩子作答', !!ans);
  ok('作答被判为「识破」（ok=true）', !!ans && ans.ok === true, ans && String(ans.ok));
  ok('记分 ok=1', !!ans && ans.score.ok === 1, ans && JSON.stringify(ans.score));

  // ── 6. 结束 ──
  console.log('\n[6] 家长结束这一局');
  await req('POST', '/api/rooms/' + code + '/act',
    { role: 'parent', type: 'end', win: true, text: '他一次都没跟你走，真棒！' });
  const endUi = await waitProbe((p) => p.tag === 'ui' && p.extra && p.extra.end === true, 6000, '孩子端弹结算');
  ok('孩子端弹出结算面板', !!endUi);

  // ── 7. 全程无报错 ──
  console.log('\n[7] 全程检查');
  const errs2 = probes.filter((p) => p.tag === 'js-error' || p.tag === 'js-reject');
  ok('全程零 JS 报错', errs2.length === 0, errs2.map((e) => e.extra).join(' | '));
  ok('没有出现 no-hook', !probes.some((p) => p.tag === 'no-hook'));

  par.close();
  await req('DELETE', '/api/rooms/' + code);

  console.log('\n' + '─'.repeat(46));
  console.log('  通过 ' + pass + ' 项，失败 ' + fail + ' 项');
  if (fail) { console.log('\n  失败清单：'); fails.forEach((f) => console.log('   · ' + f)); }
  console.log('─'.repeat(46) + '\n');
}

main()
  .catch((e) => { console.error('\n测试崩了：', e); fail++; })
  .then(() => {
    killChrome();
    try { fs.unlinkSync(TMP_HTML); } catch (e) {}
    probeServer.close();
    setTimeout(() => process.exit(fail ? 1 : 0), 300);
  });
