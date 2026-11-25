import { http } from '@kit.NetworkKit';
import { BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { settings } from './Settings';
import { CategoriesResp, StoryCategory, StoryDetail, StoryListResp, StorySummary } from '../model/Models';

const TAG: string = 'Story';
const DOMAIN: number = 0x0106;

/**
 * 对应 C# 的 IStoryService / StoryService，服务端契约一字未改：
 *   GET /api/story/types
 *   GET /api/story/list?typeId=&page=
 *   GET /api/story/search?keyword=&page=
 *   GET /api/story/detail?storyId=
 *
 * 沿用原设计那条铁律：任何失败都降级成「一句人话 + 空列表」，
 * 绝不把异常抛到界面上。故事拉不到只是少了内容，不能让孩子看到报错弹窗。
 */
@ObservedV2
export class StoryService {
  @Trace onlineEnabled: boolean = false;
  @Trace loading: boolean = false;
  @Trace detailLoading: boolean = false;
  @Trace errorText: string = '';
  @Trace categories: StoryCategory[] = [];
  @Trace items: StorySummary[] = [];
  @Trace hasMore: boolean = false;
  @Trace detail: StoryDetail | null = null;
  /** 当前选中的分类，供分类 chips 高亮。 */
  @Trace currentTypeId: number = 0;

  private currentPage: number = 0;
  private currentKeyword: string = '';

  refreshOnlineState(): void {
    const base = this.baseUrl();
    this.onlineEnabled = base.length > 0;
    if (!this.onlineEnabled) {
      this.errorText = '家长还没开启在线故事';
    } else if (this.errorText === '家长还没开启在线故事') {
      this.errorText = '';
    }
  }

  async loadCategories(): Promise<void> {
    this.refreshOnlineState();
    if (!this.onlineEnabled) {
      return;
    }
    const body = await this.get('/api/story/types');
    if (body === null) {
      return;
    }
    try {
      const resp = JSON.parse(body) as CategoriesResp;
      const list: StoryCategory[] = [];
      if (resp.categories !== undefined) {
        for (let i = 0; i < resp.categories.length; i++) {
          const c = resp.categories[i];
          list.push({ id: c.id, name: c.name, emoji: StoryService.emojiOf(c.name) });
        }
      }
      this.categories = list;
      if (list.length > 0 && this.currentTypeId === 0) {
        await this.loadList(list[0].id, 1);
      }
    } catch (err) {
      this.errorText = '故事数据格式不对，请联系家长';
    }
  }

  async loadList(typeId: number, page: number): Promise<void> {
    this.currentTypeId = typeId;
    this.currentKeyword = '';
    await this.fetchPage(`/api/story/list?typeId=${typeId}&page=${page}`, page);
  }

  async loadMore(): Promise<void> {
    if (this.loading || !this.hasMore) {
      return;
    }
    if (this.currentKeyword.length > 0) {
      const body = await this.get(`/api/story/search?keyword=${encodeURIComponent(this.currentKeyword)}&page=${this.currentPage + 1}`);
      this.applyPage(body, this.currentPage + 1, false);
      return;
    }
    if (this.currentTypeId === 0) {
      return;
    }
    const body = await this.get(`/api/story/list?typeId=${this.currentTypeId}&page=${this.currentPage + 1}`);
    this.applyPage(body, this.currentPage + 1, false);
  }

  async search(keyword: string): Promise<void> {
    this.currentKeyword = keyword.trim();
    if (this.currentKeyword.length === 0) {
      if (this.currentTypeId !== 0) {
        await this.loadList(this.currentTypeId, 1);
      }
      return;
    }
    const body = await this.get(`/api/story/search?keyword=${encodeURIComponent(this.currentKeyword)}&page=1`);
    this.applyPage(body, 1, true);
  }
  async loadDetail(id: number): Promise<void> {
    this.detailLoading = true;
    this.detail = null;
    const body = await this.get(`/api/story/detail?storyId=${id}`);
    if (body !== null) {
      try {
        this.detail = JSON.parse(body) as StoryDetail;
      } catch (err) {
        this.errorText = '这个故事打不开，换一个吧';
      }
    }
    this.detailLoading = false;
  }

  private async fetchPage(path: string, page: number): Promise<void> {
    const body = await this.get(path);
    this.applyPage(body, page, true);
  }

  private applyPage(body: string | null, page: number, replace: boolean): void {
    if (body === null) {
      if (replace) {
        this.items = [];
        this.hasMore = false;
      }
      return;
    }
    try {
      const resp = JSON.parse(body) as StoryListResp;
      const incoming: StorySummary[] = resp.items !== undefined ? resp.items : [];
      if (replace) {
        this.items = incoming;
      } else {
        const merged: StorySummary[] = [];
        for (let i = 0; i < this.items.length; i++) {
          merged.push(this.items[i]);
        }
        for (let i = 0; i < incoming.length; i++) {
          merged.push(incoming[i]);
        }
        this.items = merged;
      }
      this.currentPage = page;
      this.hasMore = resp.hasMore === true;
      if (this.items.length === 0) {
        this.errorText = this.currentKeyword.length > 0 ? '没有找到这个故事' : '这个分类还没有故事';
      } else {
        this.errorText = '';
      }
    } catch (err) {
      this.errorText = '故事数据格式不对，请联系家长';
    }
  }

  private baseUrl(): string {
    if (!settings.onlineContentEnabled) {
      return '';
    }
    let url = settings.serverUrl.trim();
    while (url.length > 0 && url.endsWith('/')) {
      url = url.substring(0, url.length - 1);
    }
    return url;
  }

  private async get(path: string): Promise<string | null> {
    const base = this.baseUrl();
    if (base.length === 0) {
      this.errorText = '家长还没开启在线故事';
      return null;
    }
    this.loading = true;
    const req = http.createHttp();
    try {
      const res = await req.request(`${base}${path}`, {
        method: http.RequestMethod.GET,
        connectTimeout: 12000,
        readTimeout: 12000,
        expectDataType: http.HttpDataType.STRING
      });
      if (res.responseCode === 200) {
        return res.result as string;
      }
      this.errorText = `服务器返回 ${res.responseCode}`;
      return null;
    } catch (err) {
      const e = err as BusinessError;
      hilog.warn(DOMAIN, TAG, '请求失败 code=%{public}d', e.code);
      this.errorText = '网络不太顺畅，等会儿再试试';
      return null;
    } finally {
      this.loading = false;
      req.destroy();
    }
  }

  private static emojiOf(name: string): string {
    if (name.indexOf('睡前') >= 0) {
      return '🌙';
    }
    if (name.indexOf('童话') >= 0) {
      return '🏰';
    }
    if (name.indexOf('寓言') >= 0) {
      return '🦊';
    }
    if (name.indexOf('成语') >= 0) {
      return '📜';
    }
    if (name.indexOf('科普') >= 0) {
      return '🔬';
    }
    return '📚';
  }
}

export const storyService: StoryService = new StoryService();
