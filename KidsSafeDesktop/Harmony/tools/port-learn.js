/**
 * 把 C# 端的题库种子（SQL）机械转成 ArkTS 数据文件。
 *
 * 为什么要转而不是手抄：模拟考 21 套卷 252 道题、口算 5~9 年级 400 多道题，
 * 全是中文长文本与答案，手抄一遍必然漏字错字；而且 C# 端题库以后要补题，
 * 重跑一次脚本就能同步，不用人肉核对。
 *
 * 用法：node tools/port-learn.js
 * 输入：D:/WorkBuddy/KidsSafeDesktop/Resources/Raw/questbank/{seed_exam,seed_math}.sql
 * 输出：entry/src/main/ets/data/{ExamData,MathBank}.ets
 *
 * 解析思路：不引 SQL 引擎，就按 SQL 字面量规则逐字符扫元组。
 * 单引号字符串里的 '' 是转义的单引号（题干里大量「」和少数 '），必须还原成一个 '。
 */
const fs = require('fs');
const path = require('path');

const SRC = 'D:/WorkBuddy/KidsSafeDesktop/Resources/Raw/questbank';
const OUT = path.join(__dirname, '..', 'entry', 'src', 'main', 'ets', 'data');

// ───────────────────────── SQL 值解析 ─────────────────────────

/**
 * 把 `(1,'g3-chinese',3,...),(2,...)` 这样的一串解析成二维数组。
 * 只支持本项目题库用到的两种类型：整数/小数 与 单引号字符串。
 */
function parseTuples(text) {
  const rows = [];
  let i = 0;
  const n = text.length;

  while (i < n) {
    // 找元组起点
    while (i < n && text[i] !== '(') i++;
    if (i >= n) break;
    i++; // 跳过 (

    const row = [];
    let val = '';
    let quoted = false;
    let isString = false;
    let done = false;

    while (i < n && !done) {
      const c = text[i];

      if (quoted) {
        if (c === "'") {
          if (text[i + 1] === "'") {
            // '' → 一个单引号
            val += "'";
            i += 2;
            continue;
          }
          quoted = false;
          i++;
          continue;
        }
        val += c;
        i++;
        continue;
      }

      if (c === "'") {
        quoted = true;
        isString = true;
        i++;
        continue;
      }

      if (c === ',') {
        row.push(isString ? val : toNumber(val));
        val = '';
        isString = false;
        i++;
        continue;
      }

      if (c === ')') {
        row.push(isString ? val : toNumber(val));
        i++;
        done = true;
        continue;
      }

      val += c;
      i++;
    }

    if (done) rows.push(row);
  }

  return rows;
}

function toNumber(raw) {
  const t = raw.trim();
  if (t.length === 0) return null;
  const v = Number(t);
  return Number.isNaN(v) ? t : v;
}

/**
 * 取出所有 `INSERT INTO <table> ... VALUES ...;` 语句里的值区段，拼成一段。
 *
 * 注意不能只取第一条：题库的写法是一套卷一条 INSERT（21 条），
 * 口算也是一行一条 INSERT（几十条），只取第一条会得到「1 套卷 12 题」这种假结果 ——
 * 而且看起来还挺像对的，最容易漏。
 */
function valuesOf(sql, table) {
  const re = new RegExp('INSERT\\s+INTO\\s+' + table + '[\\s\\S]*?VALUES', 'gi');
  const parts = [];
  let m;
  while ((m = re.exec(sql)) !== null) {
    const start = m.index + m[0].length;
    const end = sql.indexOf(';', start);
    if (end < 0) throw new Error('INSERT INTO ' + table + ' 缺少结尾分号');
    parts.push(sql.slice(start, end));
    re.lastIndex = end;
  }
  if (parts.length === 0) throw new Error('找不到 INSERT INTO ' + table);
  return parts.join(',');
}

// ───────────────────────── 数据装配 ─────────────────────────

function readSql(name) {
  return fs.readFileSync(path.join(SRC, name), 'utf8');
}

function buildExam() {
  const sql = readSql('seed_exam.sql');

  const paperRows = parseTuples(valuesOf(sql, 'exam_paper'));
  const qRows = parseTuples(valuesOf(sql, 'exam_question'));

  // (id, code, grade, subject, title, term, duration_min, note)
  const papers = paperRows.map((r) => ({
    id: r[0],
    code: r[1],
    grade: r[2],
    subject: r[3],
    title: r[4],
    term: r[5],
    minutes: r[6],
    note: r[7],
    questions: []
  }));

  const byId = new Map();
  for (const p of papers) byId.set(p.id, p);

  for (const r of qRows) {
    // (id, paper_id, ordinal, kind, stem, options, answer, explain)
    const q = {
      ordinal: r[2],
      kind: r[3],
      stem: r[4],
      options: r[5] ? String(r[5]).split('|') : [],
      answer: r[6],
      explain: r[7]
    };
    const paper = byId.get(r[1]);
    if (!paper) throw new Error('题目 ' + r[0] + ' 指向不存在的试卷 ' + r[1]);
    paper.questions.push(q);
  }

  for (const p of papers) {
    p.questions.sort((a, b) => a.ordinal - b.ordinal);
  }

  return papers;
}

