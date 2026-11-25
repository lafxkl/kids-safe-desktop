'use strict';
/*
 * 游戏乐园 · 全量加载巡检
 *
 * 把 rawfile/playland 下每个游戏都丢进无头 Chrome 打开一遍，收集
 * console.error / 未捕获异常，任何一个有报错就失败。
 *
 * 加新游戏不用改这个脚本 —— 扫目录即可。
 *
 * 用法：
 *   node Harmony/tools/test-playland.js
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

const CHROME = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
].find((p) => { try { return fs.existsSync(p); } catch (e) { return false; } });

const ROOT = path.join(__dirname, '..', 'entry/src/main/resources/rawfile/playland');
const CDP_PORT = 9225;

// 加载慢的给更多时间。kittens 是老 dojo + React 15，首屏要好几秒。
const WAIT = { kittens: 6000, pvz: 2500, xiangqi: 2200 };
const DEFAULT_WAIT = 1800;

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

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
    const events = [];
    ws.onmessage = (ev) => {
      const m = JSON.parse(ev.data);
      if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); return; }
      if (m.method) events.push(m);
    };
    ws.onerror = () => reject(new Error('WS 出错'));
    ws.onopen = () => resolve({
      events,
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

/** 从 CDP 事件里挑出真正的错误（忽略普通的 log/info） */
function pickErrors(events) {
  const out = [];
  for (const e of events) {
    if (e.method === 'Runtime.exceptionThrown') {
      const d = e.params.exceptionDetails || {};
      out.push('[异常] ' + (d.exception && d.exception.description
        ? String(d.exception.description).split('\n')[0]
        : d.text || '未知异常'));
    }
    if (e.method === 'Log.entryAdded') {
      const en = e.params.entry || {};
      if (en.level === 'error') out.push('[日志] ' + (en.text || '') + (en.url ? ' @' + en.url : ''));
    }
    if (e.method === 'Runtime.consoleAPICalled' && e.params.type === 'error') {
      const args = (e.params.args || []).map((a) => a.value != null ? String(a.value) : (a.description || a.type));
      out.push('[console] ' + args.join(' '));
    }
  }
  // 去重，同一句报错常常重复几十遍
  return [...new Set(out)];
}

async function main() {
  if (!CHROME) throw new Error('找不到 Chrome');
  const keys = fs.readdirSync(ROOT).filter((k) => {
    try { return fs.statSync(path.join(ROOT, k, 'index.html')).isFile(); } catch (e) { return false; }
  }).sort();

  console.log('\n游戏乐园 · 加载巡检（' + keys.length + ' 款）\n');

  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'pl-'));
  const chrome = spawn(CHROME, [
    '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--hide-scrollbars', '--allow-file-access-from-files',
    '--autoplay-policy=no-user-gesture-required',
    '--remote-debugging-port=' + CDP_PORT, '--remote-allow-origins=*',
    '--window-size=800,600', '--user-data-dir=' + profile,
    'about:blank',
  ], { stdio: 'ignore' });

  const kill = () => {
    try { spawn('taskkill', ['/PID', String(chrome.pid), '/T', '/F'], { stdio: 'ignore' }); } catch (e) {}
    try { chrome.kill(); } catch (e) {}
  };

  let bad = 0;
  const rows = [];
  try {
    const cdp = await cdpConnect();
    await cdp.send('Runtime.enable');
    await cdp.send('Log.enable');
    await cdp.send('Page.enable');

    for (const k of keys) {
      const file = path.join(ROOT, k, 'index.html');
      const before = cdp.events.length;

      await cdp.send('Page.navigate', { url: 'file:///' + file.replace(/\\/g, '/') });
      await sleep(WAIT[k] || DEFAULT_WAIT);

      const errs = pickErrors(cdp.events.slice(before));
      // 页面自己报「加载完成」没有？用 document.readyState + 有没有 body 兜底
      const probe = await cdp.send('Runtime.evaluate', {
        expression: 'JSON.stringify({d:document.readyState,t:(document.title||"").slice(0,40),n:document.body?document.body.children.length:0})',
        returnByValue: true,
      });
      const val = probe.result && probe.result.result && probe.result.result.value;
      const info = val ? JSON.parse(val) : {};

      const okRow = errs.length === 0;
      if (!okRow) bad++;
      rows.push({ k, ok: okRow, errs, info });
      console.log('  ' + (okRow ? '✓' : '✗') + ' ' + k.padEnd(12)
        + String(info.n || 0).padStart(3) + ' 个节点  ' + (info.t || '(无标题)'));
      errs.slice(0, 4).forEach((e) => console.log('      ' + e));
      if (errs.length > 4) console.log('      …还有 ' + (errs.length - 4) + ' 条');
    }
    cdp.close();
  } finally {
    kill();
  }

  console.log('\n' + '─'.repeat(52));
  console.log('  ' + keys.length + ' 款游戏，' + (keys.length - bad) + ' 款无报错，' + bad + ' 款有报错');
  console.log('─'.repeat(52) + '\n');
  process.exit(bad ? 1 : 0);
}

main().catch((e) => { console.error('巡检崩了：', e.message); process.exit(2); });
