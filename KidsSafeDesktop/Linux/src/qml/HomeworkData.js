.pragma library

/*
 * 小小程序员 · 作业题库（离线）。
 *
 * 与 assets/codelab/lessons.json 的分工：
 *   lessons.json —— 关卡，孩子搭积木、由 GCC 编译运行来判题；
 *   本文件       —— 作业，纯问答/排序，不需要编译器，打开就能做。
 * 作业覆盖的是同一批概念（顺序 / 重复 / 变量 / 判断 / 队列与栈），
 * 所以孩子在哪一章卡住，家长可以直接抽对应那一套作业来复查。
 *
 * 题型（判题全在 judge() 里，QML 只管渲染）：
 *   choice —— 单选，answer 是 options 里的下标（从 0 开始）
 *   fill   —— 填空，answer 是标准答案，alt 是同样算对的其它写法
 *   order  —— 排序，items 是**正确顺序**，界面上会先打乱再让孩子排
 *
 * 改题目只要动这个文件，不用重新编译。
 */

var homeworks = [
    {
        id: "hw-order",
        title: "第 1 课 · 顺序与重复",
        note: "5 道题：程序怎么一句一句走，重复能省多少积木。",
        questions: [
            {
                id: "hw-order-1",
                type: "choice",
                prompt: "三块「打印」从上到下写着 A、B、C，运行后屏幕第一行是什么？",
                options: ["A", "B", "C"],
                answer: 0,
                why: "程序从上往下一句一句执行，最上面那块最先发生。"
            },
            {
                id: "hw-order-2",
                type: "fill",
                prompt: "「重复 5 次」里面装了一行「哈」，屏幕上一共出现 ____ 行。",
                answer: "5",
                alt: ["５", "五行", "5行"],
                why: "重复几次就把口袋里的积木做几遍。"
            },
            {
                id: "hw-order-3",
                type: "choice",
                prompt: "想让屏幕出现 3 行「你好」，哪种搭法最省积木？",
                options: [
                    "一块「重复 3 次」里放一块「打印」",
                    "竖着放三块「打印」",
                    "一块「重复 3 次」里放三块「打印」"
                ],
                answer: 0,
                why: "重复的作用就是「同样的活干很多遍」，用一块积木顶三块。"
            },
            {
                id: "hw-order-4",
                type: "order",
                prompt: "把这首诗的四句排成正确的顺序：",
                items: ["床前明月光", "疑是地上霜", "举头望明月", "低头思故乡"],
                why: "顺序变了，诗就读不通了 —— 程序里的顺序也一样要紧。"
            },
            {
                id: "hw-order-5",
                type: "choice",
                code: "重复 4 次 {\n    打印「★」\n}",
                prompt: "这段程序运行后，屏幕上有几行星星？",
                options: ["2 行", "4 行", "8 行"],
                answer: 1,
                why: "重复 4 次就是打印 4 次，一行一个 ★。"
            }
        ]
    },
    {
        id: "hw-var",
        title: "第 2 课 · 变量与判断",
        note: "5 道题：盒子里的数怎么变，条件成不成立。",
        questions: [
            {
                id: "hw-var-1",
                type: "choice",
                prompt: "「变量」最像下面哪个东西？",
                options: ["一个贴了名字的盒子", "一台电视机", "一张白纸"],
                answer: 0,
                why: "盒子能放东西、能打开看，还能换成别的数。"
            },
            {
                id: "hw-var-2",
                type: "fill",
                prompt: "变量1 里放 3，再做一次「变量加 2」，盒子里现在是 ____。",
                answer: "5",
                alt: ["５"],
                why: "3 + 2 = 5，加法把盒子里的数换成了新的。"
            },
            {
                id: "hw-var-3",
                type: "choice",
                prompt: "变量1 里是 6，条件「变量1 大于 5」成立吗？",
                options: ["成立", "不成立"],
                answer: 0,
                why: "6 比 5 大，所以条件成立，「如果」口袋里的积木会执行。"
            },
            {
                id: "hw-var-4",
                type: "choice",
                code: "变量1 = 4\n如果 变量1 > 5 那么\n    打印「大」",
                prompt: "这段程序运行后，屏幕上会出现什么？",
                options: ["打印「大」", "什么都不出现", "程序报错"],
                answer: 1,
                why: "4 不比 5 大，条件不成立，整段口袋被跳过 —— 不报错，只是什么都没做。"
            },
            {
                id: "hw-var-5",
                type: "order",
                prompt: "「如果…那么」积木是按什么顺序做的？",
                items: ["先看条件成不成立", "成立就做口袋里的积木", "不成立就跳过去"],
                why: "先判断、再决定做不做，这就是「如果」的全部。"
            }
        ]
    },
    {
        id: "hw-stack",
        title: "第 3 课 · 队列与栈",
        note: "4 道题：排队和叠盘子，两种相反的规矩。",
        questions: [
            {
                id: "hw-stack-1",
                type: "choice",
                prompt: "小朋友排队买票，先来的先买到。这叫什么？",
                options: ["队列（先进先出）", "栈（后进先出）"],
                answer: 0,
                why: "排队就是队列：谁先来谁先走。"
            },
            {
                id: "hw-stack-2",
                type: "choice",
                prompt: "一摞盘子只能从最上面拿，这叫什么？",
                options: ["栈（后进先出）", "队列（先进先出）"],
                answer: 0,
                why: "最后放上去的压在最上面，所以最先被拿走。"
            },
            {
                id: "hw-stack-3",
                type: "order",
                prompt: "盘子 1、2、3 依次放上去，只能从最上面拿。把拿走的顺序排好：",
                items: ["盘子3", "盘子2", "盘子1"],
                why: "放进去是 1、2、3，拿出来就是 3、2、1。"
            },
            {
                id: "hw-stack-4",
                type: "fill",
                prompt: "栈的规矩是一句话：____ 先出。",
                answer: "后进",
                alt: ["后放先拿", "后来的先走"],
                why: "后进先出（LIFO）——最后进去的最先出来。"
            }
        ]
    }
];

