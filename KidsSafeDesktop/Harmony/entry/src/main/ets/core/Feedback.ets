import { http } from '@kit.NetworkKit';
import { BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { store } from './Store';

const TAG: string = 'Feedback';
const DOMAIN: number = 0x0100;

/**
 * 上报地址：与 Windows / 安卓 / Linux 同一个接口、同一份后台数据。
 * 联调时改这里即可；正式版不要指向家长自建服务器 —— 那台机器开发者看不到。
 */
const BASE_URL: string = 'https://kids.siguangblog.cc';

/**
 * 版本号：鸿蒙端没有统一的运行时版本读取入口，这里跟 build-profile.json5 手工对齐。
 * 只用于反馈里的「哪个版本出的问题」，写错了不影响提交。
 */
const APP_VERSION: string = '1.20';

const QUEUE_KEY: string = 'feedback_queue';

/** 一条提交的结果。queued=true 表示断网已存本机，联网后会自动补传。 */
export class FeedbackResult {
  ok: boolean = false;
  queued: boolean = false;
  message: string = '';
}

/**
 * 待提交的负载。
 *
 * 字段名与 KidsSafeServer 的 FeedbackItem 对齐（camelCase）。
 * `public` 是 TS 关键字，不能当字段名，所以叫 sharedToPublic，
 * 序列化时再替换成服务端要的 `public`（见 toJson）。
 */
class Payload {
  id: string = '';
  kind: string = '';
  platform: string = 'harmony';
  appVersion: string = APP_VERSION;
  channel: string = 'harmony-parent-dashboard';
  sharedToPublic: boolean = false;
  contact: string | null = null;
  rating: number | null = null;
  nps: number | null = null;
  dims: Record<string, number> | null = null;
  tags: string[] | null = null;
  answers: Record<string, string> | null = null;
  content: string | null = null;
}

interface ServerResp {
  ok: boolean;
  error?: string;
}

/**
 * 用户反馈（意见反馈 / 满意度 / 问卷）。
 *
 * 断网不丢：发不出去先写本机队列（preferences），下次启动或下次提交时补发。
 * 不猜身份：只带平台与版本号，不带设备号、昵称、账号。
 */
class FeedbackSvc {
  private queue(): string[] {
    const raw: string = store.getString(QUEUE_KEY, '[]');
    try {
      const arr = JSON.parse(raw) as string[];
      return Array.isArray(arr) ? arr : [];
    } catch (e) {
      return [];
    }
  }

  private saveQueue(list: string[]): void {
    store.setString(QUEUE_KEY, JSON.stringify(list));
  }

  pendingCount(): number {
    return this.queue().length;
  }

  private toJson(p: Payload): string {
    // sharedToPublic → public：服务端字段名是 public，TS 里是关键字
    return JSON.stringify(p).replace('"sharedToPublic":', '"public":');
  }

  private newId(): string {
    // 不引 crypto：这里只需要「不重复」，时间戳 + 随机数足够
    return `h-${Date.now().toString(36)}-${Math.floor(Math.random() * 1e9).toString(36)}`;
  }

  private base(kind: string, contact: string, allowPublic: boolean): Payload {
    const p = new Payload();
    p.id = this.newId();
    p.kind = kind;
    p.contact = contact.trim().length > 0 ? contact.trim() : null;
    p.sharedToPublic = allowPublic;
    return p;
  }

  feedback(category: string, content: string, contact: string, allowPublic: boolean): Payload {
    const p = this.base('feedback', contact, allowPublic);
    p.content = content.trim();
    p.tags = [category];
    return p;
  }

  satisfaction(rating: number, nps: number, dims: Record<string, number>, tags: string[],
    content: string, contact: string, allowPublic: boolean): Payload {
    const p = this.base('satisfaction', contact, allowPublic);
    p.rating = rating;
    if (nps >= 0) {
      p.nps = nps;
    }
    p.dims = dims;
    p.tags = tags.length > 0 ? tags : null;
    p.content = content.trim().length > 0 ? content.trim() : null;
    return p;
  }

  survey(answers: Record<string, string>, content: string, contact: string,
    allowPublic: boolean): Payload {
    const p = this.base('survey', contact, allowPublic);
    p.answers = answers;
    p.content = content.trim().length > 0 ? content.trim() : null;
    return p;
  }

  /**
   * 提交。先入队再发：即使发送途中应用被杀，这条也不会丢。
   * 成功后从队列移除；网络类失败保留，等下次补传。
   */
  async submit(p: Payload): Promise<FeedbackResult> {
    const list: string[] = this.queue();
    const body: string = this.toJson(p);
    list.push(body);
    this.saveQueue(list);
    return await this.flush();
  }

  /** 把队列里的逐条发出去，返回成功条数。 */
  async flush(): Promise<FeedbackResult> {
    const result = new FeedbackResult();
    let list: string[] = this.queue();
    if (list.length === 0) {
      result.ok = true;
      result.message = '';
      return result;
    }

    let sent: number = 0;
    while (list.length > 0) {
      const body: string = list[0];
      const step: FeedbackResult = await this.post(body);
      if (step.queued) {
        // 还是断网：保留在队列里，别再试后面的了
        result.ok = true;
        result.queued = true;
        result.message = `网络不太好，已存在本机，联网后自动补传（待发 ${list.length} 条）`;
        this.saveQueue(list);
        return result;
      }
      if (!step.ok) {
        // 服务端明确拒绝：这条重试也不会成功，丢掉并说明原因
        list.shift();
        this.saveQueue(list);
        result.ok = false;
        result.message = step.message;
        continue;
      }
      list.shift();
      this.saveQueue(list);
      sent++;
    }

    result.ok = true;
    result.message = sent > 0 ? '已提交 ✓ 谢谢你的反馈' : '';
    return result;
  }

  private async post(body: string): Promise<FeedbackResult> {
    const res = new FeedbackResult();
    const req = http.createHttp();
    try {
      const resp = await req.request(`${BASE_URL}/api/feedback/submit`, {
        method: http.RequestMethod.POST,
        header: { 'Content-Type': 'application/json' },
        extraData: body,
        connectTimeout: 12000,
        readTimeout: 12000,
        expectDataType: http.HttpDataType.STRING
      });

      const code: number = resp.responseCode;
      if (code >= 200 && code < 300) {
        res.ok = true;
        return res;
      }

      if (code === 429 || code >= 500) {
        res.queued = true;
        return res;
      }

      let reason: string = `提交失败（HTTP ${code}）`;
      try {
        const parsed = JSON.parse(resp.result as string) as ServerResp;
        if (parsed.error !== undefined && parsed.error.length > 0) {
          reason = parsed.error;
        }
      } catch (e) {
        // 网关错误页不是 JSON，保持默认文案
      }
      res.message = reason;
      return res;
    } catch (err) {
      const e = err as BusinessError;
      hilog.warn(DOMAIN, TAG, '提交失败 code=%{public}d msg=%{public}s', e.code, e.message);
      res.queued = true;
      return res;
    } finally {
      req.destroy();
    }
  }
}

export const feedback: FeedbackSvc = new FeedbackSvc();
