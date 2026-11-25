/**
 * 关怀中心的「纯逻辑」运行时测试。
 *
 * 覆盖不到的部分（ArkUI 页面、TTS、拨号）只能真机看；
 * 但连续天数、同日覆盖、剪枝、信件上限这些是纯算法，可以在这里跑真的 Care.ets。
 *
 * 做法：把 Care.ets 复制成 .ts，只改三行 import 指向存根，
 * 用 Node 22 的 --experimental-transform-types 直接执行。
 * 跑的是鸿蒙端的同一份源码，不是另写一遍实现。
 *
 * 用法：node tools/test-care-runtime.js
 */
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const ROOT = path.join(__dirname, '..');
const ETS = path.join(ROOT, 'entry', 'src', 'main', 'ets');

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'care-'));

try {
  // ── 真数据：moodOf 要真的走一遍 MOODS ──
  fs.copyFileSync(path.join(ETS, 'data', 'CareData.ets'), path.join(tmp, 'CareData.ts'));

  // ── 存根：内存版 preferences ──
  fs.writeFileSync(path.join(tmp, 'StoreStub.ts'), [
    'const mem: Map<string, string> = new Map<string, string>();',
    '',
    'export const store = {',
    '  getString(key: string, def: string): string {',
    '    const v: string | undefined = mem.get(key);',
    '    return v === undefined ? def : v;',
    '  },',
    '  setString(key: string, value: string): void {',
    '    mem.set(key, value);',
    '  },',
    '  clear(): void {',
    '    mem.clear();',
    '  }',
    '};',
    ''
  ].join('\n'), 'utf8');

  // ── 存根：星星账本，只记「发了几次、每次几颗」 ──
  fs.writeFileSync(path.join(tmp, 'RewardsStub.ts'), [
    'export class RewardsStub {',
    '  calls: number[] = [];',
    '  scores: number[] = [];',
    '  grant(key: string, want: number, score: number, sub: string): number {',
    '    this.calls.push(want);',
    '    this.scores.push(score);',
    '    return want;',
    '  }',
    '}',
    '',
    'export const rewards = new RewardsStub();',
    ''
  ].join('\n'), 'utf8');

  // ── 被测源码：只改 import 路径 ──
  const careSrc = fs.readFileSync(path.join(ETS, 'core', 'Care.ets'), 'utf8');
  const patched = careSrc
    .replace("from './Store'", "from './StoreStub.ts'")
    .replace("from './Rewards'", "from './RewardsStub.ts'")
    .replace("from '../data/CareData'", "from './CareData.ts'");
  if (patched === careSrc) {
    throw new Error('Care.ets 的 import 没被替换，检查依赖是否变了');
  }
  fs.writeFileSync(path.join(tmp, 'Care.ts'), patched, 'utf8');

  // ── 测试正文 ──
  fs.writeFileSync(path.join(tmp, 'test.ts'), buildTest(), 'utf8');

  const run = spawnSync(process.execPath,
    ['--experimental-transform-types', '--no-warnings', path.join(tmp, 'test.ts')],
    { encoding: 'utf8', cwd: tmp });

  const out = (run.stdout || '') + (run.stderr || '');
  console.log(out.trimEnd());
  if (run.status !== 0) {
    process.exitCode = 1;
  }
} finally {
  try {
    fs.rmSync(tmp, { recursive: true, force: true });
  } catch (e) {
    /* 临时目录删不掉不影响结论 */
  }
}