/* 找出某套作业，找不到返回 null。 */
function findSet(id) {
    for (var i = 0; i < homeworks.length; ++i) {
        if (homeworks[i].id === id) {
            return homeworks[i];
        }
    }
    return null;
}

/*
 * 判题：返回 true / false。QML 侧只拿到「孩子填的东西」，不参与判断，
 * 所以加题型只要在这里加一个分支。
 *
 * value 的形态：choice 是选项下标（数字）；fill 是字符串；order 是字符串数组。
 */
function judge(question, value) {
    if (!question) {
        return false;
    }

    if (question.type === "choice") {
        return Number(value) === Number(question.answer);
    }

    if (question.type === "fill") {
        var got = normalize(value);
        if (got.length === 0) {
            return false;
        }
        if (got === normalize(question.answer)) {
            return true;
        }
        var alt = question.alt || [];
        for (var i = 0; i < alt.length; ++i) {
            if (got === normalize(alt[i])) {
                return true;
            }
        }
        return false;
    }

    if (question.type === "order") {
        var items = question.items || [];
        if (!value || value.length !== items.length) {
            return false;
        }
        for (var j = 0; j < items.length; ++j) {
            if (String(value[j]) !== String(items[j])) {
                return false;
            }
        }
        return true;
    }

    return false;
}

/*
 * 比较前先抹平差异：全角/半角空格、标点、大小写都不该决定对错 ——
 * 孩子在平板上打个中文逗号就判错，只会让人不想再做题。
 */
function normalize(text) {
    var s = (text === undefined || text === null) ? "" : String(text);
    s = s.replace(/\s+/g, "");
    s = s.replace(/[，。、；：！？,.!?;:'"「」“”]/g, "");
    return s.toLowerCase();
}

/* 把正确顺序转一圈打乱：既不提前泄露答案，又保证每次打开都一样。 */
function scrambled(items, seed) {
    var out = (items || []).slice();
    var n = out.length;
    if (n < 2) {
        return out;
    }
    // 必须整除取模到 n-1 再 +1：取 (seed % n) + 1 时，seed 恰好等于 n-1
    // 就会转满一整圈，出来的顺序和正确答案一模一样 —— 那题等于白送。
    var times = (Math.abs(seed) % (n - 1)) + 1;
    for (var i = 0; i < times; ++i) {
        out.push(out.shift());
    }
    return out;
}
