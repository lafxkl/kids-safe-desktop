import { store } from './Store';
import { rewards } from './Rewards';
import { moodOf } from '../data/CareData';

/**
 * 心情记录与「想对爸妈说的话」的本地账本。对应 C# 的 Services/CareJournal.cs。
 *
 * 隐私取舍（留守儿童场景尤其重要，改这段代码前请先读一遍）：
 * - 全部只写本机 preferences，不上传、不同步、不需要登录；
 * - 不记位置、不记联系人、不做任何分析上报；
 * - 内容只在「心情日记」和「想对爸妈说」两页可见。家长面板**不展示明细**，
 *   只展示「记了几天」—— 孩子需要一个可以安全放情绪的地方，
 *   如果知道家长能翻，这个功能立刻就没人用了。
 */

const KEY_ENTRIES: string = 'care.moods.v1';
const KEY_LETTERS: string = 'care.letters.v1';
/** 只留最近 180 天。再多也没有意义，而且 JSON 会越读越慢。 */
const KEEP_DAYS: number = 180;
/** 最多 100 句。超了删最旧的，不提示 —— 提示只会让孩子不敢写。 */
const MAX_LETTERS: number = 100;

export class MoodEntry {
  /** yyyy-MM-dd。同一天只留一条，这是唯一键。 */
  date: string = '';
  moodId: string = '';
  /** 补的一句话，可以为空 */
  note: string = '';
  /** HH:mm */
  time: string = '';
}

export class ParentLetter {
  date: string = '';
  time: string = '';
  text: string = '';
}

/** 记一次心情的结果。星星只在「今天第一次记」时给。 */
export class MoodSaved {
  firstToday: boolean = false;
  stars: number = 0;
  /** 改完之后今天的连续天数，界面直接显示 */
  streak: number = 0;
}

function pad2(n: number): string {
  return n < 10 ? `0${n}` : `${n}`;
}

/** 本地日期键。不能用 toISOString —— 那是 UTC，晚上 8 点之后会算成第二天。 */
export function dayKey(d: Date): string {
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}

export function todayKey(): string {
  return dayKey(new Date());
}

function nowTime(): string {
  const d: Date = new Date();
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
}

/** 昨天往前数 n 天，返回 yyyy-MM-dd。用 Date 构造而不是减毫秒，跨月跨年都不会错。 */
function dayBefore(n: number): string {
  const t: Date = new Date();
  return dayKey(new Date(t.getFullYear(), t.getMonth(), t.getDate() - n));
}

export class CareStore {
  private entries: MoodEntry[] = [];
  private items: ParentLetter[] = [];
  private loaded: boolean = false;

  load(): void {
    if (this.loaded) {
      return;
    }
    this.loaded = true;
    this.entries = this.readEntries();
    this.items = this.readLetters();
  }

  private readEntries(): MoodEntry[] {
    const raw: string = store.getString(KEY_ENTRIES, '');
    if (raw.length === 0) {
      return [];
    }
    try {
      const arr = JSON.parse(raw) as MoodEntry[];
      const out: MoodEntry[] = [];
      for (let i = 0; i < arr.length; i++) {
        const e: MoodEntry = new MoodEntry();
        e.date = arr[i].date !== undefined ? arr[i].date : '';
        e.moodId = arr[i].moodId !== undefined ? arr[i].moodId : '';
        e.note = arr[i].note !== undefined ? arr[i].note : '';
        e.time = arr[i].time !== undefined ? arr[i].time : '';
        // 日期或心情缺失的坏记录直接丢掉：画不出来，留着只会让 streak 算错
        if (e.date.length === 10 && e.moodId.length > 0) {
          out.push(e);
        }
      }
      return out;
    } catch (err) {
      return [];
    }
  }

  private readLetters(): ParentLetter[] {
    const raw: string = store.getString(KEY_LETTERS, '');
    if (raw.length === 0) {
      return [];
    }
    try {
      const arr = JSON.parse(raw) as ParentLetter[];
      const out: ParentLetter[] = [];
      for (let i = 0; i < arr.length; i++) {
        const l: ParentLetter = new ParentLetter();
        l.date = arr[i].date !== undefined ? arr[i].date : '';
        l.time = arr[i].time !== undefined ? arr[i].time : '';
        l.text = arr[i].text !== undefined ? arr[i].text : '';
        if (l.text.length > 0) {
          out.push(l);
        }
      }
      return out;
    } catch (err) {
      return [];
    }
  }

  private saveEntries(): void {
    store.setString(KEY_ENTRIES, JSON.stringify(this.entries));
  }

  private saveLetters(): void {
    store.setString(KEY_LETTERS, JSON.stringify(this.items));
  }

  /** 全部记录，按日期升序。 */
  all(): MoodEntry[] {
    this.load();
    return this.entries;
  }

  todayEntry(): MoodEntry | null {
    this.load();
    const key: string = todayKey();
    for (let i = 0; i < this.entries.length; i++) {
      if (this.entries[i].date === key) {
        return this.entries[i];
      }
    }
    return null;
  }