function buildTest() {
  return `import { care, CareStore, dayKey, todayKey, shortDay } from './Care.ts';
import { store } from './StoreStub.ts';
import { rewards } from './RewardsStub.ts';
import { MOODS } from './CareData.ts';

let pass = 0;
const fails: string[] = [];

function ok(name: string, cond: boolean, detail?: string): void {
  if (cond) {
    pass++;
  } else {
    fails.push(detail ? name + ' —— ' + detail : name);
  }
}

/** 每个用例从空库开始，顺手把发星记录清零。 */
function fresh(): CareStore {
  store.clear();
  rewards.calls = [];
  rewards.scores = [];
  return new CareStore();
}

/** 用「几天前」造历史记录，和 Care.ets 里 dayBefore 的算法一致。 */
function daysAgo(n: number): string {
  const t: Date = new Date();
  return dayKey(new Date(t.getFullYear(), t.getMonth(), t.getDate() - n));
}

function seedMoods(days: number[], moodId: string): void {
  const list: object[] = [];
  for (let i = 0; i < days.length; i++) {
    list.push({ date: daysAgo(days[i]), moodId, note: '', time: '20:00' });
  }
  store.setString('care.moods.v1', JSON.stringify(list));
}

// ── 1. 空库 ──

const a = fresh();
ok('空库 todayEntry 为 null', a.todayEntry() === null);
ok('空库 streak 为 0', a.streak() === 0, String(a.streak()));
ok('空库 all 为空', a.all().length === 0);
const emptyRecent = a.recent(7);
ok('recent 补齐 7 格', emptyRecent.length === 7, String(emptyRecent.length));
ok('recent 全是 null', emptyRecent.every((e) => e === null));
ok('空库 letters 为空', a.letters().length === 0);
ok('空库 mostMoodId 为空串', a.mostMoodId() === '');

// ── 2. 记今天 ──

const b = fresh();
const saved1 = b.saveMood('happy', '');
ok('首次记录 firstToday', saved1.firstToday);
ok('首次记录发 1 颗星', saved1.stars === 1, String(saved1.stars));
ok('记账本收到一次调用', rewards.calls.length === 1, String(rewards.calls.length));
ok('首次记录后 streak 为 1', saved1.streak === 1, String(saved1.streak));
ok('todayEntry 能读回来', b.todayEntry() !== null);
ok('存的是刚点的那个心情', b.todayEntry()!.moodId === 'happy');

// ── 3. 同一天再记：覆盖但不发星 ──

const saved2 = b.saveMood('sad', '今天有点难受');
ok('同日重记 firstToday 为假', !saved2.firstToday);
ok('同日重记不发星', saved2.stars === 0, String(saved2.stars));
ok('同日重记不再调账本', rewards.calls.length === 1, String(rewards.calls.length));
ok('同日重记只有一条记录', b.all().length === 1, String(b.all().length));
ok('同日重记覆盖心情', b.todayEntry()!.moodId === 'sad');
ok('同日重记保存附言', b.todayEntry()!.note === '今天有点难受');

// ── 4. 连续天数 ──

const c = fresh();
seedMoods([1], 'calm');
ok('只记了昨天 → streak 1（今天没记不清零）', c.streak() === 1, String(c.streak()));

const d = fresh();
seedMoods([1, 2], 'calm');
ok('昨天 + 前天 → streak 2', d.streak() === 2, String(d.streak()));

const e = fresh();
seedMoods([2], 'calm');
ok('前天有、昨天断 → streak 0', e.streak() === 0, String(e.streak()));

const f = fresh();
seedMoods([0, 1, 2, 3], 'calm');
ok('连着 4 天（含今天）→ streak 4', f.streak() === 4, String(f.streak()));

const g = fresh();
seedMoods([1, 2, 3, 5], 'calm');
ok('中间跳过一天就断', g.streak() === 3, String(g.streak()));

// ── 5. 剪枝 ──

const h = fresh();
seedMoods([200, 190, 30], 'calm');
h.saveMood('happy', '');
ok('超过 180 天的记录被剪掉', h.all().length === 2, String(h.all().length));
ok('剪完还留着 30 天前那条', h.all().some((x) => x.date === daysAgo(30)));
ok('今天那条也在', h.todayEntry() !== null);

// ── 6. 坏存档 ──

const i = fresh();
store.setString('care.moods.v1', '{ 这不是 JSON');
ok('坏 JSON 不崩', i.all().length === 0);
ok('坏 JSON 后 streak 仍可算', i.streak() === 0);

const j = fresh();
store.setString('care.moods.v1', JSON.stringify([
  { date: '', moodId: 'happy' },
  { date: '2026-01-01', moodId: '' },
  { date: daysAgo(1), moodId: 'calm', note: '', time: '08:00' }
]));
ok('日期或心情缺失的记录被丢弃', j.all().length === 1, String(j.all().length));
ok('留下的那条是合法的', j.all()[0].moodId === 'calm');

// ── 7. 统计 ──

const k = fresh();
seedMoods([0, 1, 2, 3], 'lonely');
// 注意：saveMood 会**覆盖**今天那条 lonely，所以 lonely 最终只有 3 条
k.saveMood('happy', '');
ok('mostMoodId 取出现最多的', k.mostMoodId() === 'lonely', k.mostMoodId());
ok('countOf 数得准', k.countOf('lonely') === 3, String(k.countOf('lonely')));
ok('recordedIn(14) 算得准', k.recordedIn(14) === 4, String(k.recordedIn(14)));

// ── 8. 心情 id 与文案 ──

ok('MOODS 里每个 id 都能找到', MOODS.every((m) => m.id.length > 0 && m.label.length > 0));

// ── 9. 信件 ──

const l = fresh();
l.addLetter('妈妈我想你了');
l.addLetter('我今天考了 95 分');
l.addLetter('   ');
ok('空白的信不存', l.letters().length === 2, String(l.letters().length));
ok('最新的排在最前面', l.letters()[0].text === '我今天考了 95 分', l.letters()[0].text);
ok('letterCount 与现实一致', l.letterCount() === 2, String(l.letterCount()));

l.removeLetter(0);
ok('删掉最新一条后只剩一条', l.letters().length === 1, String(l.letters().length));
ok('删的是最新的那条', l.letters()[0].text === '妈妈我想你了', l.letters()[0].text);

l.removeLetter(9);
ok('越界删除不崩', l.letters().length === 1);

const m = fresh();
for (let n = 0; n < 105; n++) {
  m.addLetter('第 ' + n + ' 句');
}
ok('最多留 100 句', m.letters().length === 100, String(m.letters().length));
ok('超过上限时删最旧的', m.letters()[0].text === '第 104 句', m.letters()[0].text);
ok('最早的那句被挤掉了', !m.letters().some((x) => x.text === '第 0 句'));

// ── 10. 日期工具 ──

ok('dayKey 补零', /^\\d{4}-\\d{2}-\\d{2}$/.test(todayKey()), todayKey());
ok('shortDay 今天显示「今天」', shortDay(todayKey()) === '今天', shortDay(todayKey()));
ok('shortDay 其他日期显示月日', shortDay('2026-03-04') === '3/4', shortDay('2026-03-04'));

// ── 11. 同一天跨零点 ──

const n24 = fresh();
seedMoods([1], 'calm');
const crossed = n24.saveMood('happy', '');
ok('昨天记过、今天记 → streak 2', crossed.streak === 2, String(crossed.streak));
ok('跨零点后仍是首次记录', crossed.firstToday);

console.log('');
if (fails.length === 0) {
  console.log('关怀中心运行时：通过 ' + pass + ' 项');
} else {
  console.log('关怀中心运行时：通过 ' + pass + ' 项，失败 ' + fails.length + ' 项');
  for (const f of fails) console.log('  ✗ ' + f);
  process.exitCode = 1;
}
`;
}
