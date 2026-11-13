const { Resvg } = require('@resvg/resvg-js');
const fs = require('fs');
const path = require('path');

const OUT = path.join(__dirname, 'out');
fs.mkdirSync(OUT, { recursive: true });

// ---- 字体 ----
const FONT = {
  fontFiles: [
    'C:/Windows/Fonts/simhei.ttf',
    'C:/Windows/Fonts/seguiemj.ttf',
    'C:/Windows/Fonts/arial.ttf'
  ],
  loadSystemFonts: false,
  defaultFontFamily: 'SimHei'
};

// ---- 配色（与 App 一致）----
const C = {
  purple: '#7E57C2', teal: '#26C6DA', pink: '#FF8A65', red: '#E53935',
  grape: '#8E24AA', blue: '#29B6F6', magenta: '#AB47BC', cyan: '#00B8D4',
  green: '#26A69A', orange: '#EF6C00', indigo: '#5C6BC0', darkTeal: '#00897B'
};
const INK = '#2E3A4D';
const SOFT = '#8D6E63';

const W = 720, H = 1280;
const PADX = 48;

// 渐变背景 def
function bgDefs(id, c1, c2) {
  return `<linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="${c1}"/><stop offset="1" stop-color="${c2}"/></linearGradient>`;
}

function statusBar() {
  return `<g font-family="SimHei, sans-serif" fill="#3A2E5C">
    <text x="44" y="44" font-size="26" font-weight="bold">9:41</text>
    <g transform="translate(600,22)" fill="#3A2E5C">
      <rect x="0" y="0" width="26" height="13" rx="3" fill="none" stroke="#3A2E5C" stroke-width="2"/>
      <rect x="3" y="3" width="18" height="7" rx="1.5" fill="#3A2E5C"/>
      <rect x="28" y="3" width="3" height="7" rx="1.5" fill="#3A2E5C"/>
    </g>
    <g transform="translate(556,24)" stroke="#3A2E5C" stroke-width="2" fill="none">
      <path d="M0 0 h5 v12 h-5 z M8 3 v6 h3 M14 1 v10 h3 M20 -1 v14 h3"/>
    </g>
  </g>`;
}

function topBar(title, rightIcon) {
  return `<g>
    <rect x="0" y="56" width="${W}" height="64" fill="url(#bar)"/>
    <text x="40" y="100" font-size="40" fill="#FFFFFF" font-family="SimHei">‹</text>
    <text x="${W/2}" y="100" font-size="34" fill="#FFFFFF" font-weight="bold"
          text-anchor="middle" font-family="SimHei">${title}</text>
    ${rightIcon ? `<text x="${W-46}" y="100" font-size="34" fill="#FFFFFF" text-anchor="end" font-family="Segoe UI Emoji, SimHei">${rightIcon}</text>` : ''}
  </g>`;
}

function tile(x, y, s, emoji, name, color) {
  return `<g transform="translate(${x},${y})">
    <rect width="${s}" height="${s}" rx="26" fill="${color}"/>
    <text x="${s/2}" y="${s/2+2}" font-size="56" text-anchor="middle" dominant-baseline="central"
          font-family="Segoe UI Emoji, SimHei, sans-serif">${emoji}</text>
    <rect x="-6" y="${s+12}" width="${s+12}" height="34" rx="17" fill="#B3FFFFFF"/>
    <text x="${s/2}" y="${s+12+23}" font-size="22" text-anchor="middle" fill="${INK}"
          font-weight="bold" font-family="SimHei, sans-serif">${name}</text>
  </g>`;
}

// 居中网格
function grid(items, cols, tileSize, y0) {
  const cellW = (W - 2*PADX) / cols;
  const s = tileSize;
  let out = '';
  items.forEach((it, i) => {
    const r = Math.floor(i / cols), c = i % cols;
    const x = PADX + c*cellW + (cellW - s)/2;
    const y = y0 + r*(s + 64);
    out += tile(x, y, s, it.e, it.n, it.c);
  });
  return out;
}

