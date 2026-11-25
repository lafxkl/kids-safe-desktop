/**
 * 把 KidsSafeShared 里的静态内容（古诗 / 安全知识）机械转成 ArkTS 数据文件。
 *
 * 为什么要写脚本而不是手抄：古诗 26 首、安全贴士 60 多条，全是中文长文本，
 * 手抄一遍必然漏字错字，而且以后 C# 端改了内容，重新跑一次就能同步。
 * 口诀乐园的数据是代码算出来的（九九表），没法机械转换，见 data/RhymeData.ets。
 *
 * 用法：node tools/port-content.js
 * 输入：D:/WorkBuddy/KidsSafeShared/Models/{PoemData,SafetyKnowledgeData}.cs
 * 输出：entry/src/main/ets/data/{PoemData,SafetyData}.ets
 */
const fs = require('fs');
const path = require('path');

const SRC = 'D:/WorkBuddy/KidsSafeShared/Models';
const OUT = path.join(__dirname, '..', 'entry', 'src', 'main', 'ets', 'data');

/** 统一用 JSON 双引号字面量：中文原样保留，引号/反斜杠自动转义。 */
const q = (s) => JSON.stringify(s);
const arr = (list) => `[${list.map(q).join(', ')}]`;

/** C# 字符串里的 \" \\ \n 还原成真实字符，再交给 JSON.stringify 转义。 */
function unescapeCs(s) {
  return s.replace(/\\(["\\/nrt])/g, (_, c) => {
    if (c === 'n') return '\n';
    if (c === 'r') return '\r';
    if (c === 't') return '\t';
    return c;
  });
}

function read(name) {
  return fs.readFileSync(path.join(SRC, name), 'utf8');
}

// ───────────────────────── 古诗 ─────────────────────────
function portPoems() {
  const src = read('PoemData.cs');

  // 分类常量：public const string CategoryNature = "山水田园";
  const catMap = {};
  const reConst = /public const string (\w+) = "([^"]*)";/g;
  let m;
  while ((m = reConst.exec(src)) !== null) catMap[m[1]] = m[2];

  // 分类展示顺序
  const orderBlock = src.match(/Categories\s*=\s*\[([\s\S]*?)\];/);
  const orderKeys = orderBlock[1].split(',').map((s) => s.trim()).filter(Boolean);
  const order = orderKeys.map((k) => catMap[k]).filter(Boolean);

  // 每首诗一个块。不能用「以 ,} 收尾」来切：C# 集合表达式允许最后一项不带逗号，
  // 那样会把最后一首（竹石）漏掉。改成从 new Poem { 起，切到第一行 8 空格缩进的 }。
  const blocks = src.split(/new Poem\s*\{/).slice(1);
  const poems = [];
  for (const raw of blocks) {
    const b = raw.split(/\n {8}\}/)[0];
    const pick = (re) => {
      const r = b.match(re);
      return r ? r[1] : '';
    };
    const list = (re) => {
      const r = b.match(re);
      if (!r) return [];
      const out = [];
      const reS = /"([^"]*)"/g;
      let s;
      while ((s = reS.exec(r[1])) !== null) out.push(s[1]);
      return out;
    };
    poems.push({
      title: pick(/Title = "([\s\S]*?)"/),
      author: pick(/Author = "([\s\S]*?)"/),
      dynasty: pick(/Dynasty = "([\s\S]*?)"/),
      category: catMap[pick(/Category = (\w+)/)] || '',
      lines: list(/Lines\s*=\s*\[([\s\S]*?)\]/),
      notes: list(/Notes\s*=\s*\[([\s\S]*?)\]/),
      translation: pick(/Translation = "([\s\S]*?)"/),
      appreciation: pick(/Appreciation = "([\s\S]*?)"/)
    });
  }

  const missing = poems.filter((p) => !p.title || p.lines.length === 0);
  if (missing.length > 0) {
    throw new Error(`有 ${missing.length} 首诗解析失败，检查正则`);
  }

  const cats = order.map((c) => `export const CAT_${catIndexName(c)}: string = ${q(c)};`).join('\n');

  const body = poems
    .map((p) => `  poem(${q(p.title)}, ${q(p.author)}, ${q(p.dynasty)}, ${q(p.category)},\n` +
      `    ${arr(p.lines)},\n` +
      `    ${arr(p.notes)},\n` +
      `    ${q(p.translation)},\n` +
      `    ${q(p.appreciation)})`)
    .join(',\n');

  const out = `/**
 * 古诗学习的内置内容（离线，不联网）。
 *
 * 本文件由 tools/port-content.js 从 KidsSafeShared/Models/PoemData.cs 机械生成，
 * **不要手改**——C# 端增删古诗后重新跑一次脚本即可。
 * 生成时间：${new Date().toISOString().slice(0, 10)}
 */

/** 一首古诗。字段与 C# 的 Poem 一一对应。 */
export class Poem {
  title: string = '';
  author: string = '';
  dynasty: string = '';
  /** 主题分类，取值见下方 CAT_* 常量。 */
  category: string = '';
  /** 正文，按句分行：朗读时用逗号连起来，断句才自然。 */
  lines: string[] = [];
  /** 注释，每条形如「字词 —— 解释」。 */
  notes: string[] = [];
  /** 白话译文。 */
  translation: string = '';
  /** 一句话赏析 / 小知识。 */
  appreciation: string = '';

  /** 列表卡片上的「朝代 · 作者」。 */
  authorLine(): string {
    return \`\${this.dynasty} · \${this.author}\`;
  }

  /** 正文前两句，列表预览用。末尾的句号逗号先去掉再补省略号，否则会出现「。。……」 */
  preview(): string {
    let tail: string = this.lines.slice(0, 2).join('，');
    while (tail.length > 0) {
      const c: string = tail.charAt(tail.length - 1);
      if (c === '。' || c === '，') {
        tail = tail.substring(0, tail.length - 1);
      } else {
        break;
      }
    }
    return tail + '……';
  }

  /** 朗读全文。标题、作者在前，正文用逗号连读，比逐行读更连贯。 */
  speakText(): string {
    return \`\${this.title}，\${this.author}。\${this.lines.join('，')}\`;
  }
}

function poem(title: string, author: string, dynasty: string, category: string,
  lines: string[], notes: string[], translation: string, appreciation: string): Poem {
  const p: Poem = new Poem();
  p.title = title;
  p.author = author;
  p.dynasty = dynasty;
  p.category = category;
  p.lines = lines;
  p.notes = notes;
  p.translation = translation;
  p.appreciation = appreciation;
  return p;
}

${cats}

/** 分类展示顺序，与 C# 端 Categories 数组一致。 */
export const POEM_CATEGORIES: string[] = ${arr(order)};

/** 分类对应的 emoji（列表卡片左侧圆的图案）。 */
export function poemEmoji(category: string): string {
  const table: Map<string, string> = new Map<string, string>();
  table.set(CAT_NATURE, '⛰️');
  table.set(CAT_SEASON, '🌸');
  table.set(CAT_CHILD, '🧒');
  table.set(CAT_MISSING, '🌙');
  table.set(CAT_SPIRIT, '🌱');
  return table.get(category) ?? '📜';
}

/** 分类主题色（卡片左侧圆的底色 / 详情页分类标签）。 */
export function poemColor(category: string): string {
  const table: Map<string, string> = new Map<string, string>();
  table.set(CAT_NATURE, '#26A69A');
  table.set(CAT_SEASON, '#EC407A');
  table.set(CAT_CHILD, '#FB8C00');
  table.set(CAT_MISSING, '#7E57C2');
  table.set(CAT_SPIRIT, '#43A047');
  return table.get(category) ?? '#8D6E63';
}

export const POEMS: Poem[] = [
${body}
];

/** 按分类筛选；category 为空或「全部」时返回全部。 */
export function poemsByCategory(category: string): Poem[] {
  if (category.length === 0 || category === '全部') {
    return POEMS;
  }
  return POEMS.filter((p: Poem) => p.category === category);
}

/** 某分类下的首数，用于分类 chip 上的计数。 */
export function poemCount(category: string): number {
  return poemsByCategory(category).length;
}
`;
  return out;
}

