(function () {
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