function render(name, inner) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
    <defs>${bgDefs('bg','#F3E9FF','#E1F5FE')}${bgDefs('bar','#7E57C2','#5C6BC0')}</defs>
    <rect width="${W}" height="${H}" fill="url(#bg)"/>
    ${statusBar()}
    ${inner}
  </svg>`;
  const r = new Resvg(svg, { fitTo:{mode:'width', value:W}, font: FONT });
  fs.writeFileSync(path.join(OUT, name), r.render().asPng());
  console.log('wrote', name);
}

// ================= 1. 首页 =================
const home = [
  {e:'🎮',n:'小游戏乐园',c:C.purple},{e:'🎵',n:'听一听',c:C.pink},
  {e:'🛡️',n:'安全知识',c:C.red},{e:'📚',n:'口诀乐园',c:C.grape},
  {e:'📖',n:'学习天地',c:C.blue},{e:'🔤',n:'英语学习',c:C.purple},
  {e:'🗣️',n:'语音训练',c:C.magenta},{e:'🚀',n:'科学实验室',c:C.cyan},
  {e:'👀',n:'眼保健操',c:C.green},{e:'🧍',n:'身体百科',c:C.orange},
  {e:'❓',n:'操作帮助',c:C.indigo},{e:'🕹️',n:'游戏乐园',c:C.darkTeal}
];
render('screenshot_home.png', `
  ${topBar('儿童桌面')}
  <g transform="translate(0,140)">
    <rect x="48" y="0" width="624" height="190" rx="28" fill="#B3FFFFFF"/>
    <text x="360" y="58" font-size="60" text-anchor="middle" font-family="Segoe UI Emoji, SimHei">🌈</text>
    <text x="360" y="104" font-size="32" font-weight="bold" text-anchor="middle" fill="#5D4037" font-family="SimHei">下午好，小朋友</text>
    <text x="360" y="162" font-size="64" font-weight="bold" text-anchor="middle" fill="#7E57C2" font-family="SimHei">15:30</text>
  </g>
  ${grid(home, 4, 124, 372)}
`);

// ================= 2. 小游戏乐园 =================
const games = [
  {e:'🔤',n:'看图识字',c:C.purple},{e:'🔢',n:'数一数',c:C.pink},
  {e:'🧩',n:'配对',c:C.red},{e:'🎨',n:'找颜色',c:C.grape},
  {e:'🔊',n:'听音辨物',c:C.blue},{e:'🔗',n:'连连看',c:C.purple},
  {e:'🌀',n:'走迷宫',c:C.cyan},{e:'🛡️',n:'安全问答',c:C.green},
  {e:'🖍️',n:'涂鸦',c:C.orange},{e:'🐟',n:'小鱼吃吃',c:C.darkTeal},
  {e:'🧮',n:'算术',c:C.magenta},{e:'🔢',n:'数独',c:C.indigo}
];
render('screenshot_games.png', `
  ${topBar('🎮 小游戏乐园','🏆')}
  <text x="360" y="160" font-size="28" font-weight="bold" text-anchor="middle" fill="#7E57C2" font-family="SimHei">Hi，小朋友～ 挑一个喜欢的游戏吧！</text>
  <rect x="48" y="184" width="624" height="92" rx="22" fill="#FFFFFF" opacity="0.85"/>
  <text x="76" y="220" font-size="22" font-weight="bold" fill="#7E57C2" font-family="SimHei">🌟 选择难度</text>
  <g font-family="SimHei, sans-serif" font-size="24" font-weight="bold" text-anchor="middle">
    <rect x="76" y="232" width="170" height="36" rx="18" fill="#7E57C2"/><text x="161" y="258" fill="#fff">🌱 简单</text>
    <rect x="275" y="232" width="170" height="36" rx="18" fill="#fff" stroke="#7E57C2"/><text x="360" y="258" fill="#4A148C">⭐ 中等</text>
    <rect x="474" y="232" width="170" height="36" rx="18" fill="#fff" stroke="#7E57C2"/><text x="559" y="258" fill="#4A148C">🔥 困难</text>
  </g>
  <text x="360" y="316" font-size="24" text-anchor="middle" fill="#8D6E63" font-family="SimHei">👇 点一点，开始玩吧！</text>
  ${grid(games, 4, 116, 340)}
