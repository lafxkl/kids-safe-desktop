/**
 * 学习天地三项功能的数据自检。
 *
 * 这一类检查的价值全在「能在没有真机的情况下发现错题」：
 *   ① 口算题库的答案如果算错，孩子照着练会练出错答案，比不做还糟；
 *   ② 考试的单选题答案如果指向不存在的选项，判分永远判错；
 *   ③ 星星上报的 key 如果没登记，成绩永远上不了排行榜。
 * 这三类错误都不会让程序崩，只会安静地错 —— 所以要脚本盯。
 *
 * 用法：node tools/check-learn.js
 */
const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const DATA = path.join(ROOT, 'entry', 'src', 'main', 'ets', 'data');
const ETS = path.join(ROOT, 'entry', 'src', 'main', 'ets');

let pass = 0;
const fails = [];

function ok(name, cond, detail) {
  if (cond) {
    pass++;
  } else {
    fails.push(detail ? `${name} —— ${detail}` : name);
  }
}

function read(p) {
  return fs.readFileSync(p, 'utf8');
}

// ───────────────────────── 调用参数解析 ─────────────────────────

/**
 * 从 src 里逐个找出 `name(` 调用，返回每组的参数文本数组。
 * 生成的数据文件是「一行一次调用，可能跨行」，所以必须按括号配平扫，
 * 不能用行正则 —— 题干里本身就有括号和逗号。
 */
function collectCalls(src, name) {
  const out = [];
  let i = 0;
  while (i < src.length) {
    const at = src.indexOf(name + '(', i);
    if (at < 0) break;
    // 必须整词匹配，避免把 `sq(` 当成 `q(`
    const before = at > 0 ? src[at - 1] : '\n';
    if (/[A-Za-z0-9_]/.test(before)) {
      i = at + name.length;
      continue;
    }
    // 跳过函数声明本身（`function m(grade: number, ...)` 也是 m( 开头）
    if (src.slice(Math.max(0, at - 9), at) === 'function ') {
      i = at + name.length;
      continue;
    }
    const args = splitArgs(src, at + name.length + 1);
    if (args) out.push(args.list);
    i = args ? args.end : at + name.length;
  }
  return out;
}

/** 从 start 开始（已经跳过了左括号）按顶层逗号切参数，直到配平的右括号。 */
function splitArgs(src, start) {
  const list = [];
  let buf = '';
  let depth = 0;
  let inStr = false;
  for (let i = start; i < src.length; i++) {
    const c = src[i];
    if (inStr) {
      buf += c;
      if (c === '\\') {
        buf += src[i + 1];
        i++;
      } else if (c === '"') {
        inStr = false;
      }
      continue;
    }
    if (c === '"') {
      inStr = true;
      buf += c;
      continue;
    }
    if (c === '(' || c === '[') {
      depth++;
      buf += c;
      continue;
    }
    if (c === ')' || c === ']') {
      if (depth === 0) {
        list.push(buf.trim());
        return { list, end: i + 1 };
      }
      depth--;
      buf += c;
      continue;
    }
    if (c === ',' && depth === 0) {
      list.push(buf.trim());
      buf = '';
      continue;
    }
    buf += c;
  }
  return null;
}

const asJson = (text) => JSON.parse(text);

// ───────────────────────── 口算题库 ─────────────────────────

const SUP = {
  '⁰': '0', '¹': '1', '²': '2', '³': '3', '⁴': '4',
  '⁵': '5', '⁶': '6', '⁷': '7', '⁸': '8', '⁹': '9', '⁻': '-'
};

/**
 * 把算式里的特殊记号换成 JS 能算的样子。
 * 只做「符号级」的替换，不改写题目本身，所以对四则题是绝对安全的。
 *
 * 三处必须显式补乘号，否则会算出离谱的结果（都是实测踩到的）：
 *   「2√3」不能变成「21.732…」，「2sin30°」不能变成「20.5」，
 *   「1/2 ÷ 1/4」不能变成「1/2/1/4」（÷ 优先级和 / 一样，会把 1/4 拆开）。
 */
