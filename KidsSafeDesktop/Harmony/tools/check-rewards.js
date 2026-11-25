'use strict';
/*
 * 星星上报 · 静态检查
 *
 * 每个离线游戏都靠一句 `window.KidsSafe.award('<key>', ...)` 把成绩交给桌面。
 * 这句话有三个容易出错的地方，而且都不会在浏览器里报错，只会「星星莫名其妙少了」：
 *   1. key 写错（复制别的游戏时忘了改）—— 星星记到别的模块头上；
 *   2. 没包 try/catch —— 在电脑上直接用浏览器打开（没有桥）会抛异常，
 *      而那行往往正好在结算逻辑里，游戏就卡在结算；
 *   3. 压根没写 —— 玩半天一颗星没有。
 *
 * 这三条都能静态扫出来，比一个个玩到通关快得多。
 *
 * 用法：node Harmony/tools/check-rewards.js
 */

const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..', 'entry/src/main/resources/rawfile/playland');

// 目录名 → 允许的 key（目录名和上报 key 不一定同名，这里显式写清楚）
const EXPECT = {
  xiangqi: 'xiangqi',
  gomoku: 'gomoku',
  sudoku: 'sudoku',
  game2048: 'game2048',
  game24: 'game24',
  pvz: 'pvz',
  snake: 'snake',
  chengyu: 'chengyu',
  baobei36: 'baobei36',
  virusfighter: 'virusfighter',
  // memory 移植自 kidrememberit（Cocos2d-x），threes 移植自 mr3
  memory: 'memory',
  threes: 'threes',
  // 词语组合 / 单词组合：两个游戏分开，各自上报自己的 key
  wordzh: 'wordzh',
  worden: 'worden',
  // mouse 移植自 mice_eat_cheese_android（Qt 版），按关卡上报
  mouse: 'mouse'
};

let fail = 0;
const rows = [];

for (const dir of Object.keys(EXPECT)) {
  const file = path.join(ROOT, dir, 'index.html');
  if (!fs.existsSync(file)) {
    rows.push([dir, false, '目录不存在']);
    fail++;
    continue;
  }
  const src = fs.readFileSync(file, 'utf8');
  const key = EXPECT[dir];
  const calls = src.match(/KidsSafe\.award\(\s*'([^']+)'/g) || [];
  if (calls.length === 0) {
    rows.push([dir, false, '没有任何上报']);
    fail++;
    continue;
  }
  const used = new Set(calls.map((c) => c.replace(/.*'([^']+)'$/, '$1')));
  if (!used.has(key)) {
    rows.push([dir, false, `上报 key 是 ${[...used].join(',')}，期望 ${key}`]);
    fail++;
    continue;
  }
  if (used.size > 1) {
    rows.push([dir, false, `混用了多个 key：${[...used].join(',')}`]);
    fail++;
    continue;
  }
  // 每一处调用（只认带括号的真调用，别把 if 里的探测也算进去）前面 400 字符内必须有 try
  let unguarded = 0;
  const idxs = [];
  let from = 0;
  for (;;) {
    const at = src.indexOf('KidsSafe.award(', from);
    if (at < 0) break;
    idxs.push(at);
    from = at + 1;
  }
  for (const at of idxs) {
    const head = src.slice(Math.max(0, at - 400), at);
    if (head.indexOf('try {') < 0) unguarded++;
  }
  if (unguarded > 0) {
    rows.push([dir, false, `${unguarded} 处调用没有 try 保护`]);
    fail++;
    continue;
  }
  rows.push([dir, true, `${idxs.length} 处上报 · key=${key}`]);
}

console.log('\n星星上报 · 静态检查\n');
for (const [dir, ok, msg] of rows) {
  console.log(`  ${ok ? '✓' : '✗'} ${dir.padEnd(14)} ${msg}`);
}
console.log(`\n  ${rows.length - fail}/${rows.length} 通过\n`);
process.exit(fail ? 1 : 0);
