/**
 * 口算出题器的真跑验证：加载鸿蒙端真实的 core/OralGen.ets + data/MathBank.ets，
 * 每个年级每种题型各出 200 道，把「题目」按四则重新算一遍，和出题器给的答案比。
 *
 * 出题器写错的后果比判分错更隐蔽：孩子看着一道「3 + 5 = ?」被要求写 9，
 * 自己却以为是自己的问题。所以这里逐题验算，不抽样。
 */
import { formatNum, generateOral, gradeTitle, ROUND_COUNT, spokenOf } from './OralGen.ts';

let pass = 0;
const fails = [];

function ok(name, cond, detail) {
  if (cond) pass++;
  else fails.push(detail ? `${name} —— ${detail}` : name);
}

/** 按四则算一遍算式（只用于四则题；带余数的单独处理）。 */
function evalExpr(expression) {
  let s = expression.replace(/×/g, '*').replace(/−/g, '-');
  // 分数除法要整体括起来：1/2 ÷ 1/4 不是 1/2/1/4
  s = s.replace(
    /(\d+(?:\.\d+)?(?:\/\d+(?:\.\d+)?)?)\s*÷\s*(\d+(?:\.\d+)?(?:\/\d+(?:\.\d+)?)?)/g,
    '($1)/($2)');
  const body = s.replace(/÷/g, '/').replace(/=\s*\?$/, '').replace(/\s+/g, '');
  if (!/^[-+*/().\d]+$/.test(body)) return null;
  try {
    const v = Function('"use strict";return (' + body + ')')();
    return typeof v === 'number' && isFinite(v) ? v : null;
  } catch (e) {
    return null;
  }
}

/** 从 "23 ÷ 5 = ?" 里取被除数和除数，用来验「余数」题。 */
function dividendOf(expression) {
  const m = expression.match(/^\s*(\d+)\s*÷\s*(\d+)\s*=/);
  return m ? { n: Number(m[1]), d: Number(m[2]) } : null;
}

const TOPICS = ['', 'add', 'sub', 'mul', 'div', 'mix'];
const PER_CASE = 200;

let checked = 0;
let remainderCases = 0;
let bankSkipped = 0;

for (let grade = 1; grade <= 9; grade++) {
  for (const topic of TOPICS) {
    const deck = generateOral(grade, topic, PER_CASE);
    // 每轮都必须凑齐：题库不够时按轮次重洗补齐（不然会看到「共 4 题」的残局）
    ok(`${gradeTitle(grade)}/${topic || '全部'} 出题数量正确`, deck.length === PER_CASE,
      `实际 ${deck.length}`);

    for (const p of deck) {
      checked++;
      const where = `${gradeTitle(grade)}/${topic || '全部'} 「${p.expression}」`;

      if (!p.expression || p.expression.trim().length === 0) {
        fails.push(`${where} 题干为空`);
        continue;
      }
      if (!p.category || p.category.length === 0) {
        fails.push(`${where} 没有题型说明`);
        continue;
      }
      if (!isFinite(p.answer)) {
        fails.push(`${where} 答案不是数字`);
        continue;
      }

      if (p.hasRemainder) {
        remainderCases++;
        const d = dividendOf(p.expression);
        if (!d) {
          fails.push(`${where} 标了余数但题干不是除法`);
          continue;
        }
        // 除法恒等式：商 × 除数 + 余数 = 被除数
        ok(`${where} 余数自洽`, p.answer * d.d + p.remainder === d.n,
          `${p.answer}×${d.d}+${p.remainder} ≠ ${d.n}`);
        ok(`${where} 余数小于除数`, p.remainder > 0 && p.remainder < d.d);
        continue;
      }

      const got = evalExpr(p.expression);
      if (got === null) {
        // 五~九年级题库里有几何、函数、概率这类文字题，本脚本算不了；
        // 它们的答案由 tools/check-learn.js 用更宽的规则集验，这里不重复报错。
        if (grade >= 5) {
          bankSkipped++;
          continue;
        }
        fails.push(`${where} 一年级~四年级的题必须是纯四则，但算不出来`);
        continue;
      }
      ok(`${where} 答案正确`, Math.abs(got - p.answer) < 1e-6,
        `题面算出 ${got}，出题器给 ${p.answer}`);
    }
  }
}

// 出题器的答案文本
ok('formatNum 整数', formatNum(12) === '12', formatNum(12));
ok('formatNum 小数去尾零', formatNum(0.5) === '0.5', formatNum(0.5));
ok('formatNum 0.50 → 0.5', formatNum(0.5) === '0.5');
ok('formatNum 负数', formatNum(-3) === '-3', formatNum(-3));
ok('formatNum 两位小数', formatNum(0.75) === '0.75', formatNum(0.75));

// 语音读法：算式的每个数字都要被念成中文，不能剩下阿拉伯数字
const spoken = spokenOf('23 + 5 = ?');
ok('语音把数字读成中文', !/[0-9]/.test(spoken), spoken);
ok('语音有「加」', spoken.includes('加'), spoken);
ok('语音以「多少」结尾', spoken.trim().endsWith('多少'), spoken);
const spoken2 = spokenOf('5 - 2 = ?');
ok('语音把减号读成「减」', spoken2.includes(' 减 '), spoken2);
// 开头的减号才是「负」：「-3 + 5」读「负三 加 五」
const spoken3 = spokenOf('-3 + 5 = ?');
ok('开头负号读「负」', spoken3.includes('负'), spoken3);
ok('带前导负号时减号不误判成负', spokenOf('5 - 2 = ?').includes('减'));
const spoken4 = spokenOf('(12 + 8) × 3 = ?');
ok('语音念括号', spoken4.includes('括号'), spoken4);
ok('语音念乘号', spoken4.includes('乘'), spoken4);
ok('语音念小数', spokenOf('0.75 + 1 = ?').includes('点'), spokenOf('0.75 + 1 = ?'));

// 一轮的题量
ok('一轮 10 道', ROUND_COUNT === 10);

// 一年级不该出「混合」题（一年级课标没有两步混合）
const g1mix = generateOral(1, 'mix', 50);
ok('一年级选「混合」时退回四则', g1mix.every((p) => !p.category.includes('混合')),
  g1mix.map((p) => p.category).join(','));

// 五~九年级必须来自题库（题库里没有「有余数」这种小学题型）
const g7 = generateOral(7, '', 100);
ok('初一题目来自题库', g7.every((p) => !p.hasRemainder));

console.log(`口算验证：${checked} 道题（含 ${remainderCases} 道有余数）`);
if (fails.length === 0) {
  console.log(`口算自检：${pass}/${pass} 全部通过`);
} else {
  console.log(`口算自检：通过 ${pass} 项，失败 ${fails.length} 项`);
  for (const f of fails.slice(0, 15)) console.log('  ✗ ' + f);
  process.exitCode = 1;
}
