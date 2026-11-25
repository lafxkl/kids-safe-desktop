import { MathProblem, pickMath } from '../data/MathBank';

/**
 * 口算出题器，对应 C# 的 Models/OralTraining.cs。
 *
 * 分两条路：
 *   一~四年级：现场出题。这四级是纯四则，题目空间足够大，孩子每次练都不会重复，
 *             也就不需要题库（题库会越用越熟，练不到真本事）。
 *   五~九年级：从 data/MathBank.ets 抽固定题。这几级题型发散（小数乘除、解方程、
 *             平方根），现场生成很容易出「1 ÷ 3 = 0.333…」这种没法用数字键盘答的题，
 *             所以改用整好的题。
 *
 * 正确答案一律在出题时算好，而不是回头去解析算式字符串 —— 一旦算式里出现
 * 括号、负号、余数写法，解析就要写一个表达式求值器，比出题本身还复杂。
 */

/** 题型筛选。值直接复用计算题库里的 topic 字段（add/sub/mul/div/mix）。 */
export enum OralTopic {
  All = '',
  Add = 'add',
  Sub = 'sub',
  Mul = 'mul',
  Div = 'div',
  Mix = 'mix'
}

export class OralProblem {
  expression: string = '';
  /** 正确答案。小学是整数，五~九年级可能是小数或负数。 */
  answer: number = 0;
  /** 题型说明，显示在「第 3 / 10 题 · 两位数乘法」后面 */
  category: string = '';
  /** 有余数的除法：answer 是商，remainder 是余数 */
  hasRemainder: boolean = false;
  remainder: number = 0;

  /** 给孩子看的正确答案文本，例如「12」「0.75」「7 余 3」 */
  answerText(): string {
    if (this.hasRemainder) {
      return `${formatNum(this.answer)} 余 ${this.remainder}`;
    }
    return formatNum(this.answer);
  }
}

/** 整数不带小数点，小数最多两位（0.50 → 0.5）。 */
export function formatNum(v: number): string {
  if (!isFinite(v)) {
    return '?';
  }
  if (Math.abs(v - Math.round(v)) < 1e-9) {
    return `${Math.round(v)}`;
  }
  let s: string = v.toFixed(2);
  while (s.length > 0 && s.charAt(s.length - 1) === '0') {
    s = s.substring(0, s.length - 1);
  }
  if (s.length > 0 && s.charAt(s.length - 1) === '.') {
    s = s.substring(0, s.length - 1);
  }
  return s;
}

/** 一轮练几道题。C# 是按难度 6 / 8 / 10，鸿蒙端没做难度开关，统一 10 道。 */
export const ROUND_COUNT: number = 10;

export function gradeTitle(grade: number): string {
  const names: string[] = ['一年级', '二年级', '三年级', '四年级', '五年级',
    '六年级', '初一', '初二', '初三'];
  if (grade >= 1 && grade <= 9) {
    return names[grade - 1];
  }
  return `${grade} 年级`;
}

/** 年级磁贴的配色，小学偏暖、初中偏冷，孩子一眼能分出学段。 */
export function gradeColor(grade: number): string {
  const colors: string[] = ['#FF8A65', '#FFA726', '#FDD835', '#9CCC65',
    '#4DB6AC', '#4FC3F7', '#7986CB', '#9575CD', '#A1887F'];
  if (grade >= 1 && grade <= 9) {
    return colors[grade - 1];
  }
  return '#90A4AE';
}

// ───────────────────────── 出题 ─────────────────────────

function rint(min: number, max: number): number {
  return min + Math.floor(Math.random() * (max - min + 1));
}

/** 一次生成的产物。rem < 0 表示没有余数。 */
class Gen {
  expr: string = '';
  ans: number = 0;
  rem: number = -1;

  constructor(expr: string, ans: number, rem: number = -1) {
    this.expr = expr;
    this.ans = ans;
    this.rem = rem;
  }
}

class Kind {
  cat: string = '';
  gen: () => Gen = () => new Gen('', 0);

  constructor(cat: string, gen: () => Gen) {
    this.cat = cat;
    this.gen = gen;
  }
}

