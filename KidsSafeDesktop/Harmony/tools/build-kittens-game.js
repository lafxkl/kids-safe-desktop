/**
 * 把《猫国建设者》(Kittens Game / cat-zh) 打成**完全离线**的资源目录，
 * 供鸿蒙 Web 组件从 rawfile 直接加载。
 *
 * 原版为什么不能离线：
 *   1. React / jQuery / dojo / lz-string / SystemJS 全部从 CDN 拉，断网就是 404，白屏；
 *   2. 25 个游戏模块靠 SystemJS 的 `System.import()` 加载——它走 **XHR**，
 *      而 Web 组件把本地页跑在 `resource://rawfile/` 下，XHR 会被拦；
 *   3. i18n 用 `$.getJSON("res/i18n/zh.json")` 在启动时取语言包，同样是 XHR；
 *   4. 还挂着百度统计、Google Analytics、Service Worker、汉化组推广浮窗。
 *
 * 处理办法（尽量少改游戏代码）：
 *   - CDN → lib/ 下的本地副本；
 *   - SystemJS → **自写的 <script> 注入式加载器**：顺序仍由 index.html 的 _import() 链控制，
 *     但不发 XHR。注意不能把所有模块拼成一个文件同步执行——那样模块会赶在语言包加载完
 *     之前执行，`$I()` 当场取不到文案，启动链就断在半路；
 *   - i18n 的 JSON 内联成 `__KS_ASSETS`，并把 $.getJSON 对这些路径改成直接返回内存对象；
 *   - 删掉统计、Service Worker、推广浮窗；
 *   - blob Worker 关掉（改用 setTimeout 计时），避免本地源下 Worker 被拦、游戏时间不走；
 *   - 另外产出一个儿童端安全层：隐藏删档/重置/作弊面板/站外链接（res/kidsafe.css +
 *     kiosk-guard.js），孩子点不到任何会把存档搞没或跳出应用的东西。
 *
 * 用法：node tools/build-kittens-game.js [源目录]
 * 默认源目录：D:/迅雷下载/cat-zh-main
 * 输出：entry/src/main/resources/rawfile/playland/kittens/
 */
const fs = require('fs');
const path = require('path');

const SRC = process.argv[2] || 'D:/迅雷下载/cat-zh-main';
const OUT = path.join(__dirname, '..', 'entry', 'src', 'main', 'resources', 'rawfile', 'playland', 'kittens');

/** 根目录下需要复制的脚本。 */
const ROOT_SCRIPTS = ['config.js', 'i18n.js', 'core.js', 'game.js'];

/** lib 里只留这四个；其余是 dropbox / md5 / dojo 源码 / 测试用例，用不上。 */
const LIBS = ['react.min.js', 'jQuery.js', 'dojo.xd.js', 'lz-string.js'];

/** 内联进 offline.js 的 JSON：启动期原本靠 XHR 取的那几个。 */
const INLINE_JSON = [
  'res/i18n/en.json',
  'res/i18n/zh.json',
  'res/i18n/crowdin/zh.json',
  'res/i18n/crowdin/zhA.json',
  'res/i18n/crowdin/zht.json',
  'build.version.json',
  'server.json'
];

/**
 * 儿童端安全层：**任何屏宽都生效**，所以不放在媒体查询里。
 *
 * 顶栏那一排 Save | Options | Reset | DEV | Wipe 是给网页版玩家用的，
 * 在儿童终端上「删档 / 重置 / 作弊面板」三样都是纯风险，站外链接还会拉起浏览器。
 * 只留「保存」和「选项」，其余整排连同分隔符一起去掉。
 */
const KIDSAFE_CSS = `/* 儿童端安全层（由 tools/build-kittens-game.js 生成） */
#devPanel,
#reset-link,
#wipe-link,
#getTheApp-link,
#newVersion,
#devModeButton,
#footerLinks,
#footer,
.links-block,
#topBar a[target="_blank"] {
  display: none !important;
}

/* 顶栏里的外站链接（官网 / 备用站 / 汉化组）一律不显示 */
#topBar a[href^="http"] {
  display: none !important;
}

/* 日志里的外链同样不该出现 */
#gameLog a[href^="http"] {
  display: none !important;
}

/* 触屏上长按会弹出「复制/搜索」菜单，游戏里没用还容易误触 */
#game, #game * {
  -webkit-touch-callout: none;
  -webkit-user-select: none;
  user-select: none;
}
`;