function buildMath() {
  const sql = readSql('seed_math.sql');
  const rows = parseTuples(valuesOf(sql, 'math_problem'));
  // (grade, topic, category, expression, answer)
  return rows.map((r) => ({
    grade: r[0],
    topic: r[1],
    category: r[2],
    expression: r[3],
    answer: r[4]
  }));
}

// ───────────────────────── 生成 ArkTS ─────────────────────────

const q = (s) => JSON.stringify(String(s));

function emitExam(papers) {
  const subjects = { chinese: '语文', math: '数学', english: '英语' };

  const paperText = papers.map((p) => {
    const qs = p.questions.map((item) => {
      const opts = item.options.length > 0
        ? `[${item.options.map(q).join(', ')}]`
        : '[]';
      return `    q(${item.ordinal}, ${q(item.kind)}, ${q(item.stem)},\n` +
        `      ${opts},\n` +
        `      ${q(item.answer)}, ${q(item.explain)})`;
    }).join(',\n');

    return `  p(${p.id}, ${p.grade}, ${q(p.subject)}, ${q(p.title)}, ${q(p.term)}, ${p.minutes}, ${q(p.note)}, [\n` +
      `${qs}\n  ])`;
  }).join(',\n\n');

  return `/**
 * 模拟考试题库：3~9 年级 × 语文 / 数学 / 英语，每年级每科一套期末模拟卷。
 *
 * ⚠ 本文件由 tools/port-learn.js 从 Resources/Raw/questbank/seed_exam.sql 生成，不要手改。
 *    C# 端补题之后重跑一次脚本即可同步。
 *
 * 为什么在鸿蒙端做成常量数组而不是像 C# 那样建 SQLite：
 *   ① 题量只有两百多道，一次读进内存也就是几十 KB，查询用不到 SQL；
 *   ② 鸿蒙端要做到「装上就能考」，不能依赖任何建库/迁移步骤；
 *   ③ 题型只有三种、筛选条件只有「年级 + 科目」，数组过滤比 SQL 更直白。
 */

/** 题型。和 C# 的 ExamQuestionKind 一一对应。 */
export enum QKind {
  Choice = 'choice',
  Fill = 'fill',
  Judge = 'judge'
}

/** 一道题。options 只对选择题有值；判断题的「对 / 错」由界面生成，库里只存答案。 */
export class ExamQuestion {
  /** 卷内题号，从 1 开始 */
  ordinal: number = 0;
  kind: string = QKind.Choice;
  stem: string = '';
  options: string[] = [];
  /** 选择题是 A~D；判断题是「对」/「错」；填空题可能多个等价写法用 | 分隔 */
  answer: string = '';
  explain: string = '';
}

export class ExamPaper {
  id: number = 0;
  grade: number = 0;
  /** chinese / math / english */
  subject: string = 'chinese';
  title: string = '';
  term: string = '';
  minutes: number = 60;
  note: string = '';
  questions: ExamQuestion[] = [];
}

function q(ordinal: number, kind: string, stem: string, options: string[],
  answer: string, explain: string): ExamQuestion {
  const e: ExamQuestion = new ExamQuestion();
  e.ordinal = ordinal;
  e.kind = kind;
  e.stem = stem;
  e.options = options;
  e.answer = answer;
  e.explain = explain;
  return e;
}

function p(id: number, grade: number, subject: string, title: string, term: string,
  minutes: number, note: string, questions: ExamQuestion[]): ExamPaper {
  const e: ExamPaper = new ExamPaper();
  e.id = id;
  e.grade = grade;
  e.subject = subject;
  e.title = title;
  e.term = term;
  e.minutes = minutes;
  e.note = note;
  e.questions = questions;
  return e;
}

export const EXAM_PAPERS: ExamPaper[] = [
${paperText}
];

/** 某年级的卷子（语文 → 数学 → 英语）。没有就返回空数组，页面据此提示。 */
export function papersOf(grade: number): ExamPaper[] {
  const out: ExamPaper[] = [];
  for (let i = 0; i < EXAM_PAPERS.length; i++) {
    if (EXAM_PAPERS[i].grade === grade) {
      out.push(EXAM_PAPERS[i]);
    }
  }
  // 按科目固定顺序排，不依赖 SQL 里的插入顺序
  const order: string[] = ['chinese', 'math', 'english'];
  out.sort((a: ExamPaper, b: ExamPaper) => order.indexOf(a.subject) - order.indexOf(b.subject));
  return out;
}

export function subjectName(subject: string): string {
  if (subject === 'chinese') {
    return '${subjects.chinese}';
  }
  if (subject === 'math') {
    return '${subjects.math}';
  }
  return '${subjects.english}';
}

export function subjectEmoji(subject: string): string {
  if (subject === 'chinese') {
    return '📖';
  }
  if (subject === 'math') {
    return '🧮';
  }
  return '🔤';
}

/** 科目主色。语文用红、数学用蓝、英语用绿，和其他板块的配色区分开。 */
export function subjectColor(subject: string): string {
  if (subject === 'chinese') {
    return '#EF5350';
  }
  if (subject === 'math') {
    return '#29B6F6';
  }
  return '#66BB6A';
}

export function kindName(kind: string): string {
  if (kind === QKind.Fill) {
    return '填空题';
  }
  if (kind === QKind.Judge) {
    return '判断题';
  }
  return '单选题';
}

export function kindEmoji(kind: string): string {
  if (kind === QKind.Fill) {
    return '✏️';
  }
  if (kind === QKind.Judge) {
    return '⚖️';
  }
  return '🔘';
}
`;
}

