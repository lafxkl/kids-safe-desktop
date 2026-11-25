'use strict';
/*
 * 从孩子端 index.html 的 JI 表导出家长端台词库 data.js。
 * 两边必须一字不差 —— 家长念的台词和孩子屏幕上看到的是同一句，
 * 所以这里不做任何改写，只做结构转换。
 *
 * 用法： node Baby36Server/tools/gen-data.js
 */
const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..', '..');
const SRC = path.join(ROOT, 'Harmony/entry/src/main/resources/rawfile/playland/baobei36/index.html');
const OUT = path.join(__dirname, '..', 'public', 'data.js');

const src = fs.readFileSync(SRC, 'utf8');
const i = src.indexOf('var JI = [');
if (i < 0) throw new Error('孩子端 index.html 里找不到 var JI');
const start = i + 'var JI = '.length;
const j = src.indexOf('\n  ];', start);
if (j < 0) throw new Error('找不到 JI 数组结尾');
const block = src.slice(start, j + 4);   // 到结尾的 ']' 为止，不带分号

const JI = eval('(' + block + ')');
if (!Array.isArray(JI) || JI.length !== 36) {
  throw new Error('条数不对，应为 36，实际 ' + (JI && JI.length));
}

const SC = { zoo: '动物园', hosp: '医院', school: '学校', shop: '商店' };
const seen = new Set();
const out = JI.map((r, k) => {
  if (r.length !== 4) throw new Error('第 ' + (k + 1) + ' 条字段数不对');
  const id = r[0] + ((k % 9) + 1);
  if (seen.has(id)) throw new Error('重复 id: ' + id);
  seen.add(id);
  return { id, scene: r[0], sceneName: SC[r[0]], name: r[1], say: r[2], tip: r[3] };
});

const head = `'use strict';
/*
 * 36 计台词库 —— 由 Baby36Server/tools/gen-data.js 从孩子端 index.html 自动导出。
 * 不要手改这个文件；改台词请改孩子端 JI 表，再重跑：
 *   node Baby36Server/tools/gen-data.js
 */
`;
fs.writeFileSync(OUT, head + 'window.BB36_JI = ' + JSON.stringify(out, null, 2) + ';\n', 'utf8');
console.log('导出 ' + out.length + ' 条 → ' + path.relative(ROOT, OUT));
console.log('前 3 条：' + out.slice(0, 3).map((x) => x.id + '/' + x.name).join('  '));