function normalizeExpr(expr) {
  let s = expr
    .replace(/×/g, '*')
    .replace(/−/g, '-')
    .replace(/－/g, '-');

  // 分数除法：先把 a/b ÷ c/d 整个括起来，再统一成 /
  s = s.replace(
    /(\d+(?:\.\d+)?(?:\/\d+(?:\.\d+)?)?)\s*÷\s*(\d+(?:\.\d+)?(?:\/\d+(?:\.\d+)?)?)/g,
    '($1)/($2)');
  s = s.replace(/÷/g, '/');

  // 上标数字 → **n（2⁴ → 2**4、2⁻² → 2**-2）
  s = s.replace(/[⁰¹²³⁴⁵⁶⁷⁸⁹⁻]+/g, (m) => '**' + [...m].map((c) => SUP[c]).join(''));
  // 百分数 → 除以 100
  s = s.replace(/(\d+(?:\.\d+)?)\s*%/g, '($1/100)');

  // 三角函数（带系数要先处理，否则 2sin30° 会变成 20.5）
  s = s.replace(/(\d+(?:\.\d+)?)\s*(sin|cos|tan)\s*(\d+(?:\.\d+)?)\s*°/g,
    (m, a, f, d) => `${a}*(${trig(f, Number(d))})`);
  s = s.replace(/(sin|cos|tan)\s*(\d+(?:\.\d+)?)\s*°/g,
    (m, f, d) => `(${trig(f, Number(d))})`);

  // 根号（同样要补乘号：2√3 = 2 × √3）
  s = s.replace(/(\d+(?:\.\d+)?)\s*√\s*(\d+(?:\.\d+)?)/g,
    (m, a, n) => `${a}*(${Math.sqrt(Number(n))})`);
  s = s.replace(/√\s*(\d+(?:\.\d+)?)/g, (m, n) => `(${Math.sqrt(Number(n))})`);
  s = s.replace(/(\d+(?:\.\d+)?)\s*∛\s*(\d+(?:\.\d+)?)/g,
    (m, a, n) => `${a}*(${Math.cbrt(Number(n))})`);
  s = s.replace(/∛\s*(\d+(?:\.\d+)?)/g, (m, n) => `(${Math.cbrt(Number(n))})`);

  // 绝对值可以出现在式子中间：「|-9| - 4」
  s = s.replace(/\|([^|]+)\|/g, (m, inner) => {
    const v = evalPure(inner);
    return v === null ? m : `(${Math.abs(v)})`;
  });

  return s.replace(/\s+/g, '');
}

function trig(f, deg) {
  const r = deg * Math.PI / 180;
  const v = f === 'sin' ? Math.sin(r) : (f === 'cos' ? Math.cos(r) : Math.tan(r));
  // sin30° 在浮点里是 0.49999999999999994，四舍五入到 1e-9 再比
  return Math.round(v * 1e9) / 1e9;
}

/** 纯四则（含括号、小数、幂、根号）才求值；不认识的题返回 null。 */
function evalPure(expr) {
  const body = normalizeExpr(expr)
    .replace(/\)\s*\(/g, ')*(') // 代入后常出现 (3)(-0)，数学上是乘法，JS 会当函数调用
    .replace(/=\?$/, '')
    .replace(/\?$/, '');

  if (!/^[-+*/().\d]+$/.test(body)) return null;
  try {
    const v = Function('"use strict";return (' + body + ')')();
    return typeof v === 'number' && isFinite(v) ? v : null;
  } catch (e) {
    return null;
  }
}

/**
 * 验算一道题。返回 { ok, note } 或 null（表示这类题本脚本验不了）。
 *
 * 分派顺序很关键：先试最简单、最不容易误判的纯四则，
 * 再逐层处理「代入求值 → 解方程 → 文字题」，越往后越容易误判。
 */
