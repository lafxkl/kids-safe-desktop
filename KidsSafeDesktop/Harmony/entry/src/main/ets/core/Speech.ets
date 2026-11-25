import { textToSpeech } from '@kit.CoreSpeechKit';
import { BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';

const TAG: string = 'Speech';
const DOMAIN: number = 0x0105;
/** 引擎单次上限 10000 字符，留足余量按段落切。 */
const CHUNK: number = 600;

/**
 * 对应 C# 的 Helpers/Tts.cs。
 *
 * 鸿蒙自带离线中文合成（CoreSpeechKit），不需要额外装语音包，
 * 这一点比 Linux 端（要 speech-dispatcher + 中文包）省事得多。
 * 代价是音色只有「聆小珊」一个，且只能中文。
 */
@ObservedV2
export class Speech {
  @Trace speaking: boolean = false;
  @Trace ready: boolean = false;

  private engine: textToSpeech.TextToSpeechEngine | null = null;
  private pending: boolean = false;

  async prepare(): Promise<void> {
    if (this.engine !== null) {
      return;
    }
    try {
      // online: 1 表示离线合成（0 在线模式 SDK 尚未支持）
      // person: 13 = 聆小珊女声（官方推荐值，0 也能用）
      this.engine = await textToSpeech.createEngine({ language: 'zh-CN', person: 13, online: 1 });
      this.engine.setListener({
        onStart: (requestId: string) => {
          this.speaking = true;
        },
        onData: (requestId: string, audio: ArrayBuffer) => {
          // 直接播报模式不需要自己处理音频流
        },
        onComplete: (requestId: string) => {
          // speak() 返回 void，播报何时结束只能靠这个回调，所以 pending 必须在这里放开，
          // 否则队列会永远卡在「正在播第一段」。
          this.speaking = false;
          this.pending = false;
          this.playNext();
        },
        onStop: (requestId: string) => {
          this.speaking = false;
          this.pending = false;
        },
        onError: (requestId: string, errorCode: number, errorMessage: string) => {
          this.speaking = false;
          this.pending = false;
          hilog.warn(DOMAIN, TAG, '合成失败 code=%{public}d msg=%{public}s', errorCode, errorMessage);
        }
      });
      this.ready = true;
    } catch (err) {
      const e = err as BusinessError;
      hilog.warn(DOMAIN, TAG, 'TTS 引擎不可用 code=%{public}d', e.code);
      this.ready = false;
    }
  }

  /** 朗读整段文本，按段落排队播报。 */
  async speak(text: string): Promise<void> {
    await this.prepare();
    if (this.engine === null) {
      return;
    }
    this.queue = this.split(text);
    this.playNext();
  }

  stop(): void {
    this.queue = [];
    this.speaking = false;
    if (this.engine !== null) {
      this.engine.stop();
    }
  }

  private queue: string[] = [];

  private playNext(): void {
    if (this.pending || this.queue.length === 0 || this.engine === null) {
      return;
    }
    const chunk = this.queue.shift();
    if (chunk === undefined) {
      return;
    }
    this.pending = true;
    try {
      // 注意：TextToSpeechEngine.speak() 返回 void（不是 Promise），
      // 所以这里不能 .then()，结束信号来自 onComplete 回调。
      this.engine.speak(chunk, { requestId: `ks-${Date.now()}` });
    } catch (err) {
      this.pending = false;
      const e = err as BusinessError;
      hilog.warn(DOMAIN, TAG, '播报失败 code=%{public}d', e.code);
    }
  }

  private split(text: string): string[] {
    const out: string[] = [];
    let rest: string = text.trim();
    while (rest.length > CHUNK) {
      let cut = rest.lastIndexOf('。', CHUNK);
      if (cut < CHUNK / 2) {
        cut = rest.lastIndexOf('\n', CHUNK);
      }
      if (cut < CHUNK / 2) {
        cut = CHUNK;
      }
      out.push(rest.substring(0, cut + 1));
      rest = rest.substring(cut + 1).trim();
    }
    if (rest.length > 0) {
      out.push(rest);
    }
    return out;
  }
}

export const speech: Speech = new Speech();
