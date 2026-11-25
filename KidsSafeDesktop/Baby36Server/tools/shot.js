'use strict';
/*
 * 宝贝三十六计 · 家长端 -- 截图与页面巡检小工具（CDP 驱动）
 *
 * 为什么不用 chrome --screenshot：
 *   家长端页面连着 SSE 长连接，--virtual-time-budget 会一直等网络空闲，
 *   结果是渲染器被判定 abnormal 直接崩。用 CDP 就能自己控制「等多久、点什么、何时截」。
 *
 * 用法：
 *   node Baby36Server/tools/shot.js            # 截三张到临时目录并打印路径
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

const CHROME = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
].find((p) => { try { return fs.existsSync(p); } catch (e) { return false; } });

const PORT = 9223;
const OUT = path.join(os.tmpdir(), 'bb36');

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

async function connect() {
  const t0 = Date.now();
  while (Date.now() - t0 < 15000) {
    try {
      const r = await fetch('http://127.0.0.1:' + PORT + '/json/list');
      const list = await r.json();
      const page = list.find((x) => x.type === 'page');
      if (page) return page.webSocketDebuggerUrl;
    } catch (e) {}
    await sleep(200);
  }
  throw new Error('CDP 连不上');
}

function rpc(wsUrl) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(wsUrl);
    let id = 0;
    const pending = new Map();
    ws.onmessage = (ev) => {
      const m = JSON.parse(ev.data);
      if (m.id && pending.has(m.id)) {
        const { res } = pending.get(m.id);
        pending.delete(m.id);
        res(m);
      }
    };
    ws.onerror = (e) => reject(new Error('WS 出错'));
    ws.onopen = () => {
      resolve({
        send(method, params) {
          return new Promise((res) => {
            const i = ++id;
            pending.set(i, { res });
            ws.send(JSON.stringify({ id: i, method, params: params || {} }));
          });
        },
        close() { try { ws.close(); } catch (e) {} },
      });
    };
  });
}

async function shot(cdp, file, full) {
  const r = await cdp.send('Page.captureScreenshot', {
    format: 'png',
    captureBeyondViewport: !!full,
  });
  if (!r.result || !r.result.data) throw new Error('截图失败');
  fs.writeFileSync(file, Buffer.from(r.result.data, 'base64'));
  return file;
}

async function evalJs(cdp, expr) {
  const r = await cdp.send('Runtime.evaluate', {
    expression: expr, returnByValue: true, awaitPromise: true,
  });
  if (r.result && r.result.exceptionDetails) {
    throw new Error('页面报错：' + JSON.stringify(r.result.exceptionDetails.text || r.result.exceptionDetails));
  }
  return r.result && r.result.result ? r.result.result.value : undefined;
}

async function main() {
  if (!CHROME) throw new Error('找不到 Chrome');
  fs.mkdirSync(OUT, { recursive: true });
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'bb36cdp-'));

  const chrome = spawn(CHROME, [
    '--headless=new',
    '--disable-gpu',
    '--no-first-run',
    '--no-default-browser-check',
    '--hide-scrollbars',
    '--remote-debugging-port=' + PORT,
    '--remote-allow-origins=*',
    '--user-data-dir=' + profile,
    'about:blank',
  ], { stdio: 'ignore' });

  const kill = () => {
    try { spawn('taskkill', ['/PID', String(chrome.pid), '/T', '/F'], { stdio: 'ignore' }); } catch (e) {}
    try { chrome.kill(); } catch (e) {}
  };

  const out = [];
  try {
    const wsUrl = await connect();
    const cdp = await rpc(wsUrl);
    await cdp.send('Page.enable');
    await cdp.send('Runtime.enable');
    // 按手机竖屏渲染，2 倍像素密度，看得清字
    await cdp.send('Emulation.setDeviceMetricsOverride', {
      width: 430, height: 1000, deviceScaleFactor: 2, mobile: true,
    });

    // ── A. 初始态（未建房）──
    await cdp.send('Page.navigate', { url: 'http://127.0.0.1:8099/' });
    await sleep(1800);
    out.push(await shot(cdp, path.join(OUT, 'par_a.png')));
    const t1 = await evalJs(cdp, 'document.title');
    const c1 = await evalJs(cdp, "document.getElementById('btnCreate').textContent");
    console.log('A 初始态  按钮=' + c1);

    // ── B. 建房 + 开始 → 出题界面 ──
    await evalJs(cdp, "document.getElementById('btnCreate').click()");
    await sleep(1200);
    const code = await evalJs(cdp, "document.getElementById('codeDigits').textContent");
    console.log('B 房号=' + code);
    await evalJs(cdp, "document.getElementById('btnHello').click()");
    await sleep(800);
    await evalJs(cdp,
      "document.getElementById('freeText').value='小朋友，你妈妈让我来接你，车就在门口。';"
      + "document.getElementById('list').scrollTop=0; window.scrollTo(0,0);");
    await sleep(400);
    out.push(await shot(cdp, path.join(OUT, 'par_b.png'), true));

    // ── C. 塞几条记录看时间线 ──
    await evalJs(cdp, `
      (function(){
        var log = document.getElementById('log');
        document.getElementById('okNum').textContent = '2';
        document.getElementById('badNum').textContent = '1';
        log.innerHTML = [
          '<div class="entry me"><span class="who">你</span><span class="say">小朋友，我有一个礼物给你，快和我到车里去看看吧。</span><span class="tm">19:32</span></div>',
          '<div class="entry bad"><span class="who">上当</span><span class="say">他跟着走了…</span><span class="tm">19:33</span></div>',
          '<div class="entry me"><span class="who">你</span><span class="say">我是你妈妈的同事，她让我来接你。</span><span class="tm">19:34</span></div>',
          '<div class="entry ok"><span class="who">识破</span><span class="say">「我不跟你走！」</span><span class="tm">19:34</span></div>'
        ].join('');
        document.getElementById('cardLog').scrollIntoView();
      })();
    `);
    await sleep(500);
    out.push(await shot(cdp, path.join(OUT, 'par_c.png')));

    cdp.close();
  } finally {
    kill();
    await sleep(300);
  }

  console.log('\n截图：');
  out.forEach((p) => console.log('  ' + p));
}

main().catch((e) => { console.error('失败：', e.message); process.exit(1); });
