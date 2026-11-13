using System.Collections.Generic;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 内置小游戏的静态数据源（离线、无需任何网络或图片资源）。
/// 用 emoji 当「图片」，可在 Windows 与 Android 上直接渲染。
/// </summary>
public static class GamesData
{
    /// <summary>看图识字的词卡池（emoji + 正确汉字）。</summary>
    public static readonly List<PictureWordCard> WordCards = new()
    {
        // 动物
        new() { Emoji = "🐱", Word = "猫" },
        new() { Emoji = "🐶", Word = "狗" },
        new() { Emoji = "🐰", Word = "兔子" },
        new() { Emoji = "🐯", Word = "老虎" },
        new() { Emoji = "🐘", Word = "大象" },
        new() { Emoji = "🐼", Word = "熊猫" },
        new() { Emoji = "🐷", Word = "猪" },
        new() { Emoji = "🐮", Word = "牛" },
        new() { Emoji = "🐔", Word = "鸡" },
        new() { Emoji = "🐭", Word = "老鼠" },
        new() { Emoji = "🐴", Word = "马" },
        new() { Emoji = "🐸", Word = "青蛙" },

        // 水果食物
        new() { Emoji = "🍎", Word = "苹果" },
        new() { Emoji = "🍌", Word = "香蕉" },
        new() { Emoji = "🍉", Word = "西瓜" },
        new() { Emoji = "🍓", Word = "草莓" },
        new() { Emoji = "🍇", Word = "葡萄" },
        new() { Emoji = "🍊", Word = "橘子" },
        new() { Emoji = "🍞", Word = "面包" },

        // 交通
        new() { Emoji = "🚗", Word = "汽车" },
        new() { Emoji = "🚀", Word = "火箭" },
        new() { Emoji = "🚂", Word = "火车" },
        new() { Emoji = "✈️", Word = "飞机" },
        new() { Emoji = "⛵", Word = "船" },

        // 自然
        new() { Emoji = "⭐", Word = "星星" },
        new() { Emoji = "🌈", Word = "彩虹" },

        // 物品
        new() { Emoji = "🏠", Word = "房子" },
        new() { Emoji = "⌚", Word = "手表" },
        new() { Emoji = "🔔", Word = "铃铛" },
        new() { Emoji = "☂️", Word = "伞" },

        // 真实 AI 生成图片关卡（透明去背 PNG，位于 Resources/Images）
        // —— 原字卡挂图 30 词 ——
        new() { Image = "pw_sun.png",       Word = "太阳" },
        new() { Image = "pw_moon.png",      Word = "月亮" },
        new() { Image = "pw_water.png",     Word = "水" },
        new() { Image = "pw_fire.png",      Word = "火" },
        new() { Image = "pw_mountain.png",  Word = "山" },
        new() { Image = "pw_stone.png",     Word = "石" },
        new() { Image = "pw_field.png",     Word = "田" },
        new() { Image = "pw_soil.png",      Word = "土" },
        new() { Image = "pw_person.png",    Word = "人" },
        new() { Image = "pw_mouth.png",     Word = "口" },
        new() { Image = "pw_hand.png",      Word = "手" },
        new() { Image = "pw_foot.png",      Word = "足" },
        new() { Image = "pw_ear.png",       Word = "耳" },
        new() { Image = "pw_eye.png",       Word = "目" },
        new() { Image = "pw_nose.png",      Word = "鼻" },
        new() { Image = "pw_table.png",     Word = "桌" },
        new() { Image = "pw_chair.png",     Word = "椅" },
        new() { Image = "pw_door.png",      Word = "门" },
        new() { Image = "pw_window.png",    Word = "窗" },
        new() { Image = "pw_stool.png",     Word = "凳" },
        new() { Image = "pw_chopsticks.png",Word = "筷" },
        new() { Image = "pw_cup.png",       Word = "杯" },
        new() { Image = "pw_spoon.png",     Word = "勺" },
        new() { Image = "pw_head.png",      Word = "头" },
        new() { Image = "pw_car.png",       Word = "车" },
        new() { Image = "pw_shoe.png",      Word = "鞋" },
        new() { Image = "pw_hat.png",       Word = "帽" },
        new() { Image = "pw_clothes.png",   Word = "衣" },
        new() { Image = "pw_rice.png",      Word = "米" },
        new() { Image = "pw_bowl.png",      Word = "碗" },
        // —— 扩展词 ——
        new() { Image = "pw_cloud.png",     Word = "云" },
        new() { Image = "pw_star.png",      Word = "星" },
        new() { Image = "pw_flower.png",    Word = "花" },
        new() { Image = "pw_tree.png",      Word = "树" },
        new() { Image = "pw_fish.png",      Word = "鱼" },
        new() { Image = "pw_book.png",      Word = "书" },
        new() { Image = "pw_ball.png",      Word = "球" },
        new() { Image = "pw_bird.png",      Word = "鸟" }
    };