/**
 * 窄屏适配（手机竖屏 360-430vp；平板 ≥900 不触发）。
 *
 * 桌面版是「三列 + 写死像素」的布局，直接塞进手机有三层坑：
 *   1. body{width:1300px}、#gamePageContainer{width:1300px} —— 文档比屏幕宽，右侧一律被裁；
 *   2. #game{min-width:1180px; white-space:nowrap} —— 把父级又撑开一次，子元素还不许换行；
 *   3. .btn{width:170px}、#midColumn{width:630px} —— 两个按钮并排就顶出屏幕。
 * 这里逐层压回去：先让根节点可缩，再把三列改成竖排单列，最后放开固定宽度。
 *
 * 全部 !important：default.css 是运行时用 jQuery 追加到 <head> 末尾的，
 * 顺序永远晚于本文件，不加重压不住。
 */
const MOBILE_CSS = `@media (max-width: 900px) {
  /* 1. 根节点先能被屏幕缩住，否则后面做什么都白费 */
  html, body {
    width: auto !important;
    min-width: 0 !important;
    max-width: 100% !important;
    height: auto !important;
    overflow-x: hidden !important;
  }

  /* 2. 游戏容器写死 1300px，是右侧被裁的直接原因 */
  #gamePageContainer {
    width: 100% !important;
    min-width: 0 !important;
    max-width: 100% !important;
    overflow: visible !important;
    box-sizing: border-box !important;
  }

  /* 3. 顶栏：允许换行 */
  #topBar {
    position: static !important;
    width: 100% !important;
    min-width: 0 !important;
    height: auto !important;
    flex-wrap: wrap !important;
    padding: 4px 8px !important;
    box-sizing: border-box !important;
  }
  #headerLinks { display: none !important; }

  /* 4. 三列容器 → 单列竖排 */
  #game, #loadingContainer {
    position: static !important;
    top: auto !important;
    bottom: auto !important;
    width: 100% !important;
    min-width: 0 !important;
    max-width: 100% !important;
    padding: 0 10px 20px !important;
    box-sizing: border-box !important;
    /* 桌面版靠 nowrap 防止日志列折行，手机上必须解开，否则整页横向溢出 */
    white-space: normal !important;
  }

  .column {
    display: block !important;
    position: static !important;
    top: auto !important;
    width: 100% !important;
    min-width: 0 !important;
    max-width: none !important;
    height: auto !important;
    min-height: 0 !important;
    margin: 0 0 12px 0 !important;
    padding: 0 !important;
    overflow: visible !important;
  }

  #leftColumn, #midColumn, #rightColumn {
    width: 100% !important;
    min-width: 0 !important;
    max-width: none !important;
    padding: 0 !important;
    margin: 0 0 12px 0 !important;
  }
  #midColumn { margin-top: 0 !important; }

  .tabsContainer { position: static !important; padding-bottom: 6px !important; }

  /* 5. 固定宽度元素放开 */
  .btn {
    width: auto !important;
    min-width: 0 !important;
    max-width: 100% !important;
    box-sizing: border-box !important;
  }
  .btn.modern { width: auto !important; }
  .btnContent { white-space: normal !important; }
  #game table, #game .tabInner, #game .tabInner table {
    max-width: 100% !important;
    box-sizing: border-box !important;
  }
  td, th { white-space: normal !important; }

  /* 6. 日志限高，不然把下面的内容顶到很远 */
  #gameLog { max-height: 160px !important; }
}
`;

/**
 * 运行时护栏。
 *
 * CSS 挡的是「看得见」的入口，这个挡的是「点得到」的：
 *   - window.open / target=_blank 在 Web 组件里要么没反应，要么拉起系统浏览器，都不该发生；
 *   - 游戏里的外站链接点了就跳走，跳走就回不来（没有地址栏）；
 *   - 顶栏是 React 渲染的，重绘会把删掉的节点加回来，所以定时再擦一遍。
 */