function verifyMath(expression, answer) {
  const expr = expression.trim();

  // ① 纯四则（含幂、根号、百分数、绝对值、三角函数）
  const direct = evalPure(expr);
  if (direct !== null) {
    return { ok: Math.abs(direct - answer) < 1e-6, note: Number(direct.toFixed(6)) };
  }

  // ② 「x = 3 时，2x + 5 = ?」/「a = 3 时，6/a = ?」：代入再算
  const when = expr.match(/^\s*([a-zA-Z])\s*=\s*(-?[\d.]+)\s*时[，,]\s*(.+?)=\s*\?$/);
  if (when) {
    const v = Number(when[2]);
    const body = substitute(when[3], when[1], v);
    const got = evalPure(body);
    if (got !== null) return { ok: Math.abs(got - answer) < 1e-6, note: Number(got.toFixed(6)) };
  }

  // ③ 「2x + 3 = 11，x = ?」：把答案代进方程，看左右是否相等
  const eq = expr.match(/^(.*?)[，,]\s*([a-zA-Z])\s*=\s*\?$/);
  if (eq && eq[1].includes('=')) {
    const v = answer;
    const sides = eq[1].split('=');
    if (sides.length === 2) {
      const l = evalPure(substitute(sides[0], eq[2], v));
      const r = evalPure(substitute(sides[1], eq[2], v));
      if (l !== null && r !== null) {
        return { ok: Math.abs(l - r) < 1e-6, note: `${l} vs ${r}` };
      }
    }
  }

  // ④ 「3x + 5x = ?x」/「6a ÷ 2 = ?a」：问的是系数
  const coef = expr.match(/^(.*?)=\s*\?([a-zA-Z])$/);
  // 带次方的（6x² ÷ 2x = 3x）不能简单地把 x 抹掉当系数算，跳过
  if (coef && !new RegExp(`${coef[2]}[⁰¹²³⁴⁵⁶⁷⁸⁹]`).test(coef[1])) {
    let body = coef[1].replace(new RegExp(`(\\d)\\s*${coef[2]}`, 'g'), '$1');
    body = body.replace(new RegExp(`(?<![\\d.])${coef[2]}`, 'g'), '1');
    const got = evalPure(body);
    if (got !== null) return { ok: Math.abs(got - answer) < 1e-6, note: Number(got.toFixed(6)) };
  }

  // ⑤ 比例：3 : 4 = 9 : ?
  const prop = expr.match(/^(\d+(?:\.\d+)?)\s*:\s*(\d+(?:\.\d+)?)\s*=\s*(\d+(?:\.\d+)?)\s*:\s*\?/);
  if (prop) {
    const got = Number(prop[2]) * Number(prop[3]) / Number(prop[1]);
    return { ok: Math.abs(got - answer) < 1e-6, note: got };
  }

  // ⑥ 统计量：「数据 2、4、4、6、9 的中位数」，可能带「+ 1」这种后缀
  const stat = expr.match(/^(?:数据\s*)?([\d.、\s]+?)的(平均数|中位数|众数)(?:\s*([+-])\s*(\d+(?:\.\d+)?))?/);
  if (stat) {
    const nums = stat[1].split('、').map(Number).filter((n) => !isNaN(n));
    if (nums.length > 0) {
      let got = null;
      if (stat[2] === '平均数') {
        got = nums.reduce((a, b) => a + b, 0) / nums.length;
      } else if (stat[2] === '中位数') {
        const sorted = [...nums].sort((a, b) => a - b);
        const mid = Math.floor(sorted.length / 2);
        got = sorted.length % 2 === 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
      } else {
        // 众数：出现次数最多的那个；并列时取较小值（题库只有唯一众数）
        const count = new Map();
        for (const n of nums) count.set(n, (count.get(n) || 0) + 1);
        got = [...count.entries()].sort((a, b) => (b[1] - a[1]) || (a[0] - b[0]))[0][0];
      }
      if (stat[3]) {
        const k = Number(stat[4]);
        got = stat[3] === '+' ? got + k : got - k;
      }
      return { ok: Math.abs(got - answer) < 1e-6, note: got };
    }
  }

  // ⑦ 打折：「原价 200 元打八折，现价」
  const sale = expr.match(/原价\s*(\d+(?:\.\d+)?)\s*元打([一二三四五六七八九])折/);
  if (sale) {
    const rate = '一二三四五六七八九'.indexOf(sale[2]) + 1;
    const got = Number(sale[1]) * rate / 10;
    return { ok: Math.abs(got - answer) < 1e-6, note: got };
  }

  // ⑧ 三角形第三角
  const tri = expr.match(/两角\s*(\d+(?:\.\d+)?)\s*°?\s*和\s*(\d+(?:\.\d+)?)\s*°/);
  if (tri) {
    const got = 180 - Number(tri[1]) - Number(tri[2]);
    return { ok: Math.abs(got - answer) < 1e-6, note: got };
  }

  // ⑨ 勾股定理求斜边
  const hyp = expr.match(/直角边\s*(\d+(?:\.\d+)?)\s*、\s*(\d+(?:\.\d+)?)\s*，斜边/);
  if (hyp) {
    const got = Math.sqrt(Number(hyp[1]) ** 2 + Number(hyp[2]) ** 2);
    return { ok: Math.abs(got - answer) < 1e-6, note: got };
  }

  // ⑩ 「5 个数的平均数是 10，它们的和」
  const sum = expr.match(/(\d+)\s*个数的平均数是\s*(\d+(?:\.\d+)?)/);
  if (sum) {
    const got = Number(sum[1]) * Number(sum[2]);
    return { ok: Math.abs(got - answer) < 1e-6, note: got };
  }

  // ⑪ 骰子：「点数大于 4 的情况有几种」
  const dice = expr.match(/点数大于\s*(\d+)\s*的情况有几种/);
  if (dice) {
    const got = 6 - Number(dice[1]);
    return { ok: Math.abs(got - answer) < 1e-6, note: got };
  }

  // ⑫ 「200 的 80%」：求一个数的百分之几
  const pct = expr.match(/(\d+(?:\.\d+)?)\s*的\s*(\d+(?:\.\d+)?)\s*%/);
  if (pct) {
    const got = Number(pct[1]) * Number(pct[2]) / 100;
    return { ok: Math.abs(got - answer) < 1e-6, note: got };
  }

  // ⑬ 「涨价 10% 后是 110 元，原价」：倒推原价
  const rise = expr.match(/涨价\s*(\d+(?:\.\d+)?)\s*%\s*后是\s*(\d+(?:\.\d+)?)\s*元/);
  if (rise) {
    const got = Number(rise[2]) / (1 + Number(rise[1]) / 100);
    return { ok: Math.abs(got - answer) < 1e-6, note: Number(got.toFixed(6)) };
  }

  // ⑭ 函数求值：「y = 2x + 1，x = 3 时 y = ?」（一次、二次函数都适用）
  const fnY = expr.match(/^y\s*=\s*(.+?)[，,]\s*x\s*=\s*(-?[\d.]+)\s*时\s*y\s*=\s*\?$/);
  if (fnY) {
    const got = evalPure(substitute(fnY[1], 'x', Number(fnY[2])));
    if (got !== null) return { ok: Math.abs(got - answer) < 1e-6, note: Number(got.toFixed(6)) };
  }

  // ⑮ 线性反解：「y = 2x - 4，y = 0 时 x = ?」
  // 用两点定直线再求交点，省得写一套代数求解 —— 只对一次函数成立，
  // 所以先确认 f(0) 与 f(2) 的中点等于 f(1)（线性性），否则跳过。
  const fnX = expr.match(/^y\s*=\s*(.+?)[，,]\s*y\s*=\s*(-?[\d.]+)\s*时\s*x\s*=\s*\?$/);
  if (fnX) {
    const f0 = evalPure(substitute(fnX[1], 'x', 0));
    const f1 = evalPure(substitute(fnX[1], 'x', 1));
    const f2 = evalPure(substitute(fnX[1], 'x', 2));
    if (f0 !== null && f1 !== null && f2 !== null && Math.abs((f0 + f2) / 2 - f1) < 1e-9) {
      const k = f1 - f0;
      if (Math.abs(k) > 1e-9) {
        const got = (Number(fnX[2]) - f0) / k;
        return { ok: Math.abs(got - answer) < 1e-6, note: Number(got.toFixed(6)) };
      }
    }
  }

  // ⑯ 一元二次 / 高次方程：「x² = 16，x > 0，x = ?」「x² - 7x + 12 = 0，较小的根 = ?」
  // 不去解，直接把「题库给的答案」代回原方程，看两边是否相等 ——
  // 这样二次、带括号、带条件的写法都能验，而且不会因为「求根公式写错」而误判。
  const segs = expr.split(/[，,]/);
  if (segs.length >= 2 && segs[0].includes('=')) {
    const tail = segs.slice(1).join('，');
    let xVal = answer;
    // 「x = 9，x > 0，x + 1 = ?」这种，答案不是 x 而是 x + 1
    const shifted = tail.match(/x\s*([+-])\s*(\d+(?:\.\d+)?)\s*=\s*\?/);
    if (shifted) {
      const k = Number(shifted[2]);
      xVal = shifted[1] === '+' ? answer - k : answer + k;
    }
    const sides = segs[0].split('=');
    if (sides.length === 2) {
      const l = evalPure(substitute(sides[0], 'x', xVal));
      const r = evalPure(substitute(sides[1], 'x', xVal));
      if (l !== null && r !== null) {
        return { ok: Math.abs(l - r) < 1e-6, note: `${l} vs ${r}` };
      }
    }
  }

  // ⑰ 二元一次方程组
  const sys1 = expr.match(/^x\s*\+\s*y\s*=\s*(-?[\d.]+)[，,]\s*x\s*-\s*y\s*=\s*(-?[\d.]+)[，,]\s*x\s*=\s*\?$/);
  if (sys1) {
    const got = (Number(sys1[1]) + Number(sys1[2])) / 2;
    return { ok: Math.abs(got - answer) < 1e-6, note: got };
  }
  const sys2 = expr.match(/^(\d*(?:\.\d+)?)x\s*\+\s*y\s*=\s*(-?[\d.]+)[，,]\s*y\s*=\s*(-?[\d.]+)[，,]\s*x\s*=\s*\?$/);
  if (sys2) {
    const a = sys2[1] === '' ? 1 : Number(sys2[1]);
    const got = (Number(sys2[2]) - Number(sys2[3])) / a;
    return { ok: Math.abs(got - answer) < 1e-6, note: got };
  }

  // ⑱ 圆与扇形（题目统一给 π = 3.14，就按 3.14 算）
  const sector = expr.match(/(\d+)°\s*扇形、半径\s*(\d+(?:\.\d+)?)/);
  if (sector) {
    const got = 3.14 * Number(sector[2]) ** 2 * Number(sector[1]) / 360;
    return { ok: Math.abs(got - answer) < 1e-4, note: Number(got.toFixed(4)) };
  }
  const circle = expr.match(/(半径|直径)\s*(\d+(?:\.\d+)?)\s*[，,]\s*(周长|面积)/);
  if (circle) {
    const r = circle[1] === '直径' ? Number(circle[2]) / 2 : Number(circle[2]);
    const got = circle[3] === '周长' ? 2 * 3.14 * r : 3.14 * r * r;
    return { ok: Math.abs(got - answer) < 1e-4, note: Number(got.toFixed(4)) };
  }

  // ⑲ 面积 / 体积 / 内角和这类纯公式题
  const shape = shapeAnswer(expr);
  if (shape !== null) {
    return { ok: Math.abs(shape - answer) < 1e-6, note: shape };
  }

  return null;
}

