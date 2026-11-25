/**
 * 判分规则的真跑验证：直接加载鸿蒙端真实使用的 core/ExamGrade.ets 与 data/ExamData.ets
 * （靠 Node 22 的 --experimental-transform-types 擦掉类型注解），
 * 对 252 道真题逐条验证「标准答案必须判对、改一个字符必须判错」。
 *
 * 为什么要这么绕：判分逻辑跑在 ArkTS 里，没有真机就执行不了；
 * 但它又是「错了最伤人」的一段代码（孩子答对却被判错）。
 * 把类型擦掉在 Node 里跑同一份源码，是唯一能在电脑上验它的办法。
 */
import { isCorrect, normalize, scoreOf } from './ExamGrade.ts';
import { EXAM_PAPERS, ExamQuestion, QKind } from './ExamData.ts';

let pass = 0;
const fails = [];

function ok(name, cond, detail) {
  if (cond) pass++;
  else fails.push(detail ? `${name} —— ${detail}` : name);
}

/** 题库标准答案的可提交形式：选择题是字母，判断题是对/错，填空题取第一个等价写法。 */
function standardAnswer(q) {
  if (q.kind === QKind.Choice) return q.answer.trim();
  if (q.kind === QKind.Fill) return q.answer.split('|')[0];
  return q.answer;
}

/** 半角 → 全角，模拟中文输入法下的输入。 */
function toFullWidth(s) {
  return [...s].map((c) => {
    const code = c.charCodeAt(0);
    if (code === 0x20) return '\u3000';
    if (code >= 0x21 && code <= 0x7e) return String.fromCharCode(code + 0xfee0);
    return c;
  }).join('');
}

let total = 0;
let wrongAnswerTested = 0;

for (const paper of EXAM_PAPERS) {
  for (const q of paper.questions) {
    total++;
    const std = standardAnswer(q);
    const where = `卷 ${paper.id} 第 ${q.ordinal} 题`;

    // ① 标准答案必须判对
    ok(`${where} 标准答案判对`, isCorrect(q, std), `答案「${std}」`);

    // ② 前后加空格、句末加句号、全角输入，都要判对
    ok(`${where} 容错空格`, isCorrect(q, `  ${std}  `));
    if (q.kind === QKind.Fill) {
      ok(`${where} 容错句末句号`, isCorrect(q, `${std}。`));
      ok(`${where} 容错全角输入`, isCorrect(q, toFullWidth(std)), `全角「${toFullWidth(std)}」`);
    }

    // ③ 空答案必须判错
    ok(`${where} 空答案判错`, isCorrect(q, '') === false);

    // ④ 改一个字符后必须判错（选择题换字母，判断/填空换内容）
    if (q.kind === QKind.Choice) {
      const other = q.answer === 'A' ? 'B' : 'A';
      if (q.options.length > 1) {
        ok(`${where} 错选判错`, isCorrect(q, other) === false);
        wrongAnswerTested++;
      }
    } else if (q.kind === QKind.Judge) {
      const other = q.answer === '对' ? '错' : '对';
      ok(`${where} 反选判错`, isCorrect(q, other) === false);
      wrongAnswerTested++;
    } else if (q.answer.split('|').length === 1) {
      // 只有一个标准写法的填空题：答案后面多一个字必须判错
      ok(`${where} 多字判错`, isCorrect(q, `${std}多`) === false);
      wrongAnswerTested++;
    }
  }
}

// ⑤ 多个等价写法：任何一个都该判对
let altCount = 0;
for (const paper of EXAM_PAPERS) {
  for (const q of paper.questions) {
    if (q.kind !== QKind.Fill) continue;
    const alts = q.answer.split('|').filter((s) => s.length > 0);
    for (const a of alts) {
      altCount++;
      ok(`卷 ${paper.id} 第 ${q.ordinal} 题 等价写法「${a}」判对`, isCorrect(q, a));
    }
  }
}

// ⑥ 大小写与数值等价
const fakeChoice = new ExamQuestion();
fakeChoice.kind = QKind.Fill;
fakeChoice.answer = '0.75|3/4';
ok('0.75 判对', isCorrect(fakeChoice, '0.75'));
ok('0.750 判对（数值等价）', isCorrect(fakeChoice, '0.750'));
ok('.75 判对（数值等价）', isCorrect(fakeChoice, '.75'));
ok('3/4 判对（另一写法）', isCorrect(fakeChoice, '3/4'));
ok('0.7 判错', isCorrect(fakeChoice, '0.7') === false);

const en = new ExamQuestion();
en.kind = QKind.Fill;
en.answer = 'Beijing';
ok('beijing 判对（忽略大小写）', isCorrect(en, 'beijing'));
ok('BEIJING 判对', isCorrect(en, 'BEIJING'));
ok('beijin 判错', isCorrect(en, 'beijin') === false);

const neg = new ExamQuestion();
neg.kind = QKind.Fill;
neg.answer = '-3';
ok('-3 判对', isCorrect(neg, '-3'));
ok('−3 判对（Unicode 减号）', isCorrect(neg, '−3'));
ok('－3 判对（全角减号）', isCorrect(neg, '－3'));

// ⑦ normalize 本身的边界
ok('normalize 去空格与引号', normalize('  「春 天」 ') === '春天', normalize('  「春 天」 '));
ok('normalize 全角数字', normalize('１２３') === '123', normalize('１２３'));
ok('normalize 末尾句号', normalize('苹果。') === '苹果', normalize('苹果。'));
ok('normalize 小数不被截断', normalize('0.5') === '0.5', normalize('0.5'));

// ⑧ 百分制
ok('scoreOf 满分', scoreOf(12, 12) === 100);
ok('scoreOf 9/12', scoreOf(9, 12) === 75);
ok('scoreOf 7/12', scoreOf(7, 12) === 58, `${scoreOf(7, 12)}`);
ok('scoreOf 0', scoreOf(0, 12) === 0);
ok('scoreOf 除以零不崩', scoreOf(0, 0) === 0);

console.log(`判分验证：${total} 道真题（其中 ${wrongAnswerTested} 道验了错答、${altCount} 个等价写法）`);
if (fails.length === 0) {
  console.log(`判分自检：${pass}/${pass} 全部通过`);
} else {
  console.log(`判分自检：通过 ${pass} 项，失败 ${fails.length} 项`);
  for (const f of fails.slice(0, 15)) console.log('  ✗ ' + f);
  process.exitCode = 1;
}
