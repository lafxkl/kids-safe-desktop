import { store } from './Store';

/**
 * 「学会了哪些」的本地账本。
 *
 * 星星账本只知道「这个模块赚了多少星」，不知道具体是哪几首。
 * 但排行榜要报「背会 12 首」这种成绩，就必须记住集合 —— 同一首点两次不能算两首。
 *
 * 存成 id 数组的 JSON，量很小（古诗 26、口诀几十、安全 10、故事几十）。
 */
export class LearnProgress {
  private storeKey: string = '';
  private ids: string[] = [];
  private loaded: boolean = false;

  constructor(storeKey: string) {
    this.storeKey = storeKey;
  }

  load(): void {
    if (this.loaded) {
      return;
    }
    this.loaded = true;
    const raw: string = store.getString(this.storeKey, '');
    if (raw.length === 0) {
      return;
    }
    try {
      const arr = JSON.parse(raw) as string[];
      this.ids = [];
      for (let i = 0; i < arr.length; i++) {
        this.ids.push(arr[i]);
      }
    } catch (err) {
      this.ids = [];
    }
  }

  /** 这个条目之前学过没有。用来决定按钮是「我学会了」还是「已学会」。 */
  has(id: string): boolean {
    this.load();
    for (let i = 0; i < this.ids.length; i++) {
      if (this.ids[i] === id) {
        return true;
      }
    }
    return false;
  }

  count(): number {
    this.load();
    return this.ids.length;
  }

  /**
   * 标记学会。返回「是不是第一次」——
   * 第一次才发星，重复点只更新一下界面，不然孩子会把按钮当老虎机按。
   */
  mark(id: string): boolean {
    this.load();
    if (this.has(id)) {
      return false;
    }
    this.ids.push(id);
    store.setString(this.storeKey, JSON.stringify(this.ids));
    return true;
  }
}

export const poemLearned: LearnProgress = new LearnProgress('learned.poem');
export const rhymeLearned: LearnProgress = new LearnProgress('learned.rhyme');
export const safetyLearned: LearnProgress = new LearnProgress('learned.safety');
export const storyLearned: LearnProgress = new LearnProgress('learned.story');