/**
 * 图形题：每种形状一条公式。全部按题目给的数字算，不猜题意。
 * 认不出来的返回 null（比如「正方体展开图」这种没法纯算的）。
 */
function shapeAnswer(expr) {
  const num = (s) => Number(s);

  let m = expr.match(/长\s*(\d+(?:\.\d+)?)\s*宽\s*(\d+(?:\.\d+)?)\s*的长方形，面积/);
  if (m) return num(m[1]) * num(m[2]);

  m = expr.match(/长\s*(\d+(?:\.\d+)?)\s*宽\s*(\d+(?:\.\d+)?)\s*的长方形，周长/);
  if (m) return 2 * (num(m[1]) + num(m[2]));

  m = expr.match(/长\s*(\d+(?:\.\d+)?)\s*宽\s*(\d+(?:\.\d+)?)\s*高\s*(\d+(?:\.\d+)?)\s*的长方体，体积/);
  if (m) return num(m[1]) * num(m[2]) * num(m[3]);

  m = expr.match(/边长\s*(\d+(?:\.\d+)?)\s*的正方形，(面积|周长)/);
  if (m) return m[2] === '面积' ? num(m[1]) ** 2 : 4 * num(m[1]);

  m = expr.match(/底\s*(\d+(?:\.\d+)?)\s*高\s*(\d+(?:\.\d+)?)\s*的平行四边形，面积/);
  if (m) return num(m[1]) * num(m[2]);

  m = expr.match(/底\s*(\d+(?:\.\d+)?)\s*高\s*(\d+(?:\.\d+)?)\s*的三角形，面积/);
  if (m) return num(m[1]) * num(m[2]) / 2;

  m = expr.match(/上底\s*(\d+(?:\.\d+)?)、下底\s*(\d+(?:\.\d+)?)、高\s*(\d+(?:\.\d+)?)\s*的梯形，面积/);
  if (m) return (num(m[1]) + num(m[2])) * num(m[3]) / 2;

  m = expr.match(/平行四边形底\s*(\d+(?:\.\d+)?)、高\s*(\d+(?:\.\d+)?)，面积/);
  if (m) return num(m[1]) * num(m[2]);

  m = expr.match(/三角形底\s*(\d+(?:\.\d+)?)、高\s*(\d+(?:\.\d+)?)，面积/);
  if (m) return num(m[1]) * num(m[2]) / 2;

  m = expr.match(/菱形对角线\s*(\d+(?:\.\d+)?)\s*和\s*(\d+(?:\.\d+)?)，面积/);
  if (m) return num(m[1]) * num(m[2]) / 2;

  m = expr.match(/正方形对角线\s*(\d+(?:\.\d+)?)，面积/);
  if (m) return num(m[1]) ** 2 / 2;

  m = expr.match(/(\S+?)边形内角和/);
  if (m) {
    const n = cnNumber(m[1]);
    if (n !== null) return (n - 2) * 180;
  }

  m = expr.match(/正(\S+?)边形每个外角/);
  if (m) {
    const n = cnNumber(m[1]);
    if (n !== null) return 360 / n;
  }

  return null;
}

