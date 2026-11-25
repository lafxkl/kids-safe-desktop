/**
 * 口诀乐园的内置内容：乘法口诀 / 加减法口诀 / 除法口诀。
 *
 * 与古诗、安全知识不同，这里的内容不是一段段写死的文本，而是**算出来的**：
 * 九九表、加法表、除法表都是循环生成的。C# 端用 MathRhymeData.cs 的
 * 三个 Build* 方法生成，这里按同样规则重写一遍。
 *
 * 全程离线，不依赖服务端——孩子点开就能背，断网也一样。
 */

/** 一个口诀主题（如乘法口诀），含若干节内容。 */
export class RhymeTopic {
  emoji: string = '📚';
  title: string = '';
  /** 主题色，用于标题和卡片描边。 */
  color: string = '#7E57C2';
  items: RhymeItem[] = [];
}

/** 一节内容。四种形态：verse=说明文字、list=逐条贴士、grid=查表、phrases=口诀串。 */
export class RhymeItem {
  /** verse / list / grid / phrases */
  kind: string = 'verse';
  title: string = '';
  verse: string = '';
  lines: string[] = [];
  grid: string[][] = [];
}

/** 中文数字，下标即数值。 */
const DIGITS: string[] = ['零', '一', '二', '三', '四', '五', '六', '七', '八', '九'];

/** 1-81 的中文读法：8→八，10→十，12→十二，25→二十五。 */
function chineseNumber(value: number): string {
  if (value < 10) {
    return DIGITS[value];
  }
  const tens: number = Math.floor(value / 10);
  const ones: number = value % 10;
  const tail: string = ones === 0 ? '' : DIGITS[ones];
  if (tens === 1) {
    // 10 读「十」而不是「一十」，12 读「十二」
    return `十${tail}`;
  }
  return `${DIGITS[tens]}十${tail}`;
}

/**
 * 一句乘法口诀，如「二三得六」「三四十二」。
 *
 * 注意：C# 端这里少写了一个「得」——乘积小于 10 时输出「一一一」，
 * 念出来不成话。教材写法是「一一得一」，所以移植时补上。
 */
function phrase(a: number, b: number): string {
  const product: number = a * b;
  const connector: string = product < 10 ? '得' : '';
  return `${DIGITS[a]}${DIGITS[b]}${connector}${chineseNumber(product)}`;
}

function verseItem(text: string, title: string = ''): RhymeItem {
  const item: RhymeItem = new RhymeItem();
  item.kind = 'verse';
  item.title = title;
  item.verse = text;
  return item;
}

function listItem(lines: string[], title: string = ''): RhymeItem {
  const item: RhymeItem = new RhymeItem();
  item.kind = 'list';
  item.title = title;
  item.lines = lines;
  return item;
}

function gridItem(grid: string[][], title: string = ''): RhymeItem {
  const item: RhymeItem = new RhymeItem();
  item.kind = 'grid';
  item.title = title;
  item.grid = grid;
  return item;
}

/**
 * 口诀串：每行由若干短语组成，短语之间用**两个空格**分隔。
 *
 * 单独列一种形态，是因为小九九一行可能有 9 个短语（「九一得九  九二一十八 …」），
 * 当成普通段落排会被挤成一句、在中途硬折行；拆成短语交给页面按行自动换行才整齐。
 */
function phrasesItem(lines: string[], title: string = ''): RhymeItem {
  const item: RhymeItem = new RhymeItem();
  item.kind = 'phrases';
  item.title = title;
  item.lines = lines;
  return item;
}

function topic(emoji: string, title: string, color: string, items: RhymeItem[]): RhymeTopic {
  const t: RhymeTopic = new RhymeTopic();
  t.emoji = emoji;
  t.title = title;
  t.color = color;
  t.items = items;
  return t;
}

/** 乘法：9×9 查表 + 小九九三角口诀。 */
function buildMultiplication(): RhymeTopic {
  const grid: string[][] = [];
  for (let a = 1; a <= 9; a++) {
    const row: string[] = [];
    for (let b = 1; b <= 9; b++) {
      row.push(`${a}×${b}=${a * b}`);
    }
    grid.push(row);
  }

  // 小九九是三角形：第 a 行只到 b=a，念起来才是「一一得一 / 一二得二 二二得四 / …」
  const lines: string[] = [];
  for (let a = 1; a <= 9; a++) {
    const parts: string[] = [];
    for (let b = 1; b <= a; b++) {
      parts.push(phrase(a, b));
    }
    lines.push(parts.join('  '));
  }

  return topic('📚', '乘法口诀', '#8E24AA', [
    verseItem('从「一一得一」开始，一句一句背下来，算题又快又准！'),
    gridItem(grid, '九九乘法表'),
    phrasesItem(lines, '乘法口诀（小九九）')
  ]);
}

/** 加减法：凑十歌 + 10 以内加法表 + 减法窍门。 */
function buildAddSub(): RhymeTopic {
  const grid: string[][] = [];
  for (let a = 1; a <= 9; a++) {
    const row: string[] = [];
    for (let b = 1; b <= 9; b++) {
      row.push(`${a}+${b}=${a + b}`);
    }
    grid.push(row);
  }

  const couplet: string = '一九一九好朋友，\n二八二八手拉手，\n三七三七真亲密，\n四六四六一起走，\n五五凑成一双手。\n\n' +
    '看到 9 想 1，看到 8 想 2，看到 7 想 3，看到 6 想 4，看到 5 想 5。';

  return topic('➕', '加减法口诀', '#00897B', [
    verseItem(couplet, '凑十歌'),
    gridItem(grid, '10 以内加法表'),
    listItem([
      '减号前面叫「被减数」，减号后面叫「减数」，得数叫「差」。',
      '从大数里去掉小数，剩下多少就是差。',
      '10 减几：10-1=9，10-2=8，10-3=7，10-4=6，10-5=5，10-6=4，10-7=3，10-8=2，10-9=1。',
      '9 减几：9-1=8，9-2=7，9-3=6，9-4=5，9-5=4，9-6=3，9-7=2，9-8=1。',
      '加法和减法是好朋友：知道 3+5=8，就能算出 8-5=3、8-3=5。'
    ], '减法小窍门')
  ]);
}

/** 除法：除法表（a÷b=q）+ 窍门。 */
function buildDivision(): RhymeTopic {
  const grid: string[][] = [];
  for (let q = 1; q <= 9; q++) {
    const row: string[] = [];
    for (let b = 1; b <= 9; b++) {
      row.push(`${b * q}÷${b}=${q}`);
    }
    grid.push(row);
  }

  const intro: string = '除法是乘法的好朋友！\n会背乘法口诀，除法也就出来啦——\n因为 3×4=12，所以 12÷3=4、12÷4=3。';

  return topic('➗', '除法口诀', '#3949AB', [
    verseItem(intro, '一句话记住除法'),
    gridItem(grid, '除法表'),
    listItem([
      '除号前面叫「被除数」，后面叫「除数」，得数叫「商」。',
      '从大往小试：想「几乘除数等于被除数」，那个「几」就是商。',
      '15÷5=3 是因为 5×3=15；20÷4=5 是因为 4×5=20。',
      '平均分最直观：把 12 块糖平均分给 3 个小朋友，每人 4 块，就是 12÷3=4。'
    ], '除法小窍门')
  ]);
}

export const RHYME_TOPICS: RhymeTopic[] = [
  buildMultiplication(),
  buildAddSub(),
  buildDivision()
];