`);

// ================= 3. 科学实验室 =================
function sciCard(x,y,w,h,title,tag,tagColor){
  return `<g transform="translate(${x},${y})">
    <rect width="${w}" height="${h}" rx="20" fill="#FFFFFF" opacity="0.95"/>
    <rect x="0" y="0" width="${w}" height="10" rx="5" fill="${tagColor}"/>
    <text x="20" y="58" font-size="26" font-weight="bold" fill="${INK}" font-family="SimHei">${title}</text>
    <rect x="20" y="${h-44}" width="${60}" height="30" rx="15" fill="${tagColor}" opacity="0.18"/>
    <text x="50" y="${h-24}" font-size="18" text-anchor="middle" fill="${tagColor}" font-weight="bold" font-family="SimHei">${tag}</text>
  </g>`;
}
render('screenshot_science.png', `
  ${topBar('🚀 星际科学实验室')}
  <g font-family="SimHei, sans-serif" font-size="22" text-anchor="middle">
    ${['全部','物理','化学','生命','太空','身体奥秘'].map((t,i)=>{
      const x=48+i*100; const on=(i===5);
      return `<rect x="${x}" y="148" width="92" height="40" rx="20" fill="${on?'#EF6C00':'#FFFFFF'}" opacity="${on?1:0.85}"/>
              <text x="${x+46}" y="175" fill="${on?'#fff':'#5D4037'}" font-weight="bold">${t}</text>`;
    }).join('')}
  </g>
  ${sciCard(48,210,288,150,'彩虹牛奶','化学',C.pink)}
  ${sciCard(384,210,288,150,'火山喷发','化学',C.red)}
  ${sciCard(48,378,288,150,'会跳舞的米','物理',C.purple)}
  ${sciCard(384,378,288,150,'种子发芽','生命',C.green)}
  ${sciCard(48,546,288,150,'星座投影','太空',C.indigo)}
  ${sciCard(384,546,288,150,'心跳听诊器','身体',C.orange)}
  ${sciCard(48,714,288,150,'气球肺呼吸','身体',C.cyan)}
  ${sciCard(384,714,288,150,'指纹侦探','身体',C.grape)}
`);

// ================= 4. 听一听 CD 播放器 =================
render('screenshot_player.png', `
  ${topBar('🎵 听一听')}
  <g transform="translate(360,360)">
    <circle r="180" fill="#FFFFFF" opacity="0.25"/>
    <circle r="160" fill="#7E57C2"/>
    <circle r="160" fill="none" stroke="#FFFFFF" stroke-width="6" stroke-dasharray="20 14" opacity="0.6"/>
    <circle r="92" fill="#FFE0B2"/>
    <circle r="34" fill="#7E57C2"/>
    <text x="0" y="6" font-size="92" text-anchor="middle" dominant-baseline="central" font-family="Segoe UI Emoji, SimHei">⭐</text>
    <text x="120" y="-120" font-size="40" fill="#fff" font-family="Segoe UI Emoji, SimHei">🎵</text>
    <text x="-130" y="120" font-size="36" fill="#fff" font-family="Segoe UI Emoji, SimHei">✨</text>
  </g>
  <text x="360" y="640" font-size="38" font-weight="bold" text-anchor="middle" fill="${INK}" font-family="SimHei">小星星</text>
  <text x="360" y="682" font-size="24" text-anchor="middle" fill="#8D6E63" font-family="SimHei">经典儿歌 · 第 3 首</text>
  <rect x="80" y="724" width="560" height="8" rx="4" fill="#D1C4E9"/>
  <rect x="80" y="724" width="240" height="8" rx="4" fill="#7E57C2"/>
  <g font-family="Segoe UI Emoji, SimHei" font-size="64" fill="${INK}" text-anchor="middle">
    <text x="200" y="828">⏮</text>
    <circle cx="360" cy="800" r="46" fill="#7E57C2"/><text x="360" y="812" font-size="48" fill="#fff">⏸</text>
    <text x="520" y="828">⏭</text>
  </g>
  <g font-family="Segoe UI Emoji, SimHei" font-size="40" fill="#8D6E63" text-anchor="middle">
    <text x="120" y="812">🔁</text><text x="600" y="812">📃</text>
  </g>