/** 「五」「十二」这类中文数字转整数，认不出来返回 null。 */
function cnNumber(text) {
  const map = { 一: 1, 二: 2, 三: 3, 四: 4, 五: 5, 六: 6, 七: 7, 八: 8, 九: 9, 十: 10 };
  if (text.length === 1) return map[text] === undefined ? null : map[text];
  if (text.length === 2 && text[0] === '十') return 10 + (map[text[1]] || 0);
  if (text.length === 2 && text[1] === '十') return (map[text[0]] || 0) * 10;
  if (text.length === 3 && text[1] === '十') {
    return (map[text[0]] || 0) * 10 + (map[text[2]] || 0);
  }
  return null;
}
function substitute(body, variable, value) {
  const re = new RegExp(`(\\d)\\s*${variable}`, 'g');
  const reSolo = new RegExp(`(?<![\\d.])${variable}`, 'g');
  return body.replace(re, `$1*(${value})`).replace(reSolo, `(${value})`);
}

function checkMathBank() {
  const src = read(path.join(DATA, 'MathBank.ets'));
  const rows = collectCalls(src, 'm');
  ok('口算题库条目数 ≥ 300', rows.length >= 300, `实际 ${rows.length}`);

  let checked = 0;
  const bad = [];
  const grades = new Set();
  const blind = new Map();

  for (const r of rows) {
    const grade = Number(r[0]);
    const category = asJson(r[2]);
    const expression = asJson(r[3]);
    const answer = Number(r[4]);
    grades.add(grade);

    if (!isFinite(answer)) {
      bad.push(`答案不是数字：${expression}`);
      continue;
    }

    const res = verifyMath(expression, answer);
    if (res === null) {
      blind.set(category, (blind.get(category) || 0) + 1);
      continue;
    }

    checked++;
    if (!res.ok) {
      bad.push(`${expression} 题库答案 ${answer}，实算 ${res.note}`);
    }
  }

  const skipped = rows.length - checked;
  ok('口算答案可验算的比例 ≥ 70%', checked >= rows.length * 0.7,
    `可验算 ${checked} / 共 ${rows.length}（${skipped} 道文字/图形题本脚本算不了）`);
  ok('口算题库答案全部算得对', bad.length === 0, bad.slice(0, 8).join('；'));

  for (let g = 5; g <= 9; g++) {
    ok(`口算题库覆盖 ${g} 年级`, grades.has(g));
  }

  console.log(`     口算：${rows.length} 道，验算 ${checked} 道，未验算 ${skipped} 道`);
  if (blind.size > 0) {
    const top = [...blind.entries()].sort((a, b) => b[1] - a[1]).slice(0, 8)
      .map((e) => `${e[0]} ${e[1]}`).join('，');
    console.log(`     未验算的题型：${top}`);
  }
}