  /**
   * 记一次今天的心情。同一天再记会覆盖，但不再发星。
   *
   * 为什么「重记不发星」：这条规则是给孩子留反悔空间的 —— 早上记了「开心」，
   * 下午难过想改成「想人」，如果改一次就给一颗星，孩子会把它当老虎机。
   */
  saveMood(moodId: string, note: string): MoodSaved {
    this.load();
    const key: string = todayKey();
    let firstToday: boolean = true;

    for (let i = 0; i < this.entries.length; i++) {
      if (this.entries[i].date === key) {
        firstToday = false;
        break;
      }
    }

    const fresh: MoodEntry = new MoodEntry();
    fresh.date = key;
    fresh.moodId = moodId;
    fresh.note = note.length > 0 ? note : '';
    fresh.time = nowTime();

    const next: MoodEntry[] = [];
    for (let i = 0; i < this.entries.length; i++) {
      if (this.entries[i].date !== key) {
        next.push(this.entries[i]);
      }
    }
    next.push(fresh);

    // 剪掉过期记录。字符串比较对 yyyy-MM-dd 是安全的（等长、高位在前）。
    const cutoff: string = dayBefore(KEEP_DAYS);
    const kept: MoodEntry[] = [];
    for (let i = 0; i < next.length; i++) {
      if (next[i].date >= cutoff) {
        kept.push(next[i]);
      }
    }
    kept.sort((a: MoodEntry, b: MoodEntry) => a.date < b.date ? -1 : (a.date > b.date ? 1 : 0));

    this.entries = kept;
    this.saveEntries();

    const result: MoodSaved = new MoodSaved();
    result.firstToday = firstToday;
    const streakNow: number = this.streak();
    result.streak = streakNow;
    if (firstToday) {
      // 直接进星星账本，走「care」模块的每日上限。
      // score 报连续天数：排行榜上「心情与关怀」显示成「最佳 9 天」，
      // 比「最佳 1 颗星」有信息量。
      result.stars = rewards.grant('care', 1, streakNow, `连续 ${streakNow} 天`);
    }
    return result;
  }

  /**
   * 连续记录了几天。
   *
   * 「今天还没记」不清零，从昨天往前数 —— 否则孩子早上打开应用看到「0 天」，
   * 会觉得昨天白记了，反而更容易放弃。
   */
  streak(): number {
    this.load();
    if (this.entries.length === 0) {
      return 0;
    }
    const days: string[] = [];
    for (let i = 0; i < this.entries.length; i++) {
      days.push(this.entries[i].date);
    }

    let cursor: number = days.indexOf(todayKey()) >= 0 ? 0 : 1;
    let streak: number = 0;
    while (days.indexOf(dayBefore(cursor)) >= 0) {
      streak++;
      cursor++;
    }
    return streak;
  }

  /** 最近 n 天（含今天）的记录，缺的那天给 null，界面按位置画空格子。 */
  recent(n: number): (MoodEntry | null)[] {
    this.load();
    const out: (MoodEntry | null)[] = [];
    for (let i = n - 1; i >= 0; i--) {
      const key: string = dayBefore(i);
      let hit: MoodEntry | null = null;
      for (let j = 0; j < this.entries.length; j++) {
        if (this.entries[j].date === key) {
          hit = this.entries[j];
          break;
        }
      }
      out.push(hit);
    }
    return out;
  }

  /** 出现过最多的心情，没记录时返回空串。 */
  mostMoodId(): string {
    this.load();
    const counts: Map<string, number> = new Map<string, number>();
    for (let i = 0; i < this.entries.length; i++) {
      const id: string = this.entries[i].moodId;
      const n: number = counts.get(id) !== undefined ? counts.get(id) as number : 0;
      counts.set(id, n + 1);
    }
    let best: string = '';
    let bestN: number = 0;
    counts.forEach((n: number, id: string) => {
      if (n > bestN) {
        bestN = n;
        best = id;
      }
    });
    return best;
  }

  countOf(moodId: string): number {
    this.load();
    let n: number = 0;
    for (let i = 0; i < this.entries.length; i++) {
      if (this.entries[i].moodId === moodId) {
        n++;
      }
    }
    return n;
  }

  /** 最近 n 天里记了几天。 */
  recordedIn(n: number): number {
    const list: (MoodEntry | null)[] = this.recent(n);
    let c: number = 0;
    for (let i = 0; i < list.length; i++) {
      if (list[i] !== null) {
        c++;
      }
    }
    return c;
  }

  /** 最新的在前，列表直接按这个顺序画。 */
  letters(): ParentLetter[] {
    this.load();
    const out: ParentLetter[] = [];
    for (let i = this.items.length - 1; i >= 0; i--) {
      out.push(this.items[i]);
    }
    return out;
  }

  letterCount(): number {
    this.load();
    return this.items.length;
  }

  addLetter(text: string): void {
    this.load();
    const t: string = text.trim();
    if (t.length === 0) {
      return;
    }
    const l: ParentLetter = new ParentLetter();
    l.date = todayKey();
    l.time = nowTime();
    l.text = t;
    this.items.push(l);
    if (this.items.length > MAX_LETTERS) {
      this.items = this.items.slice(this.items.length - MAX_LETTERS);
    }
    this.saveLetters();
  }

  /** 按「界面上的下标」删（界面是倒序的，所以要和存储顺序对一下）。 */
  removeLetter(indexFromTop: number): void {
    this.load();
    const at: number = this.items.length - 1 - indexFromTop;
    if (at < 0 || at >= this.items.length) {
      return;
    }
    this.items.splice(at, 1);
    this.saveLetters();
  }
}

export const care: CareStore = new CareStore();

/** 日期显示成「3/14」或「今天」。 */
export function shortDay(key: string): string {
  if (key === todayKey()) {
    return '今天';
  }
  if (key.length === 10) {
    const m: number = Number(key.substring(5, 7));
    const d: number = Number(key.substring(8, 10));
    return `${m}/${d}`;
  }
  return key;
}