/** 分类名转常量后缀：山水田园 → NATURE… 这里只是要个稳定的英文名。 */
function catIndexName(category) {
  const table = {
    '四季风光': 'SEASON',
    '山水田园': 'NATURE',
    '童趣生活': 'CHILD',
    '思念·送别': 'MISSING',
    '励志哲理': 'SPIRIT'
  };
  return table[category] || category;
}

// ───────────────────────── 安全知识 ─────────────────────────
function portSafety() {
  const src = read('SafetyKnowledgeData.cs');
  const chunks = src.split(/new SafetyTopic\s*\{/).slice(1);

  const topics = chunks.map((c) => {
    const head = c.match(/Emoji = "([^"]*)",\s*Title = "([^"]*)",\s*Color = "([^"]*)"/);
    const tips = [];
    const reTip = /new\(\)\s*\{\s*Emoji = "([^"]*)",\s*Text = "([^"]*)"\s*\}/g;
    let m;
    while ((m = reTip.exec(c)) !== null) tips.push({ emoji: m[1], text: m[2] });
    return { emoji: head[1], title: head[2], color: head[3], tips };
  });

  for (const t of topics) {
    if (t.tips.length === 0) throw new Error(`主题「${t.title}」没解析到贴士`);
  }

  const body = topics
    .map((t) => `  topic(${q(t.emoji)}, ${q(t.title)}, ${q(t.color)}, [\n` +
      t.tips.map((x) => `    tip(${q(x.emoji)}, ${q(x.text)})`).join(',\n') +
      `\n  ])`)
    .join(',\n');

  const total = topics.reduce((n, t) => n + t.tips.length, 0);

  return `/**
 * 安全知识的内置内容（离线）：${topics.length} 个主题、${total} 条贴士。
 *
 * 本文件由 tools/port-content.js 从 KidsSafeShared/Models/SafetyKnowledgeData.cs 生成，
 * **不要手改**——内容在 C# 端维护，改完重跑脚本。
 */

/** 一条贴士：emoji 图标 + 一句大白话。 */
export class SafetyTip {
  emoji: string = '💡';
  text: string = '';
}

/** 一个主题（交通安全、防骗防拐……），含若干贴士。 */
export class SafetyTopic {
  emoji: string = '🛡️';
  title: string = '';
  /** 主题色，用于卡片描边和标题。 */
  color: string = '#E53935';
  tips: SafetyTip[] = [];
}

function tip(emoji: string, text: string): SafetyTip {
  const t: SafetyTip = new SafetyTip();
  t.emoji = emoji;
  t.text = text;
  return t;
}

function topic(emoji: string, title: string, color: string, tips: SafetyTip[]): SafetyTopic {
  const t: SafetyTopic = new SafetyTopic();
  t.emoji = emoji;
  t.title = title;
  t.color = color;
  t.tips = tips;
  return t;
}

export const SAFETY_TOPICS: SafetyTopic[] = [
${body}
];

/** 贴士总条数，首页/列表上显示用。 */
export function safetyTipCount(): number {
  let n = 0;
  for (const t of SAFETY_TOPICS) {
    n += t.tips.length;
  }
  return n;
}
`;
}