// 一年级：20 以内 + 100 以内不进位 / 不借位
function add20(): Gen {
  const a: number = rint(1, 19);
  const b: number = rint(1, 20 - a);
  return new Gen(`${a} + ${b} = ?`, a + b);
}

function add100nc(): Gen {
  const t1: number = rint(1, 8);
  const o1: number = rint(0, 9);
  const t2: number = rint(1, 9 - t1);
  const o2: number = rint(0, 9 - o1);
  const a: number = 10 * t1 + o1;
  const b: number = 10 * t2 + o2;
  return new Gen(`${a} + ${b} = ?`, a + b);
}

function sub20(): Gen {
  const a: number = rint(2, 20);
  const b: number = rint(1, a);
  return new Gen(`${a} - ${b} = ?`, a - b);
}

function sub100nb(): Gen {
  const ta: number = rint(1, 9);
  const oa: number = rint(0, 9);
  const tb: number = rint(0, ta);
  const ob: number = tb < ta ? rint(0, 9) : rint(0, oa);
  const a: number = 10 * ta + oa;
  const b: number = 10 * tb + ob;
  return new Gen(`${a} - ${b} = ?`, a - b);
}

// 二年级：100 以内进退位、表内乘除、有余数、两步混合
function addCarry(): Gen {
  const a: number = rint(10, 89);
  const b: number = rint(10, 100 - a);
  return new Gen(`${a} + ${b} = ?`, a + b);
}

function subCarry(): Gen {
  const a: number = rint(20, 99);
  const b: number = rint(11, a - 1);
  return new Gen(`${a} - ${b} = ?`, a - b);
}

function mulTable(): Gen {
  const a: number = rint(2, 9);
  const b: number = rint(2, 9);
  return new Gen(`${a} × ${b} = ?`, a * b);
}

function divTable(): Gen {
  const q: number = rint(2, 9);
  const b: number = rint(2, 9);
  return new Gen(`${q * b} ÷ ${b} = ?`, q);
}

function divRem(): Gen {
  const divisor: number = rint(2, 9);
  const q: number = rint(1, 9);
  const rem: number = rint(1, divisor - 1);
  return new Gen(`${divisor * q + rem} ÷ ${divisor} = ?`, q, rem);
}

function mix2AddMul(): Gen {
  const a: number = rint(2, 9);
  const b: number = rint(2, 9);
  const c: number = rint(1, 20);
  return new Gen(`${a} × ${b} + ${c} = ?`, a * b + c);
}

function mix2MulAdd(): Gen {
  const a: number = rint(2, 40);
  const b: number = rint(2, 9);
  const c: number = rint(1, 20);
  return new Gen(`${a} + ${b} × ${c} = ?`, a + b * c);
}

function mix2MulSub(): Gen {
  const a: number = rint(2, 9);
  const b: number = rint(2, 9);
  const p: number = a * b;
  const c: number = rint(1, p);
  return new Gen(`${a} × ${b} - ${c} = ?`, p - c);
}

function mix2DivAdd(): Gen {
  const n: number = rint(2, 9);
  const q: number = rint(2, 9);
  const m: number = n * q;
  const k: number = rint(1, 20);
  return new Gen(`${m} ÷ ${n} + ${k} = ?`, q + k);
}

// 三年级：万以内加减、多位数乘、除数是一位数
function addBig(): Gen {
  const a: number = rint(100, 9999);
  const b: number = rint(10, 9999);
  return new Gen(`${a} + ${b} = ?`, a + b);
}

function subBig(): Gen {
  const a: number = rint(100, 9999);
  const b: number = rint(10, a);
  return new Gen(`${a} - ${b} = ?`, a - b);
}

function mulDigit(): Gen {
  const a: number = rint(10, 999);
  const b: number = rint(2, 9);
  return new Gen(`${a} × ${b} = ?`, a * b);
}

function mul2x2(): Gen {
  const a: number = rint(10, 99);
  const b: number = rint(10, 99);
  return new Gen(`${a} × ${b} = ?`, a * b);
}

