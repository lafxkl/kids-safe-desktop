/**
 * 关怀中心的数据与接线自检。
 *
 * 这套内容是「写给孩子的心里话」，出错的方式和别的功能不一样：
 * 它不会崩、不会报错，只会安静地传达错的东西。所以脚本盯三类事：
 *
 *   ① 结构完整性：每节课都得有 KeyLine 和正文，少一段就是一页空白；
 *   ② 内容红线：不许出现「想开点」这类无效安慰，自伤话题必须指向真实热线；
 *   ③ 接线正确：页面没注册、星星 key 没登记，功能就静默失效。
 *
 * 用法：node tools/check-care.js
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

const read = (p) => fs.readFileSync(p, 'utf8');

/** 和 check-learn.js 同一套：按括号配平扫调用参数。 */
function collectCalls(src, name) {
  const out = [];
  let i = 0;
  while (i < src.length) {
    const at = src.indexOf(name + '(', i);
    if (at < 0) break;
    const before = at > 0 ? src[at - 1] : '\n';
    if (/[A-Za-z0-9_]/.test(before)) {
      i = at + name.length;
      continue;
    }
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

/** 参数是字符串字面量，解析回来。数据文件用双引号（JSON.stringify 生成），页面代码用单引号。 */
function str(lit) {
  if (lit === undefined || lit === null) return '';
  const t = String(lit).trim();
  if (t.length === 0) return '';
  if (t.startsWith('"')) {
    try {
      return JSON.parse(t);
    } catch (e) {
      return '';
    }
  }
  if (t.startsWith("'") && t.endsWith("'")) {
    return t.slice(1, -1).replace(/\\(['\\])/g, '$1');
  }
  return t;
}

// ───────────────────────── 数据 ─────────────────────────

const careSrc = read(path.join(DATA, 'CareData.ets'));

function checkMoods() {
  const rows = collectCalls(careSrc, 'mk');
  ok('心情选项 8 个', rows.length === 8, `实际 ${rows.length}`);

  const ids = new Set();
  for (const r of rows) {
    const id = str(r[0]);
    const emoji = str(r[1]);
    const label = str(r[2]);
    const hex = str(r[4]);
    ok(`心情「${id || '?'}」字段齐全`, id.length > 0 && emoji.length > 0 && label.length > 0,
      JSON.stringify(r));
    ok(`心情「${id}」颜色合法`, /^#[0-9A-Fa-f]{6}$/.test(hex), hex);
    ok(`心情「${id}」不重复`, !ids.has(id), 'id 重复');
    ids.add(id);
  }

  // moodOf 找不到时回退 MOODS[1]，那个位置必须真的存在，否则历史存档里的脏 id 会崩
  ok('moodOf 回退位存在', careSrc.includes('return MOODS[1];') && rows.length > 1);
  for (const must of ['lonely', 'sad']) {
    ok(`心情里有「${must}」`, ids.has(must), '日记的观察文案依赖这两个 id');
  }
}

function checkWarm() {
  const at = careSrc.indexOf('WARM_WORDS');
  const seg = careSrc.slice(at, careSrc.indexOf('];', at));
  const words = [];
  const re = /"((?:[^"\\]|\\.)*)"/g;
  let m;
  while ((m = re.exec(seg)) !== null) words.push(JSON.parse('"' + m[1] + '"'));

  ok('暖心话至少 8 句', words.length >= 8, `实际 ${words.length}`);
  for (const w of words) {
    ok('暖心话没有空句', w.trim().length > 0);
    ok(`暖心话「${w.slice(0, 8)}…」不是命令句`, !/^(你要|你必须|你应该)/.test(w), w);
  }
}

function checkChannels() {
  const rows = collectCalls(careSrc, 'hc');
  ok('求助渠道至少 4 个', rows.length >= 4, `实际 ${rows.length}`);

  const numbers = [];
  for (const r of rows) {
    const name = str(r[1]);
    const number = str(r[2]);
    const when = str(r[3]);
    ok(`渠道「${name}」有名称和说明`, name.length > 0 && when.length > 0);
    if (number.length > 0) {
      ok(`渠道「${name}」号码是纯数字`, /^[\d-]+$/.test(number), number);
      numbers.push(number);
    }
  }

  // 这两条是内容承诺，不是格式检查：
  // 一个出现自伤念头的孩子，必须在应用里找得到真实可拨的号码
  ok('内置 12355 青少年服务台', numbers.includes('12355'), numbers.join(' / '));
  ok('内置紧急号码 110 或 120', numbers.includes('110') || numbers.includes('120'));
  const urgent = rows.filter((r) => r[4] === 'true').length;
  ok('至少有一个紧急渠道标红', urgent >= 1, `实际 ${urgent} 个`);
}

function checkBreathing() {
  const rows = collectCalls(careSrc, 'bp');
  ok('呼吸分 4 段', rows.length === 4, `实际 ${rows.length}`);
  const secs = rows.map((r) => Number(r[2]));
  // 4-7-8 是这套方法的名字，前三个数字错了，它就不是 4-7-8 了
  ok('呼吸秒数是 4 / 7 / 8', secs[0] === 4 && secs[1] === 7 && secs[2] === 8, secs.join('/'));
  for (let i = 0; i < rows.length; i++) {
    ok(`呼吸第 ${i + 1} 段有标题和说明`,
      str(rows[i][0]).length > 0 && str(rows[i][1]).length > 0);
  }
}

function checkLessons() {
  const rows = collectCalls(careSrc, 'ls');
  ok('心理小课堂有课', rows.length >= 10, `实际 ${rows.length}`);

  const ids = new Set();
  let kids = 0;
  let parents = 0;
  let sectionsOk = 0;

  for (const r of rows) {
    const id = str(r[0]);
    const title = str(r[2]);
    const subtitle = str(r[3]);
    const tag = str(r[4]);
    const audience = str(r[5]);
    const minutes = Number(r[6]);
    const keyLine = str(r[7]);
    const where = `课程「${title || id}」`;

    ok(`${where} id 唯一`, !ids.has(id), 'id 重复');
    ids.add(id);

    ok(`${where} 有标题`, title.length > 0);
    ok(`${where} 有副标题`, subtitle.length > 0);
    ok(`${where} 有主题标签`, tag.length > 0);
    ok(`${where} 有 KeyLine`, keyLine.length > 0, '详情页顶部那句提纲会是空的');
    ok(`${where} 时长合理`, minutes >= 1 && minutes <= 10, String(minutes));
    ok(`${where} 视角合法`, audience === 'kids' || audience === 'parents', audience);

    if (audience === 'parents') parents++;
    else kids++;

    // 第 9 个参数是 [p(...), p(...)]
    const paras = collectCalls(r[8] || '', 'p');
    ok(`${where} 有正文段落`, paras.length >= 2, `实际 ${paras.length} 段`);
    let good = 0;
    for (const p of paras) {
      const heading = str(p[0]);
      const body = str(p[1]);
      if (heading.length > 0 && body.length > 0 && body.length <= 400) good++;
    }
    ok(`${where} 每段都有小标题和正文`, good === paras.length,
      `${good}/${paras.length}`);
    if (paras.length > 0 && good === paras.length) sectionsOk++;
  }

  ok('给孩子和给家长两套都有', kids > 0 && parents > 0, `孩子 ${kids} / 家长 ${parents}`);
  ok('所有课程的正文都完整', sectionsOk === rows.length, `${sectionsOk}/${rows.length}`);

  // ── 内容红线 ──
  // 「想开点」这类话对正在难受的孩子是二次伤害：它把情绪变成了「你不该这样」。
  // 写作原则第 4 条要求指向可执行的动作，所以这几句一律不许出现。
  // 只查课程正文：文件头注释里为了说明这条规则，本身就写了这几个词。
  const lessonsBody = careSrc.slice(careSrc.indexOf('export const LESSONS'));
  const banned = ['想开点', '别想太多', '没什么大不了', '坚强一点', '想那么多干嘛'];
  for (const b of banned) {
    ok(`课程正文里没有「${b}」`, !lessonsBody.includes(b), '这句话属于无效安慰，请换成具体动作');
  }

  // 出现「自伤 / 不想活」这类话题的课程，必须给出热线
  const hot = ['自伤', '不想活', '活着没意思', '伤害自己'];
  const hasHot = hot.some((h) => lessonsBody.includes(h));
  if (hasHot) {
    ok('提到自伤话题的地方给了热线', lessonsBody.includes('12355'), '必须指向真实可拨的号码');
  }
}

// ───────────────────────── 接线 ─────────────────────────

function checkWiring() {
  const routerSrc = read(path.join(ETS, 'core', 'Router.ets'));
  for (const p of ['CareCenter', 'MoodJournal', 'CareLesson', 'CareLessonDetail', 'Breathing', 'LetterToParents']) {
    ok(`Router 有 ${p}`, routerSrc.includes(`${p} = `), '页面没枚举就跳不过去');
  }

  const indexSrc = read(path.join(ETS, 'pages', 'Index.ets'));
  for (const p of ['CareCenterPage', 'MoodJournalPage', 'CareLessonPage', 'CareLessonDetailPage',
    'BreathingPage', 'LetterToParentsPage']) {
    ok(`Index 注册 ${p}`, indexSrc.includes(`${p}()`), '打开会是白屏');
  }

  const homeSrc = read(path.join(ETS, 'pages', 'HomePage.ets'));
  ok('桌面有关怀中心入口', homeSrc.includes("'关怀中心'"));

  // 发星 key 必须登记在 MODULES，否则成绩上不了排行榜（grant 会落到兜底 spec）
  const rewardsSrc = read(path.join(ETS, 'core', 'Rewards.ets'));
  for (const key of ['care', 'carelesson']) {
    ok(`Rewards 登记了「${key}」`, rewardsSrc.includes(`new ModuleSpec('${key}'`));
  }

  // 页面上真实的 grant 调用。
  // CareCenterPage 不在列表里：心情打卡的星星由 core/Care.ets 的 saveMood 代发
  // （记心情和发星必须是一次原子操作，拆成两步就会出现「记上了但没星」）。
  const grantFiles = ['BreathingPage.ets', 'CareLessonDetailPage.ets'];
  for (const f of grantFiles) {
    const src = read(path.join(ETS, 'pages', f));
    const calls = collectCalls(src, 'grant');
    ok(`${f} 有星星上报`, calls.length > 0, '做完事没星星，孩子会以为白做了');
    for (const c of calls) {
      const key = str(c[0]);
      ok(`${f} 上报的 key「${key}」已登记`, rewardsSrc.includes(`new ModuleSpec('${key}'`), key);
    }
  }

  const centerSrc = read(path.join(ETS, 'pages', 'CareCenterPage.ets'));
  ok('关怀中心调用了 saveMood', centerSrc.includes('.saveMood('), '记心情必须走存储层才能发星');

  // 家长后台的入口：家长得找得到那 10 节课，但又不能看到孩子的记录
  const dashSrc = read(path.join(ETS, 'pages', 'ParentDashboardPage.ets'));
  ok('家长后台有心理关怀入口', dashSrc.includes('careCard()'));
  ok('家长后台能进给家长的课', dashSrc.includes("router.careAudience = 'parents'"),
    '进去还停在孩子那套就白点了');
  ok('家长后台只取天数不取内容',
    dashSrc.includes('care.recordedIn(') && !dashSrc.includes('care.all()'),
    '心情明细不能出现在家长面板');

  // 数据文件是脚本生成的，不能手改
  const head = careSrc.slice(0, 500);
  ok('CareData.ets 带生成标记', head.includes('port-care.js') && head.includes('不要手改'));
}

// ───────────────────────── 主流程 ─────────────────────────

checkMoods();
checkWarm();
checkChannels();
checkBreathing();
checkLessons();
checkWiring();

console.log('');
if (fails.length === 0) {
  console.log(`关怀中心自检：${pass}/${pass} 全部通过`);
} else {
  console.log(`关怀中心自检：通过 ${pass} 项，失败 ${fails.length} 项`);
  for (const f of fails) console.log('  ✗ ' + f);
  process.exitCode = 1;
}