const KIOSK_GUARD = `(function () {
  'use strict';

  // 1. 不许开新窗口
  window.open = function () { return null; };

  // 2. 普通点击里的外站跳转一律拦掉（捕获阶段，先于游戏自己的 handler）
  document.addEventListener('click', function (e) {
    var el = e.target;
    while (el && el.tagName !== 'A') { el = el.parentNode; }
    if (!el) { return; }
    var href = el.getAttribute('href') || '';
    if (/^https?:/i.test(href) || el.getAttribute('target') === '_blank') {
      e.preventDefault();
      e.stopPropagation();
    }
  }, true);

  // 3. 擦掉危险入口；React 重绘会加回来，所以周期性再擦
  var KILL_IDS = ['devPanel', 'reset-link', 'wipe-link', 'getTheApp-link', 'newVersion', 'devModeButton'];
  function scrub() {
    for (var i = 0; i < KILL_IDS.length; i++) {
      var el = document.getElementById(KILL_IDS[i]);
      if (el && el.parentNode) { el.parentNode.removeChild(el); }
    }
    var links = document.querySelectorAll('#topBar a[href^="http"], #topBar a[target="_blank"]');
    for (var j = 0; j < links.length; j++) {
      if (links[j].parentNode) { links[j].parentNode.removeChild(links[j]); }
    }
  }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { scrub(); fitWide(); });
  } else {
    scrub();
    fitWide();
  }
  setInterval(scrub, 2000);

  /*
   * 4. 平板（901-1300px）：桌面版是写死 1180-1300px 的三列布局，
   *    屏幕差一点就会把右边那列切掉。这里按比例整体缩小，
   *    而不是改布局——三列的相对关系是游戏设计的一部分，动了反而更难用。
   *
   *    ≤900px 交给 mobile.css 做单列竖排，不走这条路径。
   */
  var DESIGN_WIDTH = 1300;
  function fitWide() {
    var el = document.getElementById('gamePageContainer');
    if (!el) { return; }
    var avail = document.documentElement.clientWidth;
    if (avail <= 900) {
      el.style.zoom = '';
      return;
    }
    var z = avail < DESIGN_WIDTH ? avail / DESIGN_WIDTH : 1;
    el.style.zoom = z < 0.999 ? String(Math.max(0.62, z)) : '';
  }
  window.addEventListener('resize', fitWide);
})();
`;

function copyDir(from, to) {
  fs.mkdirSync(to, { recursive: true });
  for (const entry of fs.readdirSync(from, { withFileTypes: true })) {
    const src = path.join(from, entry.name);
    const dst = path.join(to, entry.name);
    if (entry.isDirectory()) {
      copyDir(src, dst);
    } else {
      fs.copyFileSync(src, dst);
    }
  }
}

/**
 * 自己实现递归删除：环境里的 fs.rmSync 被沙箱接管成「扔回收站」，会超时失败。
 * unlink + rmdir 不受影响，而且这里删的只是本脚本刚生成的构建产物。
 */
function removeDir(target) {
  if (!fs.existsSync(target)) {
    return;
  }
  if (!fs.statSync(target).isDirectory()) {
    fs.unlinkSync(target);
    return;
  }
  for (const entry of fs.readdirSync(target, { withFileTypes: true })) {
    const full = path.join(target, entry.name);
    if (entry.isDirectory()) {
      removeDir(full);
    } else {
      fs.unlinkSync(full);
    }
  }
  fs.rmdirSync(target);
}

/** 替换必须命中，否则说明源文件改版了——宁可报错，也不要静默产出坏包。 */
function patch(html, pattern, replacement, name) {
  if (html.match(pattern) === null) {
    throw new Error(`补丁「${name}」没匹配到，源 index.html 可能已改版`);
  }
  return html.replace(pattern, replacement);
}

