import { cryptoFramework } from '@kit.CryptoArchitectureKit';
import { util } from '@kit.ArkTS';
import { BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';

const TAG: string = 'Crypto';
const DOMAIN: number = 0x0101;

function toHex(data: Uint8Array): string {
  let out: string = '';
  for (let i = 0; i < data.length; i++) {
    const part: string = data[i].toString(16);
    out += part.length === 1 ? `0${part}` : part;
  }
  return out;
}

/** 每台设备一份随机盐，防止 4 位 PIN 的哈希被彩虹表直接反查。 */
export function randomSalt(): string {
  try {
    const rand = cryptoFramework.createRandom();
    return toHex(rand.generateRandomSync(16).data);
  } catch (err) {
    const e = err as BusinessError;
    hilog.error(DOMAIN, TAG, '生成随机盐失败 code=%{public}d', e.code);
    return 'fallback-salt';
  }
}

/** SHA256(salt + ':' + text)，返回小写十六进制。 */
export function sha256Hex(text: string): string {
  try {
    const md = cryptoFramework.createMd('SHA256');
    const encoder = new util.TextEncoder();
    md.updateSync({ data: encoder.encodeInto(text) });
    return toHex(md.digestSync().data);
  } catch (err) {
    const e = err as BusinessError;
    hilog.error(DOMAIN, TAG, '摘要计算失败 code=%{public}d', e.code);
    return '';
  }
}