function div1DigitExact(): Gen {
  const b: number = rint(2, 9);
  const q: number = rint(2, 99);
  return new Gen(`${b * q} ÷ ${b} = ?`, q);
}

function div1DigitRem(): Gen {
  const divisor: number = rint(2, 9);
  const q: number = rint(2, 30);
  const rem: number = rint(1, divisor - 1);
  return new Gen(`${divisor * q + rem} ÷ ${divisor} = ?`, q, rem);
}

function mix3ParenMul(): Gen {
  const a: number = rint(2, 40);
  const b: number = rint(2, 40);
  const c: number = rint(2, 9);
  return new Gen(`(${a} + ${b}) × ${c} = ?`, (a + b) * c);
}

function mix3AddMul(): Gen {
  const a: number = rint(10, 99);
  const b: number = rint(2, 9);
  const c: number = rint(1, 30);
  return new Gen(`${a} + ${b} × ${c} = ?`, a + b * c);
}

// 四年级：三位数乘两位数、除数是两位数、四则混合
function mul3x2(): Gen {
  const a: number = rint(100, 999);
  const b: number = rint(10, 99);
  return new Gen(`${a} × ${b} = ?`, a * b);
}

function div2DigitExact(): Gen {
  const b: number = rint(11, 99);
  const q: number = rint(2, 12);
  return new Gen(`${b * q} ÷ ${b} = ?`, q);
}

function div2DigitRem(): Gen {
  const divisor: number = rint(11, 99);
  const q: number = rint(2, 12);
  const rem: number = rint(1, divisor - 1);
  return new Gen(`${divisor * q + rem} ÷ ${divisor} = ?`, q, rem);
}

function mix4ParenMul(): Gen {
  const a: number = rint(2, 50);
  const b: number = rint(2, 50);
  const c: number = rint(2, 9);
  return new Gen(`(${a} + ${b}) × ${c} = ?`, (a + b) * c);
}

function mix4ParenSubMul(): Gen {
  const a: number = rint(20, 99);
  const b: number = rint(2, a - 1);
  const c: number = rint(2, 9);
  return new Gen(`(${a} - ${b}) × ${c} = ?`, (a - b) * c);
}

function mix4MulSub(): Gen {
  const a: number = rint(10, 99);
  const b: number = rint(2, 9);
  const p: number = a * b;
  const c: number = rint(1, p);
  return new Gen(`${a} × ${b} - ${c} = ?`, p - c);
}

function mix4AddMul(): Gen {
  const a: number = rint(50, 999);
  const b: number = rint(2, 12);
  const c: number = rint(1, 50);
  return new Gen(`${a} + ${b} × ${c} = ?`, a + b * c);
}

function mix4DivAdd(): Gen {
  const n: number = rint(11, 99);
  const q: number = rint(2, 12);
  const m: number = n * q;
  const k: number = rint(1, 30);
  return new Gen(`${m} ÷ ${n} + ${k} = ?`, q + k);
}

/** 每个年级有哪些题型。「混合」只出现在二年级及以上。 */
function kindsFor(grade: number): Kind[] {
  if (grade === 1) {
    return [
      new Kind('加法(不进位)', add20),
      new Kind('加法', add100nc),
      new Kind('减法', sub20),
      new Kind('减法(不借位)', sub100nb)
    ];
  }
  if (grade === 2) {
    return [
      new Kind('加法(进位)', addCarry),
      new Kind('减法(退位)', subCarry),
      new Kind('乘法', mulTable),
      new Kind('除法', divTable),
      new Kind('除法(有余数)', divRem),
      new Kind('混合', mix2AddMul),
      new Kind('混合', mix2MulAdd),
      new Kind('混合', mix2MulSub),
      new Kind('混合', mix2DivAdd)
    ];
  }
  if (grade === 3) {
    return [
      new Kind('加法', addBig),
      new Kind('减法', subBig),
      new Kind('乘法', mulDigit),
      new Kind('乘法', mul2x2),
      new Kind('除法', div1DigitExact),
      new Kind('除法(有余数)', div1DigitRem),
      new Kind('混合', mix3ParenMul),
      new Kind('混合', mix3AddMul)
    ];
  }
  return [
    new Kind('加法', addBig),
    new Kind('减法', subBig),
    new Kind('乘法', mul3x2),
    new Kind('除法', div2DigitExact),
    new Kind('除法(有余数)', div2DigitRem),
    new Kind('混合', mix4ParenMul),
    new Kind('混合', mix4ParenSubMul),
    new Kind('混合', mix4MulSub),
    new Kind('混合', mix4AddMul),
    new Kind('混合', mix4DivAdd)
  ];
}