// ───────────────────────── 模拟试卷 ─────────────────────────

function checkExam() {
  const src = read(path.join(DATA, 'ExamData.ets'));
  const papers = collectCalls(src, 'p');
  const questions = collectCalls(src, 'q');

  ok('试卷数 = 21（3~9 年级 × 三科）', papers.length === 21, `实际 ${papers.length}`);
  ok('题目总数 = 252', questions.length === 252, `实际 ${questions.length}`);

  const byGrade = new Map();
  const subjects = new Set();
  for (const p of papers) {
    const grade = Number(p[1]);
    const subject = asJson(p[2]);
    subjects.add(subject);
    byGrade.set(grade, (byGrade.get(grade) || 0) + 1);
  }
  for (let g = 3; g <= 9; g++) {
    ok(`${g} 年级有 3 套卷`, byGrade.get(g) === 3, `实际 ${byGrade.get(g)}`);
  }
  ok('三科齐全', subjects.size === 3, [...subjects].join(','));

  const problems = [];
  let choices = 0;
  let fills = 0;
  let judges = 0;

  for (const q of questions) {
    const ordinal = Number(q[0]);
    const kind = asJson(q[1]);
    const stem = asJson(q[2]);
    const options = asJson(q[3]);
    const answer = asJson(q[4]);
    const explain = asJson(q[5]);

    if (!stem) problems.push(`第 ${ordinal} 题题干为空`);
    if (!answer) problems.push(`第 ${ordinal} 题答案为空`);
    if (!explain) problems.push(`第 ${ordinal} 题没有解析`);

    if (kind === 'choice') {
      choices++;
      if (options.length < 2) problems.push(`第 ${ordinal} 题选项不足 2 个`);
      const idx = answer.charCodeAt(0) - 65;
      if (!(idx >= 0 && idx < options.length)) {
        problems.push(`第 ${ordinal} 题答案 ${answer} 越界（${options.length} 个选项）`);
      }
      if (new Set(options).size !== options.length) {
        problems.push(`第 ${ordinal} 题选项重复`);
      }
    } else if (kind === 'judge') {
      judges++;
      if (answer !== '对' && answer !== '错') problems.push(`第 ${ordinal} 题判断题答案是「${answer}」`);
      if (options.length !== 0) problems.push(`第 ${ordinal} 题判断题不该带选项`);
    } else if (kind === 'fill') {
      fills++;
      if (answer.split('|').filter((s) => s.length > 0).length === 0) {
        problems.push(`第 ${ordinal} 题填空题没有有效答案`);
      }
    } else {
      problems.push(`第 ${ordinal} 题题型未知：${kind}`);
    }
  }

  ok('考试题数据全部合法', problems.length === 0, problems.slice(0, 6).join('；'));
  ok('三种题型都有', choices > 0 && fills > 0 && judges > 0,
    `单选 ${choices} / 填空 ${fills} / 判断 ${judges}`);
  console.log(`     考试：${papers.length} 套卷 / ${questions.length} 题（单选 ${choices}、填空 ${fills}、判断 ${judges}）`);
}

