import QtQuick

/*
 * 一枚国际象棋棋子（Canvas 手绘）。
 *
 * 不用 Unicode 的 ♔♕♖ 是因为字形要靠字体：deepin / 精简发行版上默认的
 * 无衬线字体不一定带棋类符号，缺字就是一个方框 —— 同一份代码在有的机器上
 * 正常、有的机器上一排豆腐块，这种问题最难排查。手绘虽然多写几十行，
 * 但在任何机器上都长一个样，和孩子在纸上画的棋子也更像。
 *
 * 坐标系固定 100×100，由 size 缩放；type 用 FEN 的兵种字母。
 */
Item {
    id: root

    property string type: "P"          // P 兵 / N 马 / B 象 / R 车 / Q 后 / K 王
    property bool white: true

    readonly property color bodyColor: white ? "#FFFDF8" : "#4A3A31"
    readonly property color lineColor: white ? "#A38A72" : "#F0DCC6"

    implicitWidth: 56
    implicitHeight: 56

    onTypeChanged: canvas.requestPaint()
    onWhiteChanged: canvas.requestPaint()

    Canvas {
        id: canvas

        anchors.fill: parent
        renderStrategy: Canvas.Cooperative
        onPaint: root.paintOn(this)
        onWidthChanged: requestPaint()
        onHeightChanged: requestPaint()
    }

    Component.onCompleted: canvas.requestPaint()

    function paintOn(cv) {
        const ctx = cv.getContext("2d");
        if (!ctx) {
            return;
        }
        ctx.reset();
        ctx.save();
        ctx.scale(cv.width / 100, cv.height / 100);
        ctx.lineJoin = "round";
        ctx.lineCap = "round";
        ctx.fillStyle = root.bodyColor;
        ctx.strokeStyle = root.lineColor;
        ctx.lineWidth = 3.5;

        // 落地阴影：没有它，白棋贴在浅色格子上几乎看不见边界。
        // 用「压扁的圆」而不是 ellipse()：Qt Canvas 的 ellipse 实现随版本有出入。
        ctx.save();
        ctx.translate(50, 86);
        ctx.scale(1, 0.23);
        ctx.beginPath();
        ctx.arc(0, 0, 26, 0, Math.PI * 2);
        ctx.fillStyle = "rgba(74, 59, 50, 0.14)";
        ctx.fill();
        ctx.restore();
        ctx.fillStyle = root.bodyColor;

        switch (root.type) {
        case "P": paintPawn(ctx); break;
        case "N": paintKnight(ctx); break;
        case "B": paintBishop(ctx); break;
        case "R": paintRook(ctx); break;
        case "Q": paintQueen(ctx); break;
        case "K": paintKing(ctx); break;
        default: paintPawn(ctx); break;
        }

        ctx.restore();
    }

    function shape(ctx) {
        ctx.fill();
        ctx.stroke();
    }

    function roundRect(ctx, x, y, w, h, r) {
        ctx.beginPath();
        ctx.moveTo(x + r, y);
        ctx.lineTo(x + w - r, y);
        ctx.quadraticCurveTo(x + w, y, x + w, y + r);
        ctx.lineTo(x + w, y + h - r);
        ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
        ctx.lineTo(x + r, y + h);
        ctx.quadraticCurveTo(x, y + h, x, y + h - r);
        ctx.lineTo(x, y + r);
        ctx.quadraticCurveTo(x, y, x + r, y);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
    }

    function paintPawn(ctx) {
        ctx.beginPath();
        ctx.arc(50, 30, 15, 0, Math.PI * 2);
        shape(ctx);

        ctx.beginPath();
        ctx.moveTo(41, 42);
        ctx.lineTo(59, 42);
        ctx.quadraticCurveTo(64, 60, 68, 74);
        ctx.lineTo(32, 74);
        ctx.quadraticCurveTo(36, 60, 41, 42);
        ctx.closePath();
        shape(ctx);

        roundRect(ctx, 26, 74, 48, 11, 5);
    }

    function paintRook(ctx) {
        // 城垛：三个缺口一刀切出来，比逐个画方块省事
        ctx.beginPath();
        ctx.moveTo(28, 22);
        ctx.lineTo(38, 22);
        ctx.lineTo(38, 32);
        ctx.lineTo(45, 32);
        ctx.lineTo(45, 22);
        ctx.lineTo(55, 22);
        ctx.lineTo(55, 32);
        ctx.lineTo(62, 32);
        ctx.lineTo(62, 22);
        ctx.lineTo(72, 22);
        ctx.lineTo(72, 46);
        ctx.lineTo(28, 46);
        ctx.closePath();
        shape(ctx);

        ctx.beginPath();
        ctx.moveTo(35, 46);
        ctx.lineTo(65, 46);
        ctx.lineTo(68, 74);
        ctx.lineTo(32, 74);
        ctx.closePath();
        shape(ctx);

        roundRect(ctx, 27, 74, 46, 11, 5);
    }

    function paintKnight(ctx) {
        // 马头：侧面剪影 + 一只尖耳朵
        ctx.beginPath();
        ctx.moveTo(30, 76);
        ctx.quadraticCurveTo(31, 62, 43, 56);
        ctx.lineTo(38, 43);
        ctx.lineTo(29, 45);
        ctx.lineTo(36, 28);
        ctx.lineTo(43, 37);
        ctx.quadraticCurveTo(60, 30, 67, 45);
        ctx.quadraticCurveTo(74, 60, 71, 76);
        ctx.closePath();
        shape(ctx);

        roundRect(ctx, 30, 74, 42, 11, 5);
    }

    function paintBishop(ctx) {
        ctx.beginPath();
        ctx.arc(50, 22, 8, 0, Math.PI * 2);
        shape(ctx);

        ctx.beginPath();
        ctx.moveTo(50, 30);
        ctx.quadraticCurveTo(66, 46, 64, 64);
        ctx.lineTo(36, 64);
        ctx.quadraticCurveTo(34, 46, 50, 30);
        ctx.closePath();
        shape(ctx);

        ctx.beginPath();
        ctx.moveTo(38, 64);
        ctx.lineTo(62, 64);
        ctx.lineTo(65, 74);
        ctx.lineTo(35, 74);
        ctx.closePath();
        shape(ctx);

        roundRect(ctx, 28, 74, 44, 11, 5);
    }

    function paintQueen(ctx) {
        // 皇冠：三个尖 + 顶上的圆球
        ctx.beginPath();
        ctx.moveTo(28, 70);
        ctx.lineTo(32, 40);
        ctx.lineTo(41, 55);
        ctx.lineTo(50, 33);
        ctx.lineTo(59, 55);
        ctx.lineTo(68, 40);
        ctx.lineTo(72, 70);
        ctx.closePath();
        shape(ctx);

        ctx.beginPath();
        ctx.arc(32, 38, 6, 0, Math.PI * 2);
        shape(ctx);
        ctx.beginPath();
        ctx.arc(50, 30, 6, 0, Math.PI * 2);
        shape(ctx);
        ctx.beginPath();
        ctx.arc(68, 38, 6, 0, Math.PI * 2);
        shape(ctx);

        roundRect(ctx, 27, 70, 46, 11, 5);
    }

    function paintKing(ctx) {
        // 十字
        ctx.beginPath();
        ctx.moveTo(45, 10);
        ctx.lineTo(55, 10);
        ctx.lineTo(55, 20);
        ctx.lineTo(64, 20);
        ctx.lineTo(64, 30);
        ctx.lineTo(55, 30);
        ctx.lineTo(55, 40);
        ctx.lineTo(45, 40);
        ctx.lineTo(45, 30);
        ctx.lineTo(36, 30);
        ctx.lineTo(36, 20);
        ctx.lineTo(45, 20);
        ctx.closePath();
        shape(ctx);

        ctx.beginPath();
        ctx.moveTo(38, 40);
        ctx.lineTo(62, 40);
        ctx.quadraticCurveTo(68, 58, 69, 72);
        ctx.lineTo(31, 72);
        ctx.quadraticCurveTo(32, 58, 38, 40);
        ctx.closePath();
        shape(ctx);

        roundRect(ctx, 26, 72, 48, 11, 5);
    }
}