    /// <summary>「拼音识字」游戏用到的词卡池（汉字 + 正确拼音 + emoji/真实抠图）。</summary>
    public static readonly List<PinyinCard> PinyinCards = new()
    {
        // 动物（emoji）
        new() { Emoji = "🐱", Word = "猫",     Pinyin = "māo" },
        new() { Emoji = "🐶", Word = "狗",     Pinyin = "gǒu" },
        new() { Emoji = "🐰", Word = "兔子",   Pinyin = "tù zi" },
        new() { Emoji = "🐯", Word = "老虎",   Pinyin = "lǎo hǔ" },
        new() { Emoji = "🐘", Word = "大象",   Pinyin = "dà xiàng" },
        new() { Emoji = "🐼", Word = "熊猫",   Pinyin = "xióng māo" },
        new() { Emoji = "🐷", Word = "猪",     Pinyin = "zhū" },
        new() { Emoji = "🐮", Word = "牛",     Pinyin = "niú" },
        new() { Emoji = "🐔", Word = "鸡",     Pinyin = "jī" },
        new() { Emoji = "🐭", Word = "老鼠",   Pinyin = "lǎo shǔ" },
        new() { Emoji = "🐴", Word = "马",     Pinyin = "mǎ" },
        new() { Emoji = "🐸", Word = "青蛙",   Pinyin = "qīng wā" },

        // 水果食物（emoji）
        new() { Emoji = "🍎", Word = "苹果",   Pinyin = "píng guǒ" },
        new() { Emoji = "🍌", Word = "香蕉",   Pinyin = "xiāng jiāo" },
        new() { Emoji = "🍉", Word = "西瓜",   Pinyin = "xī guā" },
        new() { Emoji = "🍓", Word = "草莓",   Pinyin = "cǎo méi" },
        new() { Emoji = "🍇", Word = "葡萄",   Pinyin = "pú táo" },
        new() { Emoji = "🍊", Word = "橘子",   Pinyin = "jú zi" },
        new() { Emoji = "🍞", Word = "面包",   Pinyin = "miàn bāo" },

        // 交通（emoji）
        new() { Emoji = "🚗", Word = "汽车",   Pinyin = "qì chē" },
        new() { Emoji = "🚀", Word = "火箭",   Pinyin = "huǒ jiàn" },
        new() { Emoji = "🚂", Word = "火车",   Pinyin = "huǒ chē" },
        new() { Emoji = "✈️", Word = "飞机",   Pinyin = "fēi jī" },
        new() { Emoji = "⛵", Word = "船",     Pinyin = "chuán" },

        // 自然 / 物品（emoji）
        new() { Emoji = "⭐", Word = "星星",   Pinyin = "xīng xing" },
        new() { Emoji = "🌈", Word = "彩虹",   Pinyin = "cǎi hóng" },
        new() { Emoji = "🏠", Word = "房子",   Pinyin = "fáng zi" },
        new() { Emoji = "⌚", Word = "手表",   Pinyin = "shǒu biǎo" },
        new() { Emoji = "🔔", Word = "铃铛",   Pinyin = "líng dāng" },
        new() { Emoji = "☂️", Word = "伞",     Pinyin = "sǎn" },

        // 真实 AI 生成图片关卡（透明去背 PNG，位于 Resources/Images）
        new() { Image = "pw_sun.png",       Word = "太阳", Pinyin = "tài yáng" },
        new() { Image = "pw_moon.png",      Word = "月亮", Pinyin = "yuè liang" },
        new() { Image = "pw_water.png",     Word = "水",   Pinyin = "shuǐ" },
        new() { Image = "pw_fire.png",      Word = "火",   Pinyin = "huǒ" },
        new() { Image = "pw_mountain.png",  Word = "山",   Pinyin = "shān" },
        new() { Image = "pw_stone.png",     Word = "石",   Pinyin = "shí" },
        new() { Image = "pw_field.png",     Word = "田",   Pinyin = "tián" },
        new() { Image = "pw_soil.png",      Word = "土",   Pinyin = "tǔ" },
        new() { Image = "pw_person.png",    Word = "人",   Pinyin = "rén" },
        new() { Image = "pw_mouth.png",     Word = "口",   Pinyin = "kǒu" },
        new() { Image = "pw_hand.png",      Word = "手",   Pinyin = "shǒu" },
        new() { Image = "pw_foot.png",      Word = "足",   Pinyin = "zú" },
        new() { Image = "pw_ear.png",       Word = "耳",   Pinyin = "ěr" },
        new() { Image = "pw_eye.png",       Word = "目",   Pinyin = "mù" },
        new() { Image = "pw_nose.png",      Word = "鼻",   Pinyin = "bí" },
        new() { Image = "pw_table.png",     Word = "桌",   Pinyin = "zhuō" },
        new() { Image = "pw_chair.png",     Word = "椅",   Pinyin = "yǐ" },
        new() { Image = "pw_door.png",      Word = "门",   Pinyin = "mén" },
        new() { Image = "pw_window.png",    Word = "窗",   Pinyin = "chuāng" },
        new() { Image = "pw_stool.png",     Word = "凳",   Pinyin = "dèng" },
        new() { Image = "pw_chopsticks.png",Word = "筷",   Pinyin = "kuài" },
        new() { Image = "pw_cup.png",       Word = "杯",   Pinyin = "bēi" },
        new() { Image = "pw_spoon.png",     Word = "勺",   Pinyin = "sháo" },
        new() { Image = "pw_head.png",      Word = "头",   Pinyin = "tóu" },
        new() { Image = "pw_car.png",       Word = "车",   Pinyin = "chē" },
        new() { Image = "pw_shoe.png",      Word = "鞋",   Pinyin = "xié" },
        new() { Image = "pw_hat.png",       Word = "帽",   Pinyin = "mào" },
        new() { Image = "pw_clothes.png",   Word = "衣",   Pinyin = "yī" },
        new() { Image = "pw_rice.png",      Word = "米",   Pinyin = "mǐ" },
        new() { Image = "pw_bowl.png",      Word = "碗",   Pinyin = "wǎn" },
        new() { Image = "pw_cloud.png",     Word = "云",   Pinyin = "yún" },
        new() { Image = "pw_star.png",      Word = "星",   Pinyin = "xīng" },
        new() { Image = "pw_flower.png",    Word = "花",   Pinyin = "huā" },
        new() { Image = "pw_tree.png",      Word = "树",   Pinyin = "shù" },
        new() { Image = "pw_fish.png",      Word = "鱼",   Pinyin = "yú" },
        new() { Image = "pw_book.png",      Word = "书",   Pinyin = "shū" },
        new() { Image = "pw_ball.png",      Word = "球",   Pinyin = "qiú" },
        new() { Image = "pw_bird.png",      Word = "鸟",   Pinyin = "niǎo" }
    };

