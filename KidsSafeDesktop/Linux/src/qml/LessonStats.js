.pragma library

/*
 * 关卡统计：小小程序员页顶部的「3 / 26」和家长后台的「学习情况」卡共用这一份。
 *
 * 为什么单独抽出来：这两处都得回答同一个问题「一共几关、过了几关」，而口径容易
 * 出错 —— 教学关（type=teach）只有「看演示」，永远不会有过关记录，把它算进分母，
 * 进度就永远差着几格（32 关里有 6 个教学关）。两处各写一遍，迟早一处改了一处没改。
 *
 * 参数都从外面传进来（lessons 来自 CodeLab.lessons、passed 来自 CodeLab.passedIds），
 * .pragma library 里拿不到 QML 上下文，也正好强迫这里保持纯函数、好测。
 */

// 返回 { chapters: [{ name, done, total }], done, total }，只数要判题的关卡。
function stats(lessons, passed) {
    var list = lessons || []
    var doneIds = passed || []
    var chapters = []
    var done = 0
    var total = 0

    for (var i = 0; i < list.length; ++i) {
        var item = list[i]
        if (String(item.type) === "teach") {
            continue
        }
        var name = String(item.chapter)
        var row = null
        for (var j = 0; j < chapters.length; ++j) {
            if (chapters[j].name === name) {
                row = chapters[j]
                break
            }
        }
        if (row === null) {
            row = { name: name, done: 0, total: 0 }
            chapters.push(row)
        }
        row.total += 1
        total += 1
        // 只认关卡表里真实存在的 id：旧版本删掉的关卡留下的过关记录不该算分
        if (doneIds.indexOf(String(item.id)) >= 0) {
            row.done += 1
            done += 1
        }
    }
    return { chapters: chapters, done: done, total: total }
}

// "第一章 · 顺序" → "顺序"。章节名的格式由 lessons.json 决定，取最后一段就够。
function shortChapter(name) {
    var parts = String(name).split("·")
    return parts.length > 1 ? parts[parts.length - 1].trim() : String(name)
}
