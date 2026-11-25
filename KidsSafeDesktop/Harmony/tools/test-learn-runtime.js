/**
 * 在电脑上真跑 ArkTS 逻辑的测试台。
 *
 * 为什么需要它：判分（core/ExamGrade.ets）和出题（core/OralGen.ets）是纯逻辑，
 * 可它们写在 .ets 里，没有真机就执行不了 —— 只能靠人眼读、靠构建通过来自我安慰。
 * 而这两段恰恰是「错了最伤人」的：判分错了，孩子答对被判错；出题错了，
 * 孩子照着错的答案练。
 *
 * 办法：Node 22 的 --experimental-transform-types 能直接执行带类型注解的 TS。
 * 把 .ets 复制成 .ts（只改 import 路径），就能在电脑上跑**同一份源码**，
 * 不写第二份实现、不做假桩。
 *
 * 用法：node tools/test-learn-runtime.js
 * 产物：临时目录用完即删，不污染工程。
 */
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const ROOT = path.join(__dirname, '..');
const ETS = path.join(ROOT, 'entry', 'src', 'main', 'ets');
const RUNTIME = path.join(__dirname, 'runtime');

/** 需要搬进临时目录的源文件：ArkTS 源码 → 临时文件名。 */
const SOURCES = [
  { from: path.join(ETS, 'data', 'ExamData.ets'), to: 'ExamData.ts' },
  { from: path.join(ETS, 'data', 'MathBank.ets'), to: 'MathBank.ts' },
  { from: path.join(ETS, 'core', 'ExamGrade.ets'), to: 'ExamGrade.ts' },
  { from: path.join(ETS, 'core', 'OralGen.ets'), to: 'OralGen.ts' }
];

const TESTS = ['judge.ts', 'oral.ts'];

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'ks-learn-'));

function prepare() {
  for (const s of SOURCES) {
    let src = fs.readFileSync(s.from, 'utf8');
    // ArkTS 的 import 不带扩展名，ESM 必须带；顺便把相对路径压平（都在同一目录）
    src = src
      .replace(/from '\.\.\/data\/(\w+)'/g, "from './$1.ts'")
      .replace(/from '\.\.\/core\/(\w+)'/g, "from './$1.ts'")
      .replace(/from '\.\/(\w+)'/g, "from './$1.ts'");
    fs.writeFileSync(path.join(tmp, s.to), src, 'utf8');
  }
  for (const t of TESTS) {
    fs.copyFileSync(path.join(RUNTIME, t), path.join(tmp, t));
  }
}

function run(file) {
  const res = spawnSync(process.execPath,
    ['--experimental-transform-types', '--no-warnings', path.join(tmp, file)],
    { encoding: 'utf8' });
  const out = (res.stdout || '') + (res.stderr || '');
  return { code: res.status, out };
}

let failed = 0;
try {
  prepare();
  for (const t of TESTS) {
    const r = run(t);
    const lines = r.out.trim().split('\n');
    for (const line of lines) {
      // Node 的实验特性警告不占结论位置，只留业务输出
      if (line.includes('ExperimentalWarning') || line.includes('trace-warnings')) continue;
      console.log(line);
    }
    if (r.code !== 0) {
      failed++;
    }
    console.log('');
  }
} finally {
  try {
    fs.rmSync(tmp, { recursive: true, force: true });
  } catch (e) {
    // 删不掉就算了，是系统临时目录
  }
}

if (failed === 0) {
  console.log('运行时逻辑：判分与出题全部通过（跑的是鸿蒙端同一份源码）');
} else {
  console.log(`运行时逻辑：${failed} 个测试文件失败`);
  process.exitCode = 1;
}