    /// <summary>「数一数」游戏用到的图标池。</summary>
    public static readonly List<string> CountingEmojis = new()
    {
        "🍎", "⭐", "🐱", "🌸", "🚗", "⚽", "🍓", "🌞", "🐟", "🍩"
    };

    /// <summary>「拼图配对」游戏用到的图标池（成对出现）。</summary>
    public static readonly List<string> MatchingEmojis = new()
    {
        "🐱", "🐶", "🍎", "🌞", "⭐", "🌸",
        "🚗", "🐼", "🍌", "🌳", "🐠", "🍓"
    };

    /// <summary>「找颜色」游戏用到的颜色池（名称 + 十六进制）。</summary>
    public static readonly List<ColorItem> Colors = new()
    {
        new ColorItem { Name = "红色", Hex = "#E53935" },
        new ColorItem { Name = "橙色", Hex = "#FB8C00" },
        new ColorItem { Name = "黄色", Hex = "#FDD835" },
        new ColorItem { Name = "绿色", Hex = "#43A047" },
        new ColorItem { Name = "蓝色", Hex = "#1E88E5" },
        new ColorItem { Name = "紫色", Hex = "#8E24AA" },
        new ColorItem { Name = "粉色", Hex = "#EC407A" },
        new ColorItem { Name = "棕色", Hex = "#6D4C41" },
        new ColorItem { Name = "黑色", Hex = "#212121" },
        new ColorItem { Name = "白色", Hex = "#FAFAFA" },
        new ColorItem { Name = "灰色", Hex = "#9E9E9E" }
    };
}
