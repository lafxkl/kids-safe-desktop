import { ExamQuestion, QKind } from '../data/ExamData';

/**
 * 考试判分，对应 C# 的 Services/ExamGrader.cs。
 *
 * 判分要能吃下孩子的书写习惯，否则「答对了却判错」比不判分更伤人：
 *   - 全角数字、全角括号（中文输入法默认全角，几乎每个孩子都会碰上）
 *   - 前后空格、句末句号
 *   - 英文大小写（beijing / Beijing）
 *   - 数值等值：0.5 与 0.50 与 .5 是同一个答案
 *   - 填空题的多个等价写法（库里用 | 分隔，如「丑陋|难看」）
 *
 * 反过来，判分只回答「对不对」，不猜意图：多选、超范围的字母一律算错。
 */
export function isCorrect(question: ExamQuestion, userAnswer: string): boolean {
  const user: string = normalize(userAnswer);
  if (user.length === 0) {
    return false;
  }
  if (question.kind === QKind.Fill) {
    return matchFill(question.answer, user);
  }
  return normalize(question.answer) === user;
}

/** 填空题：标准答案可能给了多个等价写法，命中任意一个即算对。 */
function matchFill(standard: string, user: string): boolean {
  const parts: string[] = standard.split('|');
  for (let i = 0; i < parts.length; i++) {
    const candidate: string = normalize(parts[i]);
    if (candidate.length === 0) {
      continue;
    }
    if (candidate === user) {
      return true;
    }
    const a: number = Number(candidate);
    const b: number = Number(user);
    if (!isNaN(a) && !isNaN(b) && Math.abs(a - b) < 1e-6) {
      return true;
    }
  }
  return false;
}

/**
 * 归一化：全角转半角、去空白、去句末标点、英文转小写。
 * 「−」「—」这类长得像减号的字符统一成 ASCII 减号。
 */
export function normalize(text: string): string {
  if (text.length === 0) {
    return '';
  }
  let out: string = '';
  for (let i = 0; i < text.length; i++) {
    let c: string = text.charAt(i);
    const code: number = c.charCodeAt(0);

    // 全角字母数字 / 全角标点 → 半角
    if (code >= 0xFF01 && code <= 0xFF5E) {
      c = String.fromCharCode(code - 0xFEE0);
    } else if (code === 0x3000) {
      continue; // 全角空格直接丢
    }

    if (c === '−' || c === '–' || c === '—' || c === '－') {
      c = '-';
    } else if (c === '×' || c === '＊') {
      c = '*';
    } else if (c === '÷') {
      c = '/';
    } else if (c === '，' || c === '、') {
      c = ',';
    } else if (c === '。' || c === '．') {
      c = '.';
    } else if (c === '：') {
      c = ':';
    } else if (c === '；') {
      c = ';';
    } else if (c === '「' || c === '」' || c === '“' || c === '”' ||
      c === '"' || c === '\'' || c === '‘' || c === '’') {
      continue; // 引号一律不算内容
    } else if (c === ' ' || c === '\t' || c === '\r' || c === '\n') {
      continue;
    }

    out += c.toLowerCase();
  }

  // 填空题末尾的句号经常多打，去掉；小数中间的 . 不动
  while (out.length > 0 && out.charAt(out.length - 1) === '.') {
    out = out.substring(0, out.length - 1);
  }
  return out;
}

/**
 * 百分制。四舍五入用 Math.round —— 和 C# 的 MidpointRounding.AwayFromZero 在
 * 正数上结果一致（本题满分 12 题，不会出现 .5 的半数点）。
 */
export function scoreOf(correct: number, total: number): number {
  if (total <= 0) {
    return 0;
  }
  return Math.round(100 * correct / total);
}
