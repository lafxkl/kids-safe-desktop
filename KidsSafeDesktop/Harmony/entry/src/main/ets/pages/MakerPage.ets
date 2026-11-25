import { webview } from '@kit.ArkWeb';
import { fileIo as fs, picker } from '@kit.CoreFileKit';
import { photoAccessHelper } from '@kit.MediaLibraryKit';
import { util } from '@kit.ArkTS';
import { common } from '@kit.AbilityKit';
import { BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { router } from '../core/Router';
import { store } from '../core/Store';
import { PageHeader } from '../components/PageHeader';

const TAG: string = 'Maker';
const DOMAIN: number = 0x0100;

/** 单张图片上限：网页会把图压到 320px 再存，这里只防把 10 MB 原图当字符串过桥。 */
const MAX_IMAGE_BYTES: number = 8 * 1024 * 1024;

/**
 * 给网页用的存储桥。
 *
 * 为什么不能直接用 localStorage：鸿蒙的 Web 组件加载 `resource://rawfile` 页面时，
 * 这个 origin 不一定给了 opaquestorage —— 表现是「当时存得进去，重开就空」。
 * 家长辛苦做的作品每次消失是最不能接受的失败，所以作品改走 preferences：
 * 网页里的 LS 适配层发现有 window.KSStore 就优先用它（见 index.html 第 2 节）。
 *
 * 方法名必须与 javaScriptProxy 的 methodList 一致；参数和返回值都只能是基本类型。
 */
class MakerStoreBridge {
  /** 读一个键。返回空串表示没有 —— 网页那边会退回 localStorage。 */
  get(key: string): string {
    return store.getString(key, '');
  }

  /** 写一个键。 */
  set(key: string, value: string): void {
    store.setString(key, value);
  }
}

/**
 * 小游戏工坊（鸿蒙端）。
 *
 * 编辑器本体是四端共用的一份 H5（resources/rawfile/maker/index.html），
 * 这里只做宿主才能做的三件事：选图导入、读作品文件、写作品文件。
 * 模板、素材库、试玩、存档判断全在网页里，四端不会各写一套。
 */
@ComponentV2
export struct MakerPage {
  private controller: webview.WebviewController = new webview.WebviewController();
  private bridge: MakerStoreBridge = new MakerStoreBridge();

  @Local loading: boolean = true;
  @Local toast: string = '';

  private toastTimer: number = -1;

  build() {
    Column({ space: 10 }) {
      PageHeader({
        title: '小游戏工坊',
        subtitle: '选玩法、换素材，做出能玩的小游戏',
        onBack: () => {
          router.back();
        }
      })
        .padding({ left: 20, right: 20, top: 12 })

      Stack({ alignContent: Alignment.Bottom }) {
        Web({ src: $rawfile('maker/index.html'), controller: this.controller })
          .width('100%')
          .height('100%')
          .backgroundColor('#FFFFFF')
          // 作品存在 preferences（经 KSStore 桥），localStorage 只是兜底，
          // 但关掉它会让网页里 try/catch 之外的兜底分支直接抛异常，所以保持开启。
          .domStorageAccess(true)
          .javaScriptAccess(true)
          .fileAccess(true)
          .imageAccess(true)
          .zoomAccess(false)
          .horizontalScrollBarAccess(false)
          .darkMode(WebDarkMode.Off)
          .javaScriptProxy({
            object: this.bridge,
            name: 'KSStore',
            methodList: ['get', 'set'],
            controller: this.controller
          })
          .onPageEnd(() => {
            this.loading = false;
          })
          .onErrorReceive(() => {
            this.loading = false;
            this.showToast('工坊页面加载失败');
          })
          .onLoadIntercept((event) => {
            // 只放行本地包。作品里若带外链，一律拦掉（儿童端没有地址栏）。
            const url: string = event.data.getRequestUrl();
            if (url.startsWith('resource://rawfile/') || url.startsWith('about:') ||
              url.startsWith('data:') || url.startsWith('blob:')) {
              return false;
            }
            return true;
          })

        if (this.loading) {
          Column({ space: 12 }) {
            LoadingProgress()
              .width(48)
              .height(48)
              .color('#7C5CE0')
            Text('正在打开工坊…')
              .fontSize(16)
              .fontColor('#7A6759')
          }
          .width('100%')
          .height('100%')
          .justifyContent(FlexAlign.Center)
          .backgroundColor('#FFF8EE')
        }

        if (this.toast.length > 0) {
          Text(this.toast)
            .fontSize(14)
            .fontColor('#FFFFFF')
            .backgroundColor('#CC4A3B32')
            .borderRadius(18)
            .padding({ left: 18, right: 18, top: 9, bottom: 9 })
            .margin({ bottom: 92 })
            .maxLines(2)
            .textOverflow({ overflow: TextOverflow.Ellipsis })
        }
      }
      .layoutWeight(1)
      .width('100%')
      .borderRadius(18)
      .clip(true)
      .backgroundColor('#FFFFFF')

      // 底部动作条：三个按钮等宽，手指够得到
      Row({ space: 10 }) {
        this.actionButton('🖼 导入素材', () => {
          this.importImage();
        })
        this.actionButton('📥 打开作品', () => {
          this.importProject();
        })
        this.actionButton('📤 导出作品', () => {
          this.exportProject();
        })
      }
      .width('100%')
      .padding({ left: 20, right: 20, bottom: 16 })
    }
    .width('100%')
    .height('100%')
    .backgroundColor('#FFF8EE')
  }

  @Builder
  actionButton(label: string, tap: () => void) {
    Text(label)
      .fontSize(14)
      .fontWeight(FontWeight.Medium)
      .fontColor('#FFFFFF')
      .backgroundColor('#7C5CE0')
      .borderRadius(12)
      .height(46)
      .layoutWeight(1)
      .textAlign(TextAlign.Center)
      .onClick(() => {
        tap();
      })
  }

  private showToast(text: string): void {
    this.toast = text;
    if (this.toastTimer >= 0) {
      clearTimeout(this.toastTimer);
    }
    this.toastTimer = setTimeout(() => {
      this.toast = '';
      this.toastTimer = -1;
    }, 3200);
  }

  /** 跑一段 JS，返回结果字符串；失败返回空串。 */
  private async runJs(script: string): Promise<string> {
    try {
      const result: string = await this.controller.runJavaScript(script);
      return result ?? '';
    } catch (err) {
      const e = err as BusinessError;
      hilog.error(DOMAIN, TAG, '执行 JS 失败 code=%{public}d msg=%{public}s', e.code, e.message);
      return '';
    }
  }

  // ===== 导入素材 =====
  private importImage(): void {
    const phPicker: photoAccessHelper.PhotoViewPicker = new photoAccessHelper.PhotoViewPicker();
    phPicker.select({
      MIMEType: photoAccessHelper.PhotoViewMIMETypes.IMAGE_TYPE,
      maxSelectNumber: 1
    }).then(async (result: photoAccessHelper.PhotoSelectResult) => {
      if (result.photoUris.length === 0) {
        return;
      }
      const uri: string = result.photoUris[0];
      const bytes: Uint8Array = this.readAll(uri);
      if (bytes.length === 0) {
        this.showToast('这张图读不出来');
        return;
      }
      if (bytes.length > MAX_IMAGE_BYTES) {
        this.showToast('这张图太大了，换张小一点的');
        return;
      }

      const b64: string = new util.Base64Helper().encodeToStringSync(bytes);
      // data URL 的 mime 写 image/* 之外的值会让 <img> 不解析，
      // 统一按 png 声明：浏览器按内容嗅探，不会因为扩展名不对而失败。
      const dataUrl: string = 'data:image/png;base64,' + b64;
      const name: string = uri.substring(uri.lastIndexOf('/') + 1);

      const payload: string = JSON.stringify({ name: name, mime: 'image/png', dataUrl: dataUrl });
      await this.runJs(`window.KS.importAsset(${payload});`);
      this.showToast('素材已导入');
    }).catch((err: BusinessError) => {
      hilog.warn(DOMAIN, TAG, '选图取消或失败 code=%{public}d', err.code);
    });
  }

  // ===== 打开作品 =====
  private importProject(): void {
    const ctx = getContext(this) as common.UIAbilityContext;
    const docPicker: picker.DocumentViewPicker = new picker.DocumentViewPicker(ctx);
    docPicker.select({
      maxSelectNumber: 1,
      fileSuffixFilters: ['.json']
    }).then(async (uris: Array<string>) => {
      if (uris.length === 0) {
        return;
      }
      const bytes: Uint8Array = this.readAll(uris[0]);
      if (bytes.length === 0) {
        this.showToast('这个文件读不出来');
        return;
      }
      // 网页按 UTF-8 解 base64，这里把字节原样送过去即可
      const b64: string = new util.Base64Helper().encodeToStringSync(bytes);
      const script: string = `window.KS.loadProjectBase64("${b64}");`;
      const result: string = await this.runJs(script);
      if (result === 'ok') {
        this.showToast('作品已打开');
      } else {
        this.showToast('这个文件不是作品');
      }
    }).catch((err: BusinessError) => {
      hilog.warn(DOMAIN, TAG, '选文件取消或失败 code=%{public}d', err.code);
    });
  }

  // ===== 导出作品 =====
  private exportProject(): void {
    // 一次取回「内容 + 名字」：runJavaScript 只能拿回字符串，
    // 所以让网页把两样东西打成一条 JSON 再交回来。
    const script: string = '(function(){var b=window.KS.currentProjectBase64();' +
      'return JSON.stringify({b64:b, name:(window.KS.projectName?window.KS.projectName():"")});})()';

    this.runJs(script).then(async (raw: string) => {
      if (raw.length === 0) {
        this.showToast('先打开或新建一个作品再导出');
        return;
      }

      let b64: string = '';
      let name: string = '我的小游戏';
      try {
        const parsed = JSON.parse(raw) as MakerExport;
        b64 = parsed.b64 ?? '';
        if (parsed.name !== undefined && parsed.name.length > 0) {
          name = parsed.name;
        }
      } catch (err) {
        hilog.error(DOMAIN, TAG, '解析导出结果失败');
        return;
      }
      if (b64.length === 0) {
        this.showToast('先打开或新建一个作品再导出');
        return;
      }

      const bytes: Uint8Array = new util.Base64Helper().decodeSync(b64);
      await this.saveToFile(name, bytes);
    });
  }

  private async saveToFile(name: string, bytes: Uint8Array): Promise<void> {
    try {
      const ctx = getContext(this) as common.UIAbilityContext;
      const docPicker: picker.DocumentViewPicker = new picker.DocumentViewPicker(ctx);
      const safeName: string = this.sanitize(name) + '.json';
      const uris: Array<string> = await docPicker.save({ newFileNames: [safeName] });
      if (uris.length === 0) {
        return;
      }

      const file = fs.openSync(uris[0], fs.OpenMode.READ_WRITE | fs.OpenMode.CREATE | fs.OpenMode.TRUNC);
      try {
        fs.writeSync(file.fd, bytes.buffer as ArrayBuffer);
      } finally {
        fs.closeSync(file);
      }
      this.showToast('已导出：' + safeName);
    } catch (err) {
      const e = err as BusinessError;
      hilog.error(DOMAIN, TAG, '导出失败 code=%{public}d msg=%{public}s', e.code, e.message);
      this.showToast('导出失败：' + e.message);
    }
  }

  private sanitize(raw: string): string {
    let name: string = (raw ?? '').trim();
    if (name.length === 0) {
      name = '我的小游戏';
    }
    const bad: string = '/\\:*?"<>|';
    for (let i = 0; i < bad.length; i++) {
      name = name.replace(bad.charAt(i), '_');
    }
    return name.length > 60 ? name.substring(0, 60) : name;
  }

  /** 把 uri 指向的文件整体读进内存。 */
  private readAll(uri: string): Uint8Array {
    let file: fs.File | null = null;
    try {
      file = fs.openSync(uri, fs.OpenMode.READ_ONLY);
      const size: number = fs.statSync(file.fd).size;
      if (size <= 0) {
        return new Uint8Array(0);
      }
      const buffer: ArrayBuffer = new ArrayBuffer(size);
      fs.readSync(file.fd, buffer);
      return new Uint8Array(buffer);
    } catch (err) {
      const e = err as BusinessError;
      hilog.error(DOMAIN, TAG, '读文件失败 code=%{public}d msg=%{public}s', e.code, e.message);
      return new Uint8Array(0);
    } finally {
      if (file !== null) {
        try {
          fs.closeSync(file);
        } catch (err) {
          // 关不掉也没别的办法
        }
      }
    }
  }
}

/** runJavaScript 交回来的那条 JSON。 */
interface MakerExport {
  b64?: string;
  name?: string;
}