function emitMath(bank) {
  const rows = bank.map((m) =>
    `  m(${m.grade}, ${q(m.topic)}, ${q(m.category)}, ${q(m.expression)}, ${m.answer})`
  ).join(',\n');

  return `/**
 * 五~九年级口算题库（小数 / 分数 / 有理数 / 方程 / 根式）。
 *
 * ⚠ 本文件由 tools/port-learn.js 从 Resources/Raw/questbank/seed_math.sql 生成，不要手改。
 *
 * 一~四年级不在这里：那四个年级是纯四则，由 core/OralGen.ets 现场出题，
 * 可以无限不重复；五~九年级题型发散（小数乘除、解方程、平方根），
 * 现场生成容易出错题，改用整好的固定题库。
 */

export class MathProblem {
  grade: number = 0;
  /** add / sub / mul / div / mix，对应 OralTopic */
  topic: string = 'add';
  category: string = '';
  expression: string = '';
  answer: number = 0;
}

function m(grade: number, topic: string, category: string, expression: string,
  answer: number): MathProblem {
  const e: MathProblem = new MathProblem();
  e.grade = grade;
  e.topic = topic;
  e.category = category;
  e.expression = expression;
  e.answer = answer;
  return e;
}

export const MATH_BANK: MathProblem[] = [
${rows}
];

/**
 * 按年级 / 题型随机抽 count 道，不重复。
 * topic 传空串表示不限题型。
 */
export function pickMath(grade: number, topic: string, count: number): MathProblem[] {
  const pool: MathProblem[] = [];
  for (let i = 0; i < MATH_BANK.length; i++) {
    const p: MathProblem = MATH_BANK[i];
    if (p.grade !== grade) {
      continue;
    }
    if (topic.length > 0 && p.topic !== topic) {
      continue;
    }
    pool.push(p);
  }
  // Fisher-Yates 洗前 count 个，比「sort(()=>Math.random()-0.5)」真的均匀
  for (let i = 0; i < pool.length; i++) {
    const j: number = i + Math.floor(Math.random() * (pool.length - i));
    const t: MathProblem = pool[i];
    pool[i] = pool[j];
    pool[j] = t;
  }
  return pool.slice(0, count);
}
`;
}

// ───────────────────────── 主流程 ─────────────────────────

const papers = buildExam();
const bank = buildMath();

const qTotal = papers.reduce((n, p) => n + p.questions.length, 0);
if (papers.length === 0 || qTotal === 0) throw new Error('试卷解析为空，检查 SQL 格式');
if (bank.length === 0) throw new Error('口算题库解析为空');

// 生成前先自检：解析错了宁可在这里炸掉，也别把错数据写进包里
for (const p of papers) {
  for (const item of p.questions) {
    if (!item.stem) throw new Error(`卷 ${p.id} 第 ${item.ordinal} 题题干为空`);
    if (!item.answer) throw new Error(`卷 ${p.id} 第 ${item.ordinal} 题答案为空`);
    if (item.kind === 'choice') {
      const idx = item.answer.charCodeAt(0) - 65;
      if (!(idx >= 0 && idx < item.options.length)) {
        throw new Error(`卷 ${p.id} 第 ${item.ordinal} 题答案 ${item.answer} 超出选项范围`);
      }
    }
  }
}

fs.mkdirSync(OUT, { recursive: true });
fs.writeFileSync(path.join(OUT, 'ExamData.ets'), emitExam(papers), 'utf8');
fs.writeFileSync(path.join(OUT, 'MathBank.ets'), emitMath(bank), 'utf8');

const byGrade = {};
for (const p of papers) byGrade[p.grade] = (byGrade[p.grade] || 0) + 1;

console.log('生成完成：');
console.log(`  data/ExamData.ets   ${papers.length} 套卷 / ${qTotal} 道题`);
console.log(`     年级分布：` + Object.keys(byGrade).sort().map((g) => `${g} 年级 ${byGrade[g]} 套`).join('，'));
console.log(`  data/MathBank.ets   ${bank.length} 道口算题`);