/** 按题型筛一遍；筛空了就退回全部（比如一年级选「混合」，一年级本来没有）。 */
function filterKinds(kinds: Kind[], topic: string): Kind[] {
  if (topic.length === 0) {
    return kinds;
  }
  const out: Kind[] = [];
  for (let i = 0; i < kinds.length; i++) {
    const cat: string = kinds[i].cat;
    let hit: boolean = false;
    if (topic === OralTopic.Mix) {
      hit = cat === '混合';
    } else if (topic === OralTopic.Mul) {
      // 「乘法」要排除「(进位)」之类的干扰，但除法分类里也带「乘法」二字吗？不带。
      hit = cat.indexOf('乘法') >= 0;
    } else if (topic === OralTopic.Add) {
      hit = cat.indexOf('加法') >= 0;
    } else if (topic === OralTopic.Sub) {
      hit = cat.indexOf('减法') >= 0;
    } else if (topic === OralTopic.Div) {
      hit = cat.indexOf('除法') >= 0;
    }
    if (hit) {
      out.push(kinds[i]);
    }
  }
  return out.length > 0 ? out : kinds;
}

function fromMathProblem(p: MathProblem): OralProblem {
  const item = new OralProblem();
  item.expression = p.expression;
  item.answer = p.answer;
  item.category = p.category;
  item.hasRemainder = false;
  item.remainder = 0;
  return item;
}

/**
 * 出 count 道题。grade 1~4 现场生成，5~9 从题库抽。
 *
 * 题库抽不满时**按轮次重洗再抽**，而不是少给几道：
 * 九～初中的边缘题型题量很小（初三「除法」只有 4 道），
 * 少给的话孩子会看到「共 4 题」这种残缺的一轮；重洗能保证每轮都是 10 道，
 * 而且两次抽到同一题不会挨在一起。
 * 题库整个抽不到（年级越界等）时退回四年级现场出题，至少能练。
 */
export function generateOral(grade: number, topic: string, count: number): OralProblem[] {
  if (grade >= 5) {
    const out: OralProblem[] = [];
    // 每轮都重新洗一次，避免两轮之间题目顺序完全一样
    while (out.length < count) {
      const batch: MathProblem[] = pickMath(grade, topic, count - out.length);
      if (batch.length === 0) {
        break;
      }
      for (let i = 0; i < batch.length; i++) {
        out.push(fromMathProblem(batch[i]));
      }
    }
    if (out.length > 0) {
      return out;
    }
  }

  const kinds: Kind[] = filterKinds(kindsFor(grade >= 1 && grade <= 4 ? grade : 4), topic);
  const out: OralProblem[] = [];
  for (let i = 0; i < count; i++) {
    const kind: Kind = kinds[Math.floor(Math.random() * kinds.length)];
    const g: Gen = kind.gen();
    const item = new OralProblem();
    item.expression = g.expr;
    item.answer = g.ans;
    item.category = kind.cat;
    item.hasRemainder = g.rem >= 0;
    item.remainder = g.rem >= 0 ? g.rem : 0;
    out.push(item);
  }
  return out;
}

/**
 * 把算式转成中文语音，例如 "23 + 5 = ?" → "二十三 加 五 等于 多少"。
 *
 * 为什么要把算式念出来：一二年级的孩子认字不全，题目得靠听。
 * 负号的读法按上下文分：「3 - 5」读「减」，「-3」读「负」，
 * 判据是前一个字符是不是操作数结尾。
 */
