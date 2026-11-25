import { store } from './Store';

/**
 * 星星账本 + 总排行榜 + 兑换商店。
 *
 * 为什么星星是唯一的排行榜单位：
 *   十一个游戏 + 六个学习模块的「成绩」根本不是一种东西 —— 象棋是胜局、古诗是首数、
 *   2048 是分数、成语是答对题数。硬凑成一个分数组，排行榜就变成「谁的分数虚高」，
 *   对孩子没有任何意义。所以排行榜排的是**星星**（做完一件事就发），
 *   每一行右侧再显示该模块自己的最佳成绩，两个维度各归各位。
 *
 * 防刷：孩子会把最简单的关反复刷。所以每个模块有每日星星上限（dailyCap），
 * 超了仍然记成绩、仍然上榜，只是不再长星星。
 */

export enum ModKind {
  /** 知识类：故事、古诗、口诀、安全 */
  Learn = 'learn',
  /** 游戏类 */
  Game = 'game'
}

/** 一个可上榜的模块。加新游戏 / 新知识页时往 MODULES 里加一条即可。 */
export class ModuleSpec {
  key: string = '';
  name: string = '';
  emoji: string = '';
  kind: ModKind = ModKind.Game;
  /** 成绩单位：局 / 首 / 题 / 分 / 关 */
  unit: string = '';
  /** 每天最多能靠这个模块拿多少星 */
  dailyCap: number = 30;

  constructor(key: string, name: string, emoji: string, kind: ModKind, unit: string, cap: number) {
    this.key = key;
    this.name = name;
    this.emoji = emoji;
    this.kind = kind;
    this.unit = unit;
    this.dailyCap = cap;
  }
}

export const MODULES: ModuleSpec[] = [
  // 知识类：每天上限高一些 —— 读诗、看故事本来就该多给
  new ModuleSpec('story', '故事屋', '🌙', ModKind.Learn, '本', 40),
  new ModuleSpec('poem', '古诗学习', '📜', ModKind.Learn, '首', 40),
  new ModuleSpec('rhyme', '口诀乐园', '📚', ModKind.Learn, '首', 40),
  new ModuleSpec('safety', '安全知识', '🛡️', ModKind.Learn, '条', 40),
  new ModuleSpec('safequiz', '安全问答', '❓', ModKind.Learn, '题', 30),
  // 口算和模拟考都是「练」，上限给到和知识类一样高：一天练 10 轮才 40 颗，不算刷
  new ModuleSpec('oral', '口算练习', '🧮', ModKind.Learn, '题', 40),
  new ModuleSpec('exam', '模拟考试', '📝', ModKind.Learn, '分', 40),
  // 关怀中心：心情一天只能记一次、呼吸放松一天做几次就够，所以上限压得很低。
  // 这一组不追求「多赚星」—— 它是情绪出口，不该被当成任务去刷。
  // score 记「连续记录天数」，榜单上显示成「最佳 9 天」。
  new ModuleSpec('care', '心情与关怀', '💗', ModKind.Learn, '天', 3),
  // 心理小课堂 20 节，每节只有第一次读完才发星，理论上刷不出来。
  new ModuleSpec('carelesson', '心理小课堂', '📚', ModKind.Learn, '节', 3),
  // 游戏类
  new ModuleSpec('xiangqi', '中国象棋', '♟️', ModKind.Game, '胜', 30),
  new ModuleSpec('gomoku', '五子棋', '⚫', ModKind.Game, '胜', 30),
  new ModuleSpec('sudoku', '数独挑战', '🔢', ModKind.Game, '局', 30),
  new ModuleSpec('game24', '24 点', '🎯', ModKind.Game, '局', 30),
  new ModuleSpec('chengyu', '成语大挑战', '📜', ModKind.Game, '题', 30),
  new ModuleSpec('game2048', '2048', '🔸', ModKind.Game, '分', 30),
  // 「记住它」按关卡计分：一关就是一次完整的记忆 + 还原，比按分刷更难，所以上限不压低
  new ModuleSpec('memory', '记住它', '🧠', ModKind.Game, '关', 30),
  new ModuleSpec('threes', '三三合成', '🧩', ModKind.Game, '分', 30),
  // 词语组合 / 单词组合：两个游戏分开记星，一关一次，各给一份独立进度
  new ModuleSpec('wordzh', '词语组合', '🌿', ModKind.Game, '关', 30),
  new ModuleSpec('worden', '单词组合', '🎈', ModKind.Game, '关', 30),
  // 老鼠吃奶酪移植自 Qt 版 mice_eat_cheese，按关卡算成绩
  new ModuleSpec('mouse', '老鼠吃奶酪', '🐭', ModKind.Game, '关', 30),
  new ModuleSpec('baobei36', '宝贝三十六计', '🕵️', ModKind.Game, '计', 30),
  new ModuleSpec('pvz', '植物大战僵尸', '🌻', ModKind.Game, '波', 30),
  new ModuleSpec('virusfighter', '病毒斗士', '🦠', ModKind.Game, '关', 30),
  new ModuleSpec('snake', '蛇蛇方块', '🐍', ModKind.Game, '分', 30),
  new ModuleSpec('kittens', '猫国', '🐱', ModKind.Game, '分', 20)
];