`);

// ================= 5. 学习天地 =================
const grades = [
  {e:'1',n:'一年级',c:C.blue},{e:'2',n:'二年级',c:C.green},
  {e:'3',n:'三年级',c:C.orange},{e:'4',n:'四年级',c:C.purple},
  {e:'5',n:'五年级',c:C.red},{e:'6',n:'六年级',c:C.grape}
];
render('screenshot_learn.png', `
  ${topBar('📖 学习天地')}
  <text x="360" y="160" font-size="26" text-anchor="middle" fill="#0277BD" font-family="SimHei">选一个年级，开始口算练习吧！做完会帮你批改哦～</text>
  ${grid(grades, 3, 150, 230)}
`);

// ================= 应用图标 512 =================
(function(){
  const S=512;
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${S}" height="${S}" viewBox="0 0 ${S} ${S}">
    <defs>${bgDefs('ic','#7E57C2','#26C6DA')}</defs>
    <rect width="${S}" height="${S}" rx="112" fill="url(#ic)"/>
    <circle cx="256" cy="232" r="120" fill="#FFFFFF" opacity="0.22"/>
    <text x="256" y="244" font-size="200" text-anchor="middle" dominant-baseline="central" font-family="Segoe UI Emoji, SimHei">🧒</text>
    <circle cx="392" cy="360" r="46" fill="#FFE0B2"/>
    <text x="392" y="372" font-size="52" text-anchor="middle" dominant-baseline="central" font-family="Segoe UI Emoji, SimHei">🛡️</text>
    <text x="256" y="430" font-size="58" text-anchor="middle" font-weight="bold" fill="#fff" font-family="SimHei">儿童桌面</text>
  </svg>`;
  const r=new Resvg(svg,{fitTo:{mode:'width',value:S},font:FONT});
  fs.writeFileSync(path.join(OUT,'icon_512.png'), r.render().asPng());
  fs.writeFileSync(path.join(OUT,'icon_320.png'), new Resvg(svg,{fitTo:{mode:'width',value:320},font:FONT}).render().asPng());
  console.log('wrote icon_512.png / icon_320.png');
})();

// ================= 头图 1280x720 =================
(function(){
  const w=1280,h=720;
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
    <defs>${bgDefs('hb','#7E57C2','#26C6DA')}
      <linearGradient id="hw" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#FFFFFF" stop-opacity="0.18"/><stop offset="1" stop-color="#FFFFFF" stop-opacity="0"/></linearGradient>
    </defs>
    <rect width="${w}" height="${h}" fill="url(#hb)"/>
    <rect width="${w}" height="${h}" fill="url(#hw)"/>
    <g font-family="Segoe UI Emoji, SimHei" font-size="90" opacity="0.9">
      <text x="980" y="200">🎮</text><text x="1080" y="340">🚀</text><text x="1160" y="520">📖</text>
      <text x="900" y="560">🗣️</text><text x="1040" y="660">🧍</text>
    </g>
    <text x="80" y="300" font-size="76" font-weight="bold" fill="#fff" font-family="SimHei">儿童安全桌面</text>
    <text x="84" y="372" font-size="34" fill="#FFFFFF" font-family="SimHei">给孩子一个安全、好玩的成长空间</text>
    <text x="84" y="436" font-size="26" fill="#EDE7F6" font-family="SimHei">桌面式图标入口 · 家长守护 · 护眼模式 · 离线可玩</text>
    <g transform="translate(84,500)">
      <rect width="220" height="64" rx="32" fill="#FFFFFF"/>
      <text x="110" y="42" font-size="28" font-weight="bold" text-anchor="middle" fill="#7E57C2" font-family="SimHei">立即体验</text>
    </g>
  </svg>`;
  const r=new Resvg(svg,{fitTo:{mode:'width',value:w},font:FONT});
  fs.writeFileSync(path.join(OUT,'header_1280x720.png'), r.render().asPng());
  console.log('wrote header_1280x720.png');
})();

console.log('ALL DONE');