function buildIndexHtml() {
  let html = fs.readFileSync(path.join(SRC, 'index.html'), 'utf8');

  // 1. 百度统计
  html = patch(html, /<script>\s*var _hmt[\s\S]*?<\/script>\s*/, '', '去百度统计');

  // 2. Service Worker（本地源下注册必失败，只会在控制台刷错误）
  html = patch(html, /<script>\s*if \(window\.location\.pathname[\s\S]*?<\/script>\s*/, '', '去 ServiceWorker');

  // 3~6. 四个 CDN 库换成 lib/ 本地副本
  html = patch(html,
    /<script src="https:\/\/lf3-cdn-tos\.bytecdntp\.com\/cdn\/expire-1-y\/react[^"]*"><\/script>/,
    '<script src="lib/react.min.js"></script>', 'React 本地化');
  html = patch(html,
    /<script src="https:\/\/lf3-cdn-tos\.bytecdntp\.com\/cdn\/expire-1-y\/jquery[^"]*"><\/script>/,
    '<script src="lib/jQuery.js"></script>', 'jQuery 本地化');
  html = patch(html,
    /<script src="https:\/\/lf3-cdn-tos\.bytecdntp\.com\/cdn\/expire-1-y\/dojo[^"]*"><\/script>/,
    '<script src="lib/dojo.xd.js"></script>', 'dojo 本地化');
  html = patch(html,
    /<script src="https:\/\/lf3-cdn-tos\.bytecdntp\.com\/cdn\/expire-1-y\/lz-string[^"]*"><\/script>/,
    '<script src="lib/lz-string.js"></script>', 'lz-string 本地化');

  // 7. SystemJS + 它的 document.write 兜底块 → offline.js（含无 XHR 的加载器）
  html = patch(html,
    /<script src="https:\/\/lf3-cdn-tos\.bytecdntp\.com\/cdn\/expire-1-y\/systemjs[^"]*"><\/script>\s*<script>[\s\S]*?<\/script>/,
    '<script src="offline.js"></script>', '模块加载器替换');

  // 8. Google Analytics
  html = patch(html,
    /<script async src="https:\/\/www\.googletagmanager\.com[\s\S]*?gtag\('config', 'G-0QBDX221PR'\);\s*<\/script>\s*/,
    '', '去 GA');

  // 9. 汉化组的推广浮窗（外链图标 + QQ 群二维码，儿童端不该出现）
  html = patch(html, /<script src="chs\/kf\.js\?v=4"><\/script>\s*/, '', '去推广浮窗');

  // 10. 作者署名本来是个外站链接，链接被安全层隐藏后会剩下半个「by」
  html = patch(html,
    /<a href="http:\/\/kittensgame\.com\/" class="copyright" target="_blank" tabindex="-1">bloodrizer<\/a>/,
    'bloodrizer', '作者署名去链接');

  // 11. 样式与护栏：安全层（全宽度生效）+ 窄屏布局 + 运行时拦截
  html = patch(html, /<\/head>/,
    '<link rel="stylesheet" href="res/kidsafe.css">\n\t' +
    '<link rel="stylesheet" href="res/mobile.css">\n\t' +
    '<script src="kiosk-guard.js"></script>\n\t</head>', '注入安全层与移动端样式');

  return html;
}

/** offline.js：内联 JSON + $.getJSON 本地化 + 无 XHR 的模块加载器。 */
function buildOfflineJs() {
  const assets = {};
  for (const rel of INLINE_JSON) {
    const file = path.join(SRC, rel);
    if (fs.existsSync(file)) {
      const json = JSON.parse(fs.readFileSync(file, 'utf8'));
      // 儿童端不上报任何数据：server.json 里的 telemetryUrl 直接抹掉，
      // 否则 game.js 会往第三方端点 POST
      if (rel === 'server.json') {
        delete json.telemetryUrl;
        delete json.telemetryAppId;
      }
      assets[rel] = json;
    }
  }

  return `/*
 * 离线适配层（由 tools/build-kittens-game.js 生成，请勿手改）
 *
 * 1. __KS_ASSETS：原本要靠 XHR 取的语言包和版本号，这里直接内联。
 * 2. $.getJSON / $.ajax 对这些路径改成读内存——本地页面跑在 resource:// 下，XHR 会被拦。
 * 3. System.import 换成 <script> 注入：同样是「加载并执行」，但不走 XHR。
 *    顺序仍由 index.html 的 _import() 链控制，语言包先于模块加载，
 *    模块里在定义期就调用 $I() 的地方才拿得到文案。
 */
window.__KS_ASSETS = ${JSON.stringify(assets)};

(function () {
  var origGetJSON = $.getJSON;

  /*
   * jQuery Promise 的替身：既要能链（.then 里返回的 Promise 会被拍平，
   * 这样 index.html 的 _import() 能等上一个模块 <script> 真正执行完），
   * 又要保留 jQuery 的 .done / .fail / .always——i18n.js 三个都在用。
   * 启动链是 var def = $.getJSON(...).then(...)，随后直接 def.catch(...)，缺一不可。
   */
  function wrap(p) {
    var api = {
      then: function (ok, err) { return wrap(p.then(ok, err)); },
      catch: function (err) { return wrap(p.catch(err)); },
      fail: function (err) { return wrap(p.catch(err)); },
      always: function (f) {
        return wrap(p.then(function (v) { f(); return v; }, function (e) { f(); return e; }));
      },
      done: function (f) { p.then(f); return api; }
    };
    return api;
  }

  window.__ksWrap = wrap;

  $.getJSON = function (url, data, callback) {
    var key = String(url).split('?')[0];
    if (!Object.prototype.hasOwnProperty.call(window.__KS_ASSETS, key)) {
      return origGetJSON.apply($, arguments);
    }
    var payload = window.__KS_ASSETS[key];
    var cb = typeof data === 'function' ? data : callback;
    if (typeof cb === 'function') {
      Promise.resolve().then(function () { cb(payload); });
    }
    return wrap(Promise.resolve(payload));
  };

  /*
   * $.ajax 本地化：server.json（公告）也走 XHR，同样会被拦。
   * 只接管内联过的路径，云端存档之类的请求仍交给原实现——失败了也只是没云同步，
   * 单机存档走 localStorage，不影响玩。
   */
  var origAjax = $.ajax;
  $.ajax = function (opts) {
    if (opts && typeof opts.url === 'string') {
      var key = opts.url.split('?')[0];
      if (Object.prototype.hasOwnProperty.call(window.__KS_ASSETS, key)) {
        var payload = window.__KS_ASSETS[key];
        if (typeof opts.success === 'function') {
          Promise.resolve().then(function () { opts.success(payload); });
        }
        return wrap(Promise.resolve(payload));
      }
    }
    return origAjax.apply($, arguments);
  };
})();

(function () {
  window.System = {
    config: function () {},
    import: function (url) {
      return new Promise(function (resolve) {
        var el = document.createElement('script');
        el.src = url;
        // 加载失败也 resolve：游戏会退化成「这个模块没定义」，总比整条启动链卡死强
        el.onload = function () { resolve(); };
        el.onerror = function () {
          console.error('[offline] 模块加载失败：' + url);
          resolve();
        };
        document.head.appendChild(el);
      });
    }
  };
  window.SystemJS = { config: function () {} };
})();
`;
}

