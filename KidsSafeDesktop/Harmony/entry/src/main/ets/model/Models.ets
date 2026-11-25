/**
 * 与 C# 端 Models/AppItem.cs、Models/StoryModels.cs 对齐的数据结构。
 *
 * 差异点（鸿蒙平台决定，不是偷懒）：
 * 没有 PackageName（Android 包名）这个概念，换成 bundleName + abilityName 组合，
 * 因为鸿蒙拉起应用必须给到「包名 + Ability 名」，只给包名会报 want 不完整。
 * launchUrl 保留给「网页白名单」，对应 Windows 端直接开 http(s) 的能力。
 */
export class AppItem {
  id: string = '';
  name: string = '';
  bundleName: string = '';
  /** 入口类名。空串表示未知，启动时按候选表逐个尝试（见 AppLauncher.buildWants）。 */
  abilityName: string = '';
  launchUrl: string = '';
  accent: string = '#4C8BF5';
  emoji: string = '⭐';
}

export function makeApp(name: string, bundleName: string, emoji: string, accent: string,
  abilityName: string = ''): AppItem {
  const app: AppItem = new AppItem();
  app.bundleName = bundleName;
  app.abilityName = abilityName;
  app.id = bundleName.length > 0 ? bundleName : `tile-${Date.now()}`;
  app.name = name;
  app.emoji = emoji;
  app.accent = accent;
  return app;
}

export function makeLinkApp(name: string, url: string, emoji: string, accent: string): AppItem {
  const app: AppItem = new AppItem();
  app.launchUrl = url;
  app.id = url;
  app.name = name;
  app.emoji = emoji;
  app.accent = accent;
  return app;
}

export interface StoryCategory {
  id: number;
  name: string;
  emoji: string;
}

export interface StorySummary {
  id: number;
  title: string;
  type: string;
  length: number;
  readTime: string;
  shortDesc: string;
}

export interface StoryDetail {
  id: number;
  title: string;
  type: string;
  length: number;
  readTime: string;
  content: string;
}

/** 服务端返回体，字段名与 KidsSafeServer 保持一致，不要改名。 */
export interface CategoriesResp {
  categories: StoryCategory[];
}

export interface StoryListResp {
  items: StorySummary[];
  page: number;
  hasMore: boolean;
}