export function moduleOf(key: string): ModuleSpec {
  for (let i = 0; i < MODULES.length; i++) {
    if (MODULES[i].key === key) {
      return MODULES[i];
    }
  }
  // 未登记的上报不吞掉，给它一个兜底模块，排行榜里也能看见
  return new ModuleSpec(key, key, '⭐', ModKind.Game, '次', 20);
}

/** 某个模块的成绩记录。score 取历史最佳，times 是完成次数。 */
export class ModRecord {
  key: string = '';
  score: number = 0;
  sub: string = '';
  stars: number = 0;
  times: number = 0;
  at: number = 0;
}

/** 商店里的一件可兑换物品，完全由家长在后台配置。 */
export class ShopItem {
  id: string = '';
  title: string = '';
  emoji: string = '🎁';
  cost: number = 20;
  note: string = '';
  enabled: boolean = true;

  constructor(id: string, title: string, emoji: string, cost: number, note: string) {
    this.id = id;
    this.title = title;
    this.emoji = emoji;
    this.cost = cost;
    this.note = note;
  }
}

/** 一条兑换记录。done 由家长在后台点「已兑现」。 */
export class OrderItem {
  id: string = '';
  title: string = '';
  emoji: string = '🎁';
  cost: number = 0;
  at: number = 0;
  done: boolean = false;
}

/** 每日计数（按模块）。单独存，跨天自动清。 */
class DayStar {
  key: string = '';
  n: number = 0;
}

const KEY: string = 'reward.v1';

function todayKey(): string {
  const d = new Date();
  const m = d.getMonth() + 1;
  const day = d.getDate();
  return `${d.getFullYear()}-${m < 10 ? '0' + m : m}-${day < 10 ? '0' + day : day}`;
}

function pad2(n: number): string {
  return n < 10 ? `0${n}` : `${n}`;
}