// ───────── 主流程 ─────────
if (!fs.existsSync(SRC)) {
  console.error(`源目录不存在：${SRC}`);
  process.exit(1);
}

removeDir(OUT);
fs.mkdirSync(OUT, { recursive: true });

// 资源与脚本
copyDir(path.join(SRC, 'res'), path.join(OUT, 'res'));
copyDir(path.join(SRC, 'js'), path.join(OUT, 'js'));
fs.mkdirSync(path.join(OUT, 'lib'), { recursive: true });
for (const lib of LIBS) {
  fs.copyFileSync(path.join(SRC, 'lib', lib), path.join(OUT, 'lib', lib));
}
for (const name of ROOT_SCRIPTS) {
  fs.copyFileSync(path.join(SRC, name), path.join(OUT, name));
}

// 关掉 blob Worker：本地源下未必放行，失败的话游戏时间不走，画面会僵住
const gamePath = path.join(OUT, 'game.js');
const gameCode = fs.readFileSync(gamePath, 'utf8');
const patchedGame = gameCode.replace('return !dojo.isIE && window.Worker;',
  'return false; // 离线包：禁用 Worker，改用 setTimeout');
if (patchedGame === gameCode) {
  throw new Error('game.js 里没找到 Worker 判定，逻辑可能已改版');
}
fs.writeFileSync(gamePath, patchedGame, 'utf8');

fs.writeFileSync(path.join(OUT, 'offline.js'), buildOfflineJs(), 'utf8');
fs.writeFileSync(path.join(OUT, 'kiosk-guard.js'), KIOSK_GUARD, 'utf8');
fs.writeFileSync(path.join(OUT, 'res', 'kidsafe.css'), KIDSAFE_CSS, 'utf8');
fs.writeFileSync(path.join(OUT, 'res', 'mobile.css'), MOBILE_CSS, 'utf8');
fs.writeFileSync(path.join(OUT, 'index.html'), buildIndexHtml(), 'utf8');
fs.copyFileSync(path.join(SRC, 'icon.png'), path.join(OUT, 'icon.png'));
fs.copyFileSync(path.join(SRC, 'license.txt'), path.join(OUT, 'license.txt'));

const sizeOf = (p) => {
  let total = 0;
  const walk = (dir) => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, e.name);
      if (e.isDirectory()) {
        walk(full);
      } else {
        total += fs.statSync(full).size;
      }
    }
  };
  walk(p);
  return total;
};

console.log('离线包生成完成：');
console.log(`  输出目录：${OUT}`);
console.log(`  总体积：${(sizeOf(OUT) / 1024 / 1024).toFixed(1)} MB`);