// ───────────────────────── 安全问答 ─────────────────────────

function checkSafetyQuiz() {
  const src = read(path.join(DATA, 'SafetyQuiz.ets'));
  const rows = collectCalls(src, 'sq');
  ok('安全问答 ≥ 40 题', rows.length >= 40, `实际 ${rows.length}`);

  const cats = new Set();
  const bad = [];
  for (const r of rows) {
    const category = asJson(r[1]);
    const question = asJson(r[2]);
    const options = asJson(r[3]);
    const correct = Number(r[4]);
    const explain = asJson(r[5]);

    cats.add(category);
    if (!question) bad.push('有题干为空');
    if (options.length < 2) bad.push(`「${question}」选项不足`);
    if (!(correct >= 0 && correct < options.length)) bad.push(`「${question}」答案下标越界`);
    if (!explain) bad.push(`「${question}」没有讲解`);
    if (new Set(options).size !== options.length) bad.push(`「${question}」选项重复`);
  }

  ok('安全问答题数据全部合法', bad.length === 0, bad.slice(0, 6).join('；'));
  ok('安全问答覆盖 ≥ 8 个分类', cats.size >= 8, `实际 ${cats.size}`);
  console.log(`     安全问答：${rows.length} 题，${cats.size} 个分类`);
}

// ───────────────────────── 接线检查 ─────────────────────────

