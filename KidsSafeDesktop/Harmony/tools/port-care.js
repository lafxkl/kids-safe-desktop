/**
 * 把 KidsSafeShared/Models/CareData.cs 转成 ArkTS 数据文件 data/CareData.ets。
 *
 * 为什么用脚本：28 节心理小课堂、每节 4~6 段正文，全是中文长文本，
 * 手抄一遍必然漏字。而且这套内容是「写给孩子的心里话」，错一个字就很刺眼。
 *
 * 用法：node tools/port-care.js
 * 输入：D:/WorkBuddy/KidsSafeShared/Models/CareData.cs
 * 输出：entry/src/main/ets/data/CareData.ets
 */
const fs = require('fs');
const path = require('path');

const SRC = 'D:/WorkBuddy/KidsSafeShared/Models';
const OUT = path.join(__dirname, '..', 'entry', 'src', 'main', 'ets', 'data');

const q = (s) => JSON.stringify(s);
const arr = (list) => `[${list.map(q).join(', ')}]`;

/** C# 转义还原。注意顺序：先处理 \\ 会把 \" 变成 \\"，所以用一次性正则。 */
function unescapeCs(s) {
  return s.replace(/\\(["\\/nrt])/g, (_, c) => {
    if (c === 'n') return '\n';
    if (c === 'r') return '\r';
    if (c === 't') return '\t';
    return c;
  });
}

const src = fs.readFileSync(path.join(SRC, 'CareData.cs'), 'utf8');

/**
 * 从 `name =` 之后取出配平的一整段 { ... }。
 *
 * 不能按行的 `};` 截断：Lessons 里嵌套了 Sections 的 `new[] { ... }`，
 * 每一节也有自己的 `}`，按第一个 `};` 切只能拿到第一节。
 * 这里做一次真正的括号扫描，跳过字符串字面量里的括号。
 */
function sliceBraces(text, marker) {
  const at = text.indexOf(marker);
  if (at < 0) throw new Error('找不到 ' + marker);

  let i = text.indexOf('{', at);
  if (i < 0) throw new Error(marker + ' 后面没有 {');

  const start = i;
  let depth = 0;
  let inStr = false;
  for (; i < text.length; i++) {
    const c = text[i];
    if (inStr) {
      if (c === '\\') { i++; continue; }
      if (c === '"') inStr = false;
      continue;
    }
    if (c === '"') { inStr = true; continue; }
    if (c === '{') depth++;
    else if (c === '}') {
      depth--;
      if (depth === 0) return text.slice(start + 1, i);
    }
  }
  throw new Error(marker + ' 的括号没配平');
}

/** 在给定片段里按 `new() {` 或 `new Xxx {` 切出一个个对象体。 */
function objectBodies(text, opener) {
  const out = [];
  const re = new RegExp(opener.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g');
  let m;
  while ((m = re.exec(text)) !== null) {
    let i = m.index + m[0].length;
    // 从 `{` 开始配平
    let depth = 0;
    const start = i;
    let inStr = false;
    for (; i < text.length; i++) {
      const c = text[i];
      if (inStr) {
        if (c === '\\') { i++; continue; }
        if (c === '"') inStr = false;
        continue;
      }
      if (c === '"') { inStr = true; continue; }
      if (c === '{') depth++;
      else if (c === '}') {
        depth--;
        if (depth === 0) break;
      }
    }
    out.push(text.slice(start + 1, i));
    re.lastIndex = i + 1;
  }
  return out;
}

/** 取 `Name = "..."` 的字符串值。 */
function field(body, name) {
  const re = new RegExp('\\b' + name + '\\s*=\\s*"((?:[^"\\\\]|\\\\.)*)"');
  const m = body.match(re);
  return m ? unescapeCs(m[1]) : '';
}

function numField(body, name) {
  const re = new RegExp('\\b' + name + '\\s*=\\s*(-?\\d+)');
  const m = body.match(re);
  return m ? Number(m[1]) : 0;
}

// ───────────────────────── 心情 ─────────────────────────

function portMoods() {
  const block = sliceBraces(src, 'MoodKind[] Moods');
  const bodies = objectBodies(block, 'new()');
  const moods = bodies.map((b) => ({
    id: field(b, 'Id'),
    emoji: field(b, 'Emoji'),
    label: field(b, 'Label'),
    hint: field(b, 'Hint'),
    hex: field(b, 'Hex')
  }));
  for (const m of moods) {
    if (!m.id || !m.label) throw new Error('心情缺 id 或 label：' + JSON.stringify(m));
    if (!/^#[0-9A-Fa-f]{6}$/.test(m.hex)) throw new Error('心情颜色不是 #RRGGBB：' + m.hex);
  }
  if (moods.length !== 8) throw new Error('心情应为 8 个，实际 ' + moods.length);
  return moods;
}

// ───────────────────────── 暖心话 ─────────────────────────

function portWarmWords() {
  const block = sliceBraces(src, 'string[] WarmWords');
  const out = [];
  const re = /"((?:[^"\\]|\\.)*)"/g;
  let m;
  while ((m = re.exec(block)) !== null) out.push(unescapeCs(m[1]));
  if (out.length < 8) throw new Error('暖心话太少：' + out.length);
  return out;
}