/** 时间戳 → 「今天 14:20」/「11-14 09:05」，兑换记录和最近获得都用它。 */
export function timeLabel(ts: number): string {
  if (ts <= 0) {
    return '';
  }
  const d = new Date(ts);
  const now = new Date();
  const sameDay: boolean = d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth() &&
    d.getDate() === now.getDate();
  const hm = `${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
  return sameDay ? `今天 ${hm}` : `${pad2(d.getMonth() + 1)}-${pad2(d.getDate())} ${hm}`;
}

@ObservedV2
export class Rewards {
  /** 当前可以花的星星 */
  @Trace stars: number = 0;
  /** 历史累计（只增不减，用来显示「一共赚了多少」） */
  @Trace total: number = 0;
  /** 今日获得，跨天自动归零 */
  @Trace todayStars: number = 0;
  @Trace records: ModRecord[] = [];
  @Trace shop: ShopItem[] = [];
  @Trace orders: OrderItem[] = [];

  private day: string = '';
  private dayList: DayStar[] = [];
  private loaded: boolean = false;

  load(): void {
    if (this.loaded) {
      return;
    }
    this.loaded = true;
    const raw: string = store.getString(KEY, '');
    if (raw.length === 0) {
      this.seedShop();
      this.save();
      return;
    }
    try {
      const p = JSON.parse(raw) as RewardsBlob;
      this.stars = typeof p.stars === 'number' ? p.stars : 0;
      this.total = typeof p.total === 'number' ? p.total : 0;
      this.day = typeof p.day === 'string' ? p.day : '';
      this.todayStars = typeof p.today === 'number' ? p.today : 0;

      this.records = [];
      if (p.records) {
        for (let i = 0; i < p.records.length; i++) {
          const r = p.records[i];
          const rec = new ModRecord();
          rec.key = r.key;
          rec.score = typeof r.score === 'number' ? r.score : 0;
          rec.sub = typeof r.sub === 'string' ? r.sub : '';
          rec.stars = typeof r.stars === 'number' ? r.stars : 0;
          rec.times = typeof r.times === 'number' ? r.times : 0;
          rec.at = typeof r.at === 'number' ? r.at : 0;
          this.records.push(rec);
        }
      }

      this.dayList = [];
      if (p.caps) {
        for (let i = 0; i < p.caps.length; i++) {
          const ds = new DayStar();
          ds.key = p.caps[i].key;
          ds.n = p.caps[i].n;
          this.dayList.push(ds);
        }
      }

      this.shop = [];
      if (p.shop) {
        for (let i = 0; i < p.shop.length; i++) {
          const s = p.shop[i];
          const item = new ShopItem(s.id, s.title, s.emoji, s.cost, s.note);
          item.enabled = s.enabled !== false;
          this.shop.push(item);
        }
      }

      this.orders = [];
      if (p.orders) {
        for (let i = 0; i < p.orders.length; i++) {
          const o = p.orders[i];
          const od = new OrderItem();
          od.id = o.id;
          od.title = o.title;
          od.emoji = o.emoji;
          od.cost = o.cost;
          od.at = o.at;
          od.done = o.done === true;
          this.orders.push(od);
        }
      }
    } catch (err) {
      // 存档坏了宁可从头开始，也别让桌面起不来
      this.stars = 0;
      this.total = 0;
      this.records = [];
    }
    if (this.shop.length === 0) {
      this.seedShop();
    }
    this.rollDay();
  }

  save(): void {
    const blob: RewardsBlob = {
      stars: this.stars,
      total: this.total,
      day: this.day,
      today: this.todayStars,
      records: this.records,
      caps: this.dayList,
      shop: this.shop,
      orders: this.orders
    };
    store.setString(KEY, JSON.stringify(blob));
  }

  /** 跨天：今日星星和每模块计数一起清。必须在发星前调用。 */
  private rollDay(): void {
    const now: string = todayKey();
    if (this.day !== now) {
      this.day = now;
      this.todayStars = 0;
      this.dayList = [];
      this.save();
    }
  }

  private usedToday(key: string): number {
    for (let i = 0; i < this.dayList.length; i++) {
      if (this.dayList[i].key === key) {
        return this.dayList[i].n;
      }
    }
    return 0;
  }

  private addToday(key: string, n: number): void {
    for (let i = 0; i < this.dayList.length; i++) {
      if (this.dayList[i].key === key) {
        this.dayList[i].n = this.dayList[i].n + n;
        return;
      }
    }
    const ds = new DayStar();
    ds.key = key;
    ds.n = n;
    this.dayList.push(ds);
  }

  recordOf(key: string): ModRecord | null {
    for (let i = 0; i < this.records.length; i++) {
      if (this.records[i].key === key) {
        return this.records[i];
      }
    }
    return null;
  }

  todayOf(key: string): number {
    return this.usedToday(key);
  }

  /**
   * 上报一次成绩并发星。返回真正发出去的星数（0 表示今天这个模块封顶了）。
   *
   * score 的语义按模块不同：胜局数、最高分、答对题数都行 —— 记录里只存「最大值」，
   * 所以调用方可以直接把「这一局的成绩」传进来，比如 2048 传本局分数、
   * 象棋传 1（赢一局）。sub 是给人看的说明文字。
   */
  grant(key: string, want: number, score: number, sub: string): number {
    this.load();
    this.rollDay();
    const spec: ModuleSpec = moduleOf(key);
    const used: number = this.usedToday(key);
    const room: number = Math.max(0, spec.dailyCap - used);
    let given: number = Math.floor(want);
    if (given > room) {
      given = room;
    }
    if (given < 0) {
      given = 0;
    }

    const now: number = Date.now();
    let rec: ModRecord | null = this.recordOf(key);
    if (rec === null) {
      rec = new ModRecord();
      rec.key = key;
      this.records.push(rec);
    }
    rec.times = rec.times + 1;
    rec.stars = rec.stars + given;
    rec.at = now;
    if (score > rec.score || rec.sub.length === 0) {
      rec.score = score;
      rec.sub = sub;
    }

    this.stars = this.stars + given;
    this.total = this.total + given;
    this.todayStars = this.todayStars + given;
    if (given > 0) {
      this.addToday(key, given);
    }
    // 数组元素是对象，@Trace 只盯整体替换，这里手动踢一下让排行榜页刷新
    this.records = this.records.slice();
    this.save();
    return given;
  }

  /** 家长手动加/扣星（「今天表现特别好，奖 20 颗」）。 */
  adjust(delta: number, note: string): void {
    this.load();
    const next: number = this.stars + delta;
    this.stars = next < 0 ? 0 : next;
    if (delta > 0) {
      this.total = this.total + delta;
      this.todayStars = this.todayStars + delta;
    }
    const od = new OrderItem();
    od.id = `adj-${Date.now()}`;
    od.title = note.length > 0 ? note : (delta >= 0 ? '家长奖励' : '家长扣减');
    od.emoji = delta >= 0 ? '🌟' : '➖';
    od.cost = delta;
    od.at = Date.now();
    od.done = true;
    const list: OrderItem[] = [od];
    for (let i = 0; i < this.orders.length && i < 19; i++) {
      list.push(this.orders[i]);
    }
    this.orders = list;
    this.save();
  }

  /** 兑换。星星不够返回 false，调用方据此提示。 */
  redeem(item: ShopItem): boolean {
    this.load();
    if (this.stars < item.cost) {
      return false;
    }
    this.stars = this.stars - item.cost;
    const od = new OrderItem();
    od.id = `ord-${Date.now()}`;
    od.title = item.title;
    od.emoji = item.emoji;
    od.cost = item.cost;
    od.at = Date.now();
    od.done = false;
    const list: OrderItem[] = [od];
    for (let i = 0; i < this.orders.length && i < 19; i++) {
      list.push(this.orders[i]);
    }
    this.orders = list;
    this.save();
    return true;
  }

  markDone(orderId: string): void {
    for (let i = 0; i < this.orders.length; i++) {
      if (this.orders[i].id === orderId) {
        const o = this.orders[i];
        const next = new OrderItem();
        next.id = o.id;
        next.title = o.title;
        next.emoji = o.emoji;
        next.cost = o.cost;
        next.at = o.at;
        next.done = true;
        this.orders[i] = next;
      }
    }
    this.orders = this.orders.slice();
    this.save();
  }

  /** 待兑现的兑换单，家长后台红点用。 */
  pendingOrders(): number {
    let n = 0;
    for (let i = 0; i < this.orders.length; i++) {
      if (!this.orders[i].done) {
        n++;
      }
    }
    return n;
  }

  /** 按星星排的榜单；kind 传 null 表示全部。 */
  ranked(kind: ModKind | null): ModRecord[] {
    const out: ModRecord[] = [];
    for (let i = 0; i < this.records.length; i++) {
      const r = this.records[i];
      if (kind !== null && moduleOf(r.key).kind !== kind) {
        continue;
      }
      if (r.stars <= 0 && r.times <= 0) {
        continue;
      }
      out.push(r);
    }
    out.sort((a: ModRecord, b: ModRecord) => {
      if (b.stars !== a.stars) {
        return b.stars - a.stars;
      }
      return b.at - a.at;
    });
    return out;
  }

  /** 商店里的可换物品（家长关掉的不显示）。 */
  visibleShop(): ShopItem[] {
    const out: ShopItem[] = [];
    for (let i = 0; i < this.shop.length; i++) {
      if (this.shop[i].enabled) {
        out.push(this.shop[i]);
      }
    }
    out.sort((a: ShopItem, b: ShopItem) => a.cost - b.cost);
    return out;
  }

  addShopItem(title: string, emoji: string, cost: number, note: string): void {
    const item = new ShopItem(`it-${Date.now()}`, title, emoji.length > 0 ? emoji : '🎁',
      cost > 0 ? cost : 10, note);
    this.shop = this.shop.concat([item]);
    this.save();
  }

  removeShopItem(id: string): void {
    const out: ShopItem[] = [];
    for (let i = 0; i < this.shop.length; i++) {
      if (this.shop[i].id !== id) {
        out.push(this.shop[i]);
      }
    }
    this.shop = out;
    this.save();
  }

  toggleShopItem(id: string): void {
    for (let i = 0; i < this.shop.length; i++) {
      if (this.shop[i].id === id) {
        const s = this.shop[i];
        const next = new ShopItem(s.id, s.title, s.emoji, s.cost, s.note);
        next.enabled = !s.enabled;
        this.shop[i] = next;
      }
    }
    this.shop = this.shop.slice();
    this.save();
  }

  /**
   * 默认商品。家长不配置也能用 —— 空商店对孩子来说就是「没东西可换」，
   * 星星失去用处。这里给的都是「家长零成本、孩子很想要」的东西：
   * 加时、挑书、选晚餐、周末去哪玩。
   */
  private seedShop(): void {
    this.shop = [
      new ShopItem('it-tv', '看动画片 20 分钟', '📺', 30, '家长陪着看，看完就关'),
      new ShopItem('it-book', '挑一本新绘本', '📖', 60, '周末去书店自己选'),
      new ShopItem('it-dinner', '今晚点一道菜', '🍜', 40, '你说了算，家长做'),
      new ShopItem('it-park', '周末去公园玩', '🎡', 80, '带上滑板车')
    ];
  }
}

/** 落盘结构。字段用可选，老存档缺字段时按默认值兜底。 */
class RewardsBlob {
  stars: number = 0;
  total: number = 0;
  day: string = '';
  today: number = 0;
  records: ModRecord[] = [];
  caps: DayStar[] = [];
  shop: ShopItem[] = [];
  orders: OrderItem[] = [];
}

export const rewards: Rewards = new Rewards();