// ───────────────────────── 安全小问答 ─────────────────────────
function portSafetyQuiz() {
  const src = read('SafetyQuizData.cs');

  /*
   * 一次匹配一整题，而不是「按 new() { 切块」。
   * 踩过的坑：选项本身也是 `Options = new() { ... }`，按 new() { 切会把
   * 选项数组的开头切走，结果每题都只剩题干、选项全空 —— 而且不报错，
   * 生成出来的题库每道题都「无选项」，跑起来才发现。
   * 题库格式是全仓库统一的（Emoji, Category, Question, Options, CorrectIndex, Explain 定序），
   * 所以一条完整正则最稳；顺带用 CorrectIndex 的出现次数做总数核对。
   */
  const reQuestion = new RegExp(
    'new\\(\\)\\s*\\{\\s*Emoji\\s*=\\s*"((?:[^"\\\\]|\\\\.)*)",\\s*' +
    'Category\\s*=\\s*"((?:[^"\\\\]|\\\\.)*)",\\s*' +
    'Question\\s*=\\s*"((?:[^"\\\\]|\\\\.)*)",\\s*' +
    'Options\\s*=\\s*new\\(\\)\\s*\\{([\\s\\S]*?)\\},\\s*' +
    'CorrectIndex\\s*=\\s*(\\d+),\\s*' +
    'Explain\\s*=\\s*"((?:[^"\\\\]|\\\\.)*)"',
    'g'
  );

  const questions = [];
  let m;
  while ((m = reQuestion.exec(src)) !== null) {
    const options = [];
    const reOpt = /"((?:[^"\\]|\\.)*)"/g;
    let o;
    while ((o = reOpt.exec(m[4])) !== null) options.push(unescapeCs(o[1]));

    questions.push({
      emoji: unescapeCs(m[1]) || '🛡️',
      category: unescapeCs(m[2]) || '通用',
      question: unescapeCs(m[3]),
      options,
      correct: Number(m[5]),
      explain: unescapeCs(m[6])
    });
  }

  const declared = (src.match(/CorrectIndex\s*=/g) || []).length;
  if (questions.length !== declared) {
    throw new Error(`安全问答只解析到 ${questions.length} 题，源文件有 ${declared} 题 —— 格式可能变了`);
  }
  if (questions.length === 0) throw new Error('安全问答一道题都没解析到');

  for (const qt of questions) {
    if (!qt.question) throw new Error('安全问答有题干为空');
    if (qt.options.length < 2) throw new Error(`题目「${qt.question}」选项少于 2 个`);
    if (qt.correct < 0 || qt.correct >= qt.options.length) {
      throw new Error(`题目「${qt.question}」的 CorrectIndex 越界`);
    }
  }

  const body = questions.map((x) =>
    `  sq(${q(x.emoji)}, ${q(x.category)}, ${q(x.question)},\n` +
    `    ${arr(x.options)}, ${x.correct}, ${q(x.explain)})`
  ).join(',\n');

  return `/**
 * 安全小问答题库（离线）：${questions.length} 道题，覆盖交通 / 防拐 / 防火 / 用电 / 溺水 / 饮食 /
 * 灾害 / 网络 / 急救 / 自我保护。
 *
 * 本文件由 tools/port-content.js 从 KidsSafeShared/Models/SafetyQuizData.cs 生成，
 * **不要手改**——题目在 C# 端维护，改完重跑脚本。
 *
 * 和「安全知识」是两套东西：那边是「读的贴士」，这边是「答的题」。
 * 两边的分类名刻意保持一致，孩子读完某个主题再做题能对上号。
 */

export class SafetyQuizQuestion {
  emoji: string = '🛡️';
  category: string = '通用';
  question: string = '';
  options: string[] = [];
  /** 正确选项在 options 里的下标（界面会打乱选项，靠这个标记） */
  correct: number = 0;
  /** 答完必看的讲解，对错都给 */
  explain: string = '';
}

function sq(emoji: string, category: string, question: string, options: string[],
  correct: number, explain: string): SafetyQuizQuestion {
  const e: SafetyQuizQuestion = new SafetyQuizQuestion();
  e.emoji = emoji;
  e.category = category;
  e.question = question;
  e.options = options;
  e.correct = correct;
  e.explain = explain;
  return e;
}

export const SAFETY_QUIZ: SafetyQuizQuestion[] = [
${body}
];
`;
}

// ───────────────────────── 主流程 ─────────────────────────
fs.mkdirSync(OUT, { recursive: true });
fs.writeFileSync(path.join(OUT, 'PoemData.ets'), portPoems(), 'utf8');
fs.writeFileSync(path.join(OUT, 'SafetyData.ets'), portSafety(), 'utf8');
fs.writeFileSync(path.join(OUT, 'SafetyQuiz.ets'), portSafetyQuiz(), 'utf8');
console.log('生成完成：');
console.log('  data/PoemData.ets');
console.log('  data/SafetyData.ets');
console.log('  data/SafetyQuiz.ets');