// ───────────────────────── 求助渠道 ─────────────────────────

function portChannels() {
  const block = sliceBraces(src, 'HelpChannel[] Channels');
  const bodies = objectBodies(block, 'new()');
  const out = bodies.map((b) => ({
    emoji: field(b, 'Emoji'),
    name: field(b, 'Name'),
    number: field(b, 'Number'),
    when: field(b, 'When'),
    urgent: /\bUrgent\s*=\s*true/.test(b)
  }));
  for (const c of out) {
    if (!c.name || !c.when) throw new Error('求助渠道缺字段：' + JSON.stringify(c));
  }
  // 有号码的必须是纯数字（界面要拼 tel:），否则「拨打」按钮点了没反应
  for (const c of out) {
    if (c.number && !/^[\d-]+$/.test(c.number)) {
      throw new Error(`渠道「${c.name}」的号码不是纯数字：${c.number}`);
    }
  }
  return out;
}

// ───────────────────────── 呼吸步骤 ─────────────────────────

function portBreathing() {
  const at = src.indexOf('Breathing =');
  if (at < 0) throw new Error('找不到 Breathing');
  const end = src.indexOf('};', at);
  const block = src.slice(at, end);

  const out = [];
  // ("标题", "说明", 4),
  const re = /\("((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*,\s*(\d+)\)/g;
  let m;
  while ((m = re.exec(block)) !== null) {
    out.push({ title: unescapeCs(m[1]), detail: unescapeCs(m[2]), seconds: Number(m[3]) });
  }
  if (out.length !== 4) throw new Error('呼吸步骤应为 4 段，实际 ' + out.length);
  return out;
}

// ───────────────────────── 心理小课堂 ─────────────────────────

function portLessons() {
  const block = sliceBraces(src, 'List<CareLesson> Lessons');
  const bodies = objectBodies(block, 'new CareLesson');

  const lessons = bodies.map((b) => {
    // 只取到 Sections 之前的部分做字段匹配，避免正文里恰好出现 `Title = ` 之类
    const secAt = b.indexOf('Sections');
    const head = secAt >= 0 ? b.slice(0, secAt) : b;
    const secBody = secAt >= 0 ? b.slice(secAt) : '';

    const sections = [];
    const reSec = /\("((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\)/g;
    let m;
    while ((m = reSec.exec(secBody)) !== null) {
      sections.push({ heading: unescapeCs(m[1]), body: unescapeCs(m[2]) });
    }

    return {
      id: field(head, 'Id'),
      emoji: field(head, 'Emoji'),
      title: field(head, 'Title'),
      subtitle: field(head, 'Subtitle'),
      tag: field(head, 'Tag'),
      audience: field(head, 'Audience') || 'kids',
      minutes: numField(head, 'Minutes') || 3,
      keyLine: field(head, 'KeyLine'),
      sections
    };
  });

  // 这五项缺任何一项，页面上的「一节好课」就塌了，宁可生成时报错也不要带病输出
  for (const l of lessons) {
    const where = `课程「${l.title || l.id}」`;
    if (!l.id) throw new Error('有课程没有 Id');
    if (!l.title) throw new Error(where + ' 没有标题');
    if (!l.keyLine) throw new Error(where + ' 没有 KeyLine（详情页的提纲句）');
    if (l.sections.length === 0) throw new Error(where + ' 没有正文段落');
    for (const s of l.sections) {
      if (!s.heading || !s.body) throw new Error(where + ' 有段落缺标题或正文');
    }
    if (l.audience !== 'kids' && l.audience !== 'parents') {
      throw new Error(where + ' 的 Audience 只能是 kids / parents，实际 ' + l.audience);
    }
  }

  const ids = new Set();
  for (const l of lessons) {
    if (ids.has(l.id)) throw new Error('课程 id 重复：' + l.id);
    ids.add(l.id);
  }
  return lessons;
}

// ───────────────────────── 输出 ─────────────────────────

const moods = portMoods();
const warm = portWarmWords();
const channels = portChannels();
const breathing = portBreathing();
const lessons = portLessons();

const kidCount = lessons.filter((l) => l.audience === 'kids').length;
const parentCount = lessons.length - kidCount;

const lines = [];
const push = (s) => lines.push(s);

push('/**');
push(' * 关怀中心的内置内容：心情、暖心话、求助渠道、呼吸步骤、心理小课堂。');
push(' *');
push(' * 本文件由 tools/port-care.js 从 KidsSafeShared/Models/CareData.cs 生成，');
push(' * **不要手改**——内容在 C# 端维护，改完重跑脚本：node tools/port-care.js');
push(' *');
push(` * 心理小课堂 ${lessons.length} 节（给孩子 ${kidCount} 节 / 给家长 ${parentCount} 节）。`);
push(' *');
push(' * 写作原则（这套内容比代码重要，改文案时请守住）：');
push(' * 1) 不把「留守」写成缺陷。孩子不是问题儿童，只是家里的大人暂时不在身边；');
push(' * 2) 每一条都给出「现在就能做的一个动作」，不讲大道理；');
push(' * 3) 明确说「爸爸妈妈外出不是因为你不好」——这是最常见的自我归因；');
push(' * 4) 出现自伤念头必须指向真实可用的求助渠道，不能用「想开点」代替；');
push(' * 5) 家长篇不指责家长，给的是「15 分钟电话怎么打」这类能执行的动作。');
push(' */');
push('');

push('/** 一个心情选项。hex 用作圆点底色，浅色文字压在上面。 */');
push('export class MoodKind {');
push('  id: string = \'\';');
push('  emoji: string = \'🙂\';');
push('  label: string = \'\';');
push('  /** 一格提示，只在孩子点开时显示，主界面不铺满字 */');
push('  hint: string = \'\';');
push('  hex: string = \'#FFD54F\';');
push('}');
push('');
push('function mk(id: string, emoji: string, label: string, hint: string, hex: string): MoodKind {');
push('  const m: MoodKind = new MoodKind();');
push('  m.id = id;');
push('  m.emoji = emoji;');
push('  m.label = label;');
push('  m.hint = hint;');
push('  m.hex = hex;');
push('  return m;');
push('}');
push('');
push('export const MOODS: MoodKind[] = [');
for (const m of moods) {
  push(`  mk(${q(m.id)}, ${q(m.emoji)}, ${q(m.label)}, ${q(m.hint)}, ${q(m.hex)}),`);
}
push('];');
push('');
push('/** 找不到就退回「平静」——心情 id 是本地存档里读出来的，历史数据可能对不上。 */');
push('export function moodOf(id: string): MoodKind {');
push('  for (let i = 0; i < MOODS.length; i++) {');
push('    if (MOODS[i].id === id) {');
push('      return MOODS[i];');
push('    }');
push('  }');
push('  return MOODS[1];');
push('}');
push('');

push('/** 随机一句「温暖的话」，可以直接念出来。 */');
push('export const WARM_WORDS: string[] = [');
for (const w of warm) push(`  ${q(w)},`);
push('];');
push('');

push('/** 一个求助渠道。number 为空表示没有统一号码（学校老师 / 社区），只展示说明。 */');
push('export class HelpChannel {');
push('  emoji: string = \'📞\';');
push('  name: string = \'\';');
push('  number: string = \'\';');
push('  when: string = \'\';');
push('  /** 红色高亮：110 / 120 这种「马上会出事」才用 */');
push('  urgent: boolean = false;');
push('}');
push('');
push('function hc(emoji: string, name: string, number: string, when: string, urgent: boolean): HelpChannel {');
push('  const c: HelpChannel = new HelpChannel();');
push('  c.emoji = emoji;');
push('  c.name = name;');
push('  c.number = number;');
push('  c.when = when;');
push('  c.urgent = urgent;');
push('  return c;');
push('}');
push('');
push('export const CHANNELS: HelpChannel[] = [');
for (const c of channels) {
  push(`  hc(${q(c.emoji)}, ${q(c.name)}, ${q(c.number)}, ${q(c.when)}, ${c.urgent}),`);
}
push('];');
push('');

push('/** 4-7-8 呼吸的一段。seconds 是这一段持续几秒。 */');
push('export class BreathPhase {');
push('  title: string = \'\';');
push('  detail: string = \'\';');
push('  seconds: number = 4;');
push('}');
push('');
push('function bp(title: string, detail: string, seconds: number): BreathPhase {');
push('  const p: BreathPhase = new BreathPhase();');
push('  p.title = title;');
push('  p.detail = detail;');
push('  p.seconds = seconds;');
push('  return p;');
push('}');
push('');
push('/** 4-7-8：吸气 4 秒、屏住 7 秒、呼气 8 秒，最后一段是收尾提示不参与计时。 */');
push('export const BREATH_STEPS: BreathPhase[] = [');
for (const b of breathing) {
  push(`  bp(${q(b.title)}, ${q(b.detail)}, ${b.seconds}),`);
}
push('];');
push('');

push('/** 心理小课堂的一段正文。 */');
push('export class LessonSection {');
push('  heading: string = \'\';');
push('  body: string = \'\';');
push('}');
push('');
push('/** 一节课。audience 为 kids / parents，列表页按它分两个视图。 */');
push('export class CareLesson {');
push('  id: string = \'\';');
push('  emoji: string = \'💗\';');
push('  title: string = \'\';');
push('  subtitle: string = \'\';');
push('  /** 主题标签，例如「想念」「自我肯定」「同伴关系」 */');
push('  tag: string = \'\';');
push('  audience: string = \'kids\';');
push('  minutes: number = 3;');
push('  /** 全篇最重要的一句，详情页置顶显示 */');
push('  keyLine: string = \'\';');
push('  sections: LessonSection[] = [];');
push('}');
push('');
push('function ls(id: string, emoji: string, title: string, subtitle: string, tag: string,');
push('  audience: string, minutes: number, keyLine: string, sections: LessonSection[]): CareLesson {');
push('  const l: CareLesson = new CareLesson();');
push('  l.id = id;');
push('  l.emoji = emoji;');
push('  l.title = title;');
push('  l.subtitle = subtitle;');
push('  l.tag = tag;');
push('  l.audience = audience;');
push('  l.minutes = minutes;');
push('  l.keyLine = keyLine;');
push('  l.sections = sections;');
push('  return l;');
push('}');
push('');
push('function p(heading: string, body: string): LessonSection {');
push('  const s: LessonSection = new LessonSection();');
push('  s.heading = heading;');
push('  s.body = body;');
push('  return s;');
push('}');
push('');
push('export const LESSONS: CareLesson[] = [');

for (const l of lessons) {
  const paras = l.sections.map((s) => `p(${q(s.heading)}, ${q(s.body)})`).join(',\n      ');
  push('  ls(');
  push(`    ${q(l.id)}, ${q(l.emoji)}, ${q(l.title)}, ${q(l.subtitle)}, ${q(l.tag)},`);
  push(`    ${q(l.audience)}, ${l.minutes}, ${q(l.keyLine)},`);
  push('    [');
  push('      ' + paras);
  push('    ]');
  push('  ),');
}
push('];');
push('');
push('export function kidsLessons(): CareLesson[] {');
push('  return LESSONS.filter((l: CareLesson) => l.audience === \'kids\');');
push('}');
push('');
push('export function parentLessons(): CareLesson[] {');
push('  return LESSONS.filter((l: CareLesson) => l.audience === \'parents\');');
push('}');
push('');
push('export function findLesson(id: string): CareLesson | null {');
push('  for (let i = 0; i < LESSONS.length; i++) {');
push('    if (LESSONS[i].id === id) {');
push('      return LESSONS[i];');
push('    }');
push('  }');
push('  return null;');
push('}');
push('');

fs.mkdirSync(OUT, { recursive: true });
fs.writeFileSync(path.join(OUT, 'CareData.ets'), lines.join('\n'), 'utf8');

const chars = lines.join('\n').length;
console.log('生成完成：data/CareData.ets');
console.log(`  心情 ${moods.length} 个 / 暖心话 ${warm.length} 句 / 求助渠道 ${channels.length} 个 / 呼吸 ${breathing.length} 段`);
console.log(`  心理小课堂 ${lessons.length} 节（给孩子 ${kidCount} / 给家长 ${parentCount}）`);
console.log(`  共 ${chars} 字节`);