export function spokenOf(expr: string): string {
  if (expr.length === 0) {
    return '算一算';
  }
  let out: string = '';
  let num: string = '';
  let prev: string = ' ';

  for (let i = 0; i < expr.length; i++) {
    const c: string = expr.charAt(i);
    if ((c >= '0' && c <= '9') || c === '.') {
      num += c;
      // prev 必须跟着数字走：否则「5 - 2」里的减号会因为 prev 还是空格
      // 被读成「负」，念出来是「五负二」。
      prev = c;
      continue;
    }
    // 遇到运算符先把攒着的数字念掉
    if (num.length > 0) {
      out += chineseOf(num);
      num = '';
    }

    if (c === ' ') {
      out += ' ';
    } else if (c === '+') {
      out += ' 加 ';
    } else if (c === '-') {
      out += endsOperand(prev) ? ' 减 ' : '负';
    } else if (c === '×' || c === '·') {
      out += ' 乘 ';
    } else if (c === '÷' || c === '/') {
      out += ' 除以 ';
    } else if (c === '=') {
      out += ' 等于 ';
    } else if (c === '?') {
      out += ' 多少 ';
    } else if (c === '(') {
      out += '括号';
    } else if (c === ')') {
      out += '括号 ';
    } else if (c === '²') {
      out += '的平方 ';
    } else if (c === '³') {
      out += '的立方 ';
    } else if (c === '√') {
      out += '根号 ';
    } else if (c === ':') {
      out += '比';
    } else if (c === 'x' || c === 'X') {
      out += '爱克斯';
    } else {
      out += c;
    }
    // 空格不更新 prev：它不改变「上一个是不是操作数」的判断
    if (c !== ' ') {
      prev = c;
    }
  }
  if (num.length > 0) {
    out += chineseOf(num);
  }

  // 合并多余空格
  let text: string = '';
  let lastSpace: boolean = false;
  for (let i = 0; i < out.length; i++) {
    const c: string = out.charAt(i);
    if (c === ' ') {
      if (!lastSpace && text.length > 0) {
        text += ' ';
      }
      lastSpace = true;
    } else {
      text += c;
      lastSpace = false;
    }
  }
  return text.length > 0 ? text : '算一算';
}

/** 上一个字符是不是「一个数/括号」的结尾：决定 - 读「减」还是「负」。 */
function endsOperand(c: string): boolean {
  if (c >= '0' && c <= '9') {
    return true;
  }
  return c === ')' || c === '²' || c === '³' || c === '°';
}

/** 数字 → 中文读法。小数按「点」逐位念，例如 0.75 → 零点七五。 */
function chineseOf(raw: string): string {
  const dot: number = raw.indexOf('.');
  if (dot < 0) {
    const n: number = Number(raw);
    return isNaN(n) ? raw : intToChinese(n);
  }
  const head: string = raw.substring(0, dot);
  const tail: string = raw.substring(dot + 1);
  let s: string = head.length > 0 ? intToChinese(Number(head)) : '零';
  s += '点';
  for (let i = 0; i < tail.length; i++) {
    s += digits[Number(tail.charAt(i))];
  }
  return s;
}

const digits: string[] = ['零', '一', '二', '三', '四', '五', '六', '七', '八', '九'];
const units: string[] = ['', '十', '百', '千', '万'];

/** 0 ~ 99999 的中文读法，够口算题用了（最大是五位数加减）。 */
function intToChinese(n: number): string {
  if (n === 0) {
    return '零';
  }
  if (n < 0) {
    return '负' + intToChinese(-n);
  }
  const s: string = `${Math.floor(n)}`;
  let out: string = '';
  let zero: boolean = false;
  for (let i = 0; i < s.length; i++) {
    const d: number = Number(s.charAt(i));
    const pos: number = s.length - 1 - i;
    if (d === 0) {
      zero = out.length > 0;
      continue;
    }
    if (zero) {
      out += '零';
      zero = false;
    }
    out += digits[d] + units[pos];
  }
  // 「一十三」按口语读「十三」
  if (out.indexOf('一十') === 0) {
    out = out.substring(1);
  }
  return out;
}
