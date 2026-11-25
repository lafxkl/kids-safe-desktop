/**
 * 绘本资源清单。内容与 KidsSafeShared/Models/ReadingData.cs 一一对应。
 *
 * C# 是主版本（Windows / Android 读它），官网 care-sources.html 第八节也有一份。
 * 改文案时三处一起改 —— 这三处都是「给人看的说明」，不同步比不同步代码更刺眼。
 *
 * 为什么不从服务端下发：条目少、都是机构官网、变化以年计，而这几家本来就要联网才能看，
 * 离线可读就够了。多一套接口 + 缓存反而增加不一致的机会。
 */

export class ReadingSource {
  id: string = '';
  emoji: string = '';
  name: string = '';
  url: string = '';
  lang: string = '';
  ages: string = '';
  what: string = '';
  how: string = '';
  note: string = '';
}

function mkSource(id: string, emoji: string, name: string, url: string, lang: string,
  ages: string, what: string, how: string, note: string): ReadingSource {
  const s: ReadingSource = new ReadingSource();
  s.id = id;
  s.emoji = emoji;
  s.name = name;
  s.url = url;
  s.lang = lang;
  s.ages = ages;
  s.what = what;
  s.how = how;
  s.note = note;
  return s;
}

export class BookPick {
  title: string = '';
  original: string = '';
  author: string = '';
  why: string = '';
  searchUrl: string = '';
}

function mkBook(title: string, original: string, author: string, why: string,
  searchUrl: string): BookPick {
  const b: BookPick = new BookPick();
  b.title = title;
  b.original = original;
  b.author = author;
  b.why = why;
  b.searchUrl = searchUrl;
  return b;
}

/** 清单修订日期，与 C# 的 ReadingCatalog.UpdatedAt 一致。 */
export const READING_UPDATED_AT: string = '2026-11-24';

export const READING_SOURCES: ReadingSource[] = [
  mkSource('icdl', '🌍', '国际儿童数字图书馆', 'https://en.childrenslibrary.org/',
    '多语种（含少量中文）', '3–13 岁',
    'ICDL 基金会做的公益数字图书馆，把各国童书扫描后在网页上直接翻开看，可以按年龄、主题、语言筛选。',
    '进站先选语言，再选年龄；不用注册、不用装软件，点封面就能一页页翻。',
    '只能在线阅读，不提供下载；不少书出版年代较早，画风偏旧，但内容干净、没有广告。'),
  mkSource('storyline', '🎬', 'Storyline Online', 'https://storylineonline.net/',
    '英文（真人朗读）', '4–10 岁',
    '美国演员工会基金会（SAG-AFTRA Foundation）的公益项目：演员给孩子朗读绘本，画面就是书的插图，配字幕。',
    '选一本书放视频，和孩子一起听；每本还附一份活动指南，可以照着提问或做手工。',
    '是「听别人读」，不是电子书，也没有中文；适合亲子共听和练听力，不适合当识字材料。'),
  mkSource('openlibrary', '📚', 'Open Library', 'https://openlibrary.org/',
    '英文为主，含部分中文', '全年龄',
    'Internet Archive 的开放图书目录，收录上千万条书目：公版书可以直接读全文，受版权保护的书可以线上借阅。',
    '搜书名或作者，看页面上有没有「Read」或「Borrow」按钮；借阅要注册一个免费账号，借期 1 小时，可以续借。',
    '不是每本都有电子版，标着「Borrow」的才能借；借阅和全文阅读都要联网。')
];

/**
 * 家长最常问的几本。都是受版权保护的作品 —— 只给「查馆藏 / 借阅」的入口，
 * 不给任何 PDF 直链，也不建议去找网盘版。
 */
export const READING_PICKS: BookPick[] = [
  mkBook('猜猜我有多爱你', 'Guess How Much I Love You', 'Sam McBratney',
    '睡前读最合适。读完可以和孩子比一比「谁的爱更多」，比说十遍「我爱你」都管用。',
    'https://openlibrary.org/search?q=Guess+How+Much+I+Love+You'),
  mkBook('好饿的毛毛虫', 'The Very Hungry Caterpillar', 'Eric Carle',
    '数数、星期、认识食物，一本书能玩好几样；洞洞书版本还能用手指抠。',
    'https://openlibrary.org/search?q=The+Very+Hungry+Caterpillar'),
  mkBook('棕熊，棕熊，你在看什么？', 'Brown Bear, Brown Bear, What Do You See?',
    'Bill Martin Jr. / Eric Carle',
    '句式重复，孩子听两遍就能跟着念，是很省力的英语启蒙入口。',
    'https://openlibrary.org/search?q=Brown+Bear+Brown+Bear+What+Do+You+See'),
  mkBook('月亮，晚安', 'Goodnight Moon', 'Margaret Wise Brown',
    '把房间里的东西一件件说晚安，节奏慢下来，适合当睡前仪式的一部分。',
    'https://openlibrary.org/search?q=Goodnight+Moon'),
  mkBook('野兽国', 'Where the Wild Things Are', 'Maurice Sendak',
    '讲怒气怎么来的、又怎么散的。孩子闹过脾气之后读，比说「别生气」有用。',
    'https://openlibrary.org/search?q=Where+the+Wild+Things+Are')
];

/** 版权与使用边界。原样显示在页面底部，改文案时别删。 */
export const READING_RULES: string[] = [
  '只收「明确免费、可公开访问」的来源：公益图书馆、基金会或出版社授权的项目。不放网盘合集，也不放扫描版 PDF。',
  '网盘里常见的「620 本」「520 本经典绘本」来源大多不明，属于未授权扫描。下载和转发都可能侵权，文件里还常夹带广告或可执行程序。',
  '中文绘本优先借：城市少儿图书馆、学校图书室大多有；国家图书馆少儿馆可以线上预约后到馆取。',
  '经典绘本的平装版大多二三十元，比一次出游便宜。真想长期反复读，买一本比存一堆来源不明的文件踏实。'
];