function checkWiring() {
  const rewards = read(path.join(ETS, 'core', 'Rewards.ets'));
  for (const key of ['oral', 'exam', 'safequiz']) {
    ok(`Rewards 登记了 ${key} 模块`, rewards.includes(`ModuleSpec('${key}'`));
  }

  const pages = {
    OralPracticePage: path.join(ETS, 'pages', 'OralPracticePage.ets'),
    ExamPage: path.join(ETS, 'pages', 'ExamPage.ets'),
    SafetyQuizPage: path.join(ETS, 'pages', 'SafetyQuizPage.ets')
  };
  const keys = { OralPracticePage: 'oral', ExamPage: 'exam', SafetyQuizPage: 'safequiz' };
  for (const name of Object.keys(pages)) {
    const src = read(pages[name]);
    const key = keys[name];
    const at = src.indexOf(`rewards.grant('${key}'`);
    ok(`${name} 上报 ${key}`, at >= 0);
    // 上报必须在 try 之外也要能编译，这里只查 key 与登记的模块一致
  }

  const routerSrc = read(path.join(ETS, 'core', 'Router.ets'));
  for (const page of ['LearnWorld', 'OralPractice', 'ExamList', 'Exam', 'SafetyQuiz']) {
    ok(`Router 有 ${page}`, new RegExp(`${page} = '`).test(routerSrc));
  }

  const indexSrc = read(path.join(ETS, 'pages', 'Index.ets'));
  for (const page of ['LearnWorldPage', 'OralPracticePage', 'ExamListPage', 'ExamPage', 'SafetyQuizPage']) {
    ok(`Index 注册了 ${page}`, indexSrc.includes(`${page}()`));
  }

  const home = read(path.join(ETS, 'pages', 'HomePage.ets'));
  ok('桌面有学习天地入口', home.includes("'学习天地'"));
  ok('桌面有安全问答入口', home.includes("'安全问答'"));

  // 生成的数据文件不能被手改：确认文件头带着「不要手改」的标记
  for (const f of ['ExamData.ets', 'MathBank.ets', 'SafetyQuiz.ets']) {
    const head = read(path.join(DATA, f)).slice(0, 600);
    ok(`${f} 带生成标记`, head.includes('port-') || head.includes('不要手改'));
  }
}

// ───────────────────────── 主流程 ─────────────────────────

checkMathBank();
checkExam();
checkSafetyQuiz();
checkWiring();

console.log('');
if (fails.length === 0) {
  console.log(`学习天地自检：${pass}/${pass} 全部通过`);
} else {
  console.log(`学习天地自检：通过 ${pass} 项，失败 ${fails.length} 项`);
  for (const f of fails) console.log('  ✗ ' + f);
  process.exitCode = 1;
}
