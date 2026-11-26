import QtQuick

/*
 * 自绘矢量图标。
 *
 * 为什么不用 emoji：deepin 25（以及多数精简发行版）默认只装了黑白符号字体，
 * 📖 🛡️ ⏳ 这类 emoji 会被渲染成单色线稿——同一份文案在 Windows 上是彩色贴纸，
 * 在 Linux 上变成一串黑图标，跟 Windows 端的观感完全对不上。装 Noto Color Emoji
 * 能解决，但不能要求用户为了儿童桌面去装字体。
 *
 * 所以关键位置的图标全部改成 Canvas 手绘：不依赖任何字体、任何图标主题，
 * 在任何机器上都长一个样。坐标系固定 100×100，由 size 缩放，
 * 因此 24px 的小图标和 96px 的大图标是同一套路径。
 *
 * 用法：
 *   GlyphIcon { name: "book"; color: "white"; implicitWidth: 38; implicitHeight: 38 }
 *
 * 单色图标用 color；彩色头像（bear/cat/rabbit/panda/fox/dog）内置配色，
 * 忽略 color。
 */
Item {
    id: root

    property string name: "star"
    property color color: "white"

    implicitWidth: 32
    implicitHeight: 32

    onNameChanged: canvas.requestPaint()
    onColorChanged: canvas.requestPaint()

    Canvas {
        id: canvas

        anchors.fill: parent
        // Cooperative：和场景图共用一个渲染线程，静态图标不会在首帧闪白。
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
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.lineWidth = 7;
        ctx.strokeStyle = root.color;
        ctx.fillStyle = root.color;

        switch (root.name) {
        case "book":      paintBook(ctx); break;
        case "shield":    paintShield(ctx); break;
        case "gamepad":   paintGamepad(ctx); break;
        case "music":     paintMusic(ctx); break;
        case "pencil":    paintPencil(ctx); break;
        case "hourglass": paintHourglass(ctx); break;
        case "eye":       paintEye(ctx); break;
        case "palette":   paintPalette(ctx); break;
        case "grid":      paintGrid(ctx); break;
        case "sudoku":    paintSudoku(ctx); break;
        case "chess":     paintChess(ctx); break;
        case "blocks":    paintBlocks(ctx); break;
        case "hammer":    paintHammer(ctx); break;
        case "rocket":    paintRocket(ctx); break;
        case "moon":      paintMoon(ctx); break;
        case "speaker":   paintSpeaker(ctx); break;
        case "plug":      paintPlug(ctx); break;
        case "trophy":    paintTrophy(ctx); break;
        case "sun":       paintSun(ctx); break;
        case "partly":    paintPartly(ctx); break;
        case "cloud":     paintCloud(ctx); break;
        case "rain":      paintRain(ctx); break;
        case "snow":      paintSnow(ctx); break;
        case "thunder":   paintThunder(ctx); break;
        case "fog":       paintFog(ctx); break;
        case "traffic":   paintTraffic(ctx); break;
        case "hand":      paintHand(ctx); break;
        case "flame":     paintFlame(ctx); break;
        case "socket":    paintSocket(ctx); break;
        case "drop":      paintDrop(ctx); break;
        case "apple":     paintApple(ctx); break;
        case "storm":     paintStorm(ctx); break;
        case "laptop":    paintLaptop(ctx); break;
        case "guard":     paintGuard(ctx); break;
        case "phone":     paintPhone(ctx); break;
        // ---- 桌面环境用的界面符号 ----
        case "close":     paintClose(ctx); break;
        case "maximize":  paintMaximize(ctx); break;
        case "restore":   paintRestore(ctx); break;
        case "folder":    paintFolder(ctx); break;
        case "terminal":  paintTerminal(ctx); break;
        case "chip":      paintChip(ctx); break;
        case "back":      paintBack(ctx); break;
        case "forward":   paintForward(ctx); break;
        case "up":        paintUp(ctx); break;
        case "search":    paintSearch(ctx); break;
        case "trash":     paintTrash(ctx); break;
        case "plus":      paintPlus(ctx); break;
        case "refresh":   paintRefresh(ctx); break;
        case "doc":       paintDoc(ctx); break;
        case "picture":   paintPicture(ctx); break;
        case "arrow":     paintArrow(ctx); break;
        case "home":      paintHome(ctx); break;
        case "monitor":   paintMonitor(ctx); break;
        case "download":  paintDownload(ctx); break;
        case "disk":      paintDisk(ctx); break;
        case "bear":      paintBear(ctx); break;
        case "tiger":     paintTiger(ctx); break;
        case "dog":       paintDog(ctx); break;
        case "fox":       paintFox(ctx); break;
        case "pig":       paintPig(ctx); break;
        case "cat":       paintCat(ctx); break;
        case "rabbit":    paintRabbit(ctx); break;
        case "panda":     paintPanda(ctx); break;
        case "star":
        default:          paintStar(ctx); break;
        }

        ctx.restore();
    }

    // ---- 内置内容 ----

    function paintBook(ctx) {
        // 摊开的书：两页各自填充，中间一条书脊
        ctx.globalAlpha = 0.28;
        ctx.beginPath();
        ctx.moveTo(50, 30);
        ctx.quadraticCurveTo(34, 24, 14, 30);
        ctx.lineTo(14, 74);
        ctx.quadraticCurveTo(34, 68, 50, 74);
        ctx.closePath();
        ctx.moveTo(50, 30);
        ctx.quadraticCurveTo(66, 24, 86, 30);
        ctx.lineTo(86, 74);
        ctx.quadraticCurveTo(66, 68, 50, 74);
        ctx.closePath();
        ctx.fill();
        ctx.globalAlpha = 1;

        ctx.beginPath();
        ctx.moveTo(50, 30);
        ctx.quadraticCurveTo(34, 24, 14, 30);
        ctx.lineTo(14, 74);
        ctx.quadraticCurveTo(34, 68, 50, 74);
        ctx.moveTo(50, 30);
        ctx.quadraticCurveTo(66, 24, 86, 30);
        ctx.lineTo(86, 74);
        ctx.quadraticCurveTo(66, 68, 50, 74);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(50, 31);
        ctx.lineTo(50, 74);
        ctx.stroke();

        // 两行「字」
        ctx.globalAlpha = 0.75;
        ctx.lineWidth = 5;
        ctx.beginPath();
        ctx.moveTo(23, 44); ctx.lineTo(41, 44);
        ctx.moveTo(23, 55); ctx.lineTo(38, 55);
        ctx.moveTo(59, 44); ctx.lineTo(77, 44);
        ctx.moveTo(59, 55); ctx.lineTo(74, 55);
        ctx.stroke();
        ctx.globalAlpha = 1;
    }

    function paintShield(ctx) {
        ctx.beginPath();
        ctx.moveTo(50, 12);
        ctx.lineTo(84, 25);
        ctx.lineTo(84, 49);
        ctx.quadraticCurveTo(84, 76, 50, 90);
        ctx.quadraticCurveTo(16, 76, 16, 49);
        ctx.lineTo(16, 25);
        ctx.closePath();
        ctx.globalAlpha = 0.25;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        ctx.lineWidth = 8;
        ctx.beginPath();
        ctx.moveTo(34, 50);
        ctx.lineTo(46, 62);
        ctx.lineTo(68, 38);
        ctx.stroke();
    }

    function paintGamepad(ctx) {
        ctx.beginPath();
        ctx.roundedRect(10, 30, 80, 46, 20, 20);
        ctx.globalAlpha = 0.25;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        // 十字键
        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.moveTo(30, 44); ctx.lineTo(30, 62);
        ctx.moveTo(21, 53); ctx.lineTo(39, 53);
        ctx.stroke();

        // 两个按钮
        ctx.beginPath();
        ctx.arc(66, 46, 5.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.arc(78, 58, 5.5, 0, Math.PI * 2);
        ctx.fill();
    }

    function paintMusic(ctx) {
        ctx.beginPath();
        ctx.arc(32, 72, 12, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.arc(72, 62, 12, 0, Math.PI * 2);
        ctx.fill();

        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.moveTo(44, 72);
        ctx.lineTo(44, 26);
        ctx.moveTo(84, 62);
        ctx.lineTo(84, 18);
        ctx.moveTo(44, 30);
        ctx.lineTo(84, 20);
        ctx.stroke();
    }

    function paintPencil(ctx) {
        ctx.save();
        ctx.translate(50, 50);
        ctx.rotate(-45 * Math.PI / 180);

        // 笔身
        ctx.beginPath();
        ctx.roundedRect(-14, -40, 28, 60, 6, 6);
        ctx.globalAlpha = 0.28;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        // 笔尖
        ctx.beginPath();
        ctx.moveTo(-14, 20);
        ctx.lineTo(0, 40);
        ctx.lineTo(14, 20);
        ctx.closePath();
        ctx.stroke();

        // 笔尾橡皮
        ctx.beginPath();
        ctx.moveTo(-14, -26);
        ctx.lineTo(14, -26);
        ctx.stroke();

        ctx.restore();
    }

    function paintHourglass(ctx) {
        ctx.beginPath();
        ctx.moveTo(26, 14);
        ctx.lineTo(74, 14);
        ctx.moveTo(26, 86);
        ctx.lineTo(74, 86);
        ctx.stroke();

        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.moveTo(30, 14);
        ctx.lineTo(30, 30);
        ctx.quadraticCurveTo(30, 44, 50, 50);
        ctx.quadraticCurveTo(70, 56, 70, 70);
        ctx.lineTo(70, 86);
        ctx.moveTo(70, 14);
        ctx.lineTo(70, 30);
        ctx.quadraticCurveTo(70, 44, 50, 50);
        ctx.quadraticCurveTo(30, 56, 30, 70);
        ctx.lineTo(30, 86);
        ctx.stroke();

        // 下半部分的沙
        ctx.globalAlpha = 0.55;
        ctx.beginPath();
        ctx.moveTo(38, 86);
        ctx.lineTo(62, 86);
        ctx.quadraticCurveTo(60, 68, 50, 62);
        ctx.quadraticCurveTo(40, 68, 38, 86);
        ctx.closePath();
        ctx.fill();
        ctx.globalAlpha = 1;
    }

    function paintEye(ctx) {
        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.moveTo(8, 50);
        ctx.quadraticCurveTo(30, 22, 50, 22);
        ctx.quadraticCurveTo(70, 22, 92, 50);
        ctx.quadraticCurveTo(70, 78, 50, 78);
        ctx.quadraticCurveTo(30, 78, 8, 50);
        ctx.closePath();
        ctx.globalAlpha = 0.22;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        ctx.beginPath();
        ctx.arc(50, 50, 13, 0, Math.PI * 2);
        ctx.fill();
    }

    function paintPalette(ctx) {
        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.arc(50, 50, 34, 0, Math.PI * 2);
        ctx.globalAlpha = 0.22;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        // 拇指孔
        ctx.beginPath();
        ctx.arc(66, 62, 8, 0, Math.PI * 2);
        ctx.stroke();

        // 颜料点
        const dots = [[38, 34], [56, 30], [70, 44], [34, 56]];
        for (const d of dots) {
            ctx.beginPath();
            ctx.arc(d[0], d[1], 5, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    function paintTrophy(ctx) {
        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.moveTo(30, 14);
        ctx.lineTo(70, 14);
        ctx.lineTo(70, 40);
        ctx.quadraticCurveTo(70, 60, 50, 62);
        ctx.quadraticCurveTo(30, 60, 30, 40);
        ctx.closePath();
        ctx.globalAlpha = 0.28;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        // 双耳
        ctx.beginPath();
        ctx.moveTo(30, 22);
        ctx.quadraticCurveTo(16, 22, 18, 36);
        ctx.quadraticCurveTo(20, 48, 32, 48);
        ctx.moveTo(70, 22);
        ctx.quadraticCurveTo(84, 22, 82, 36);
        ctx.quadraticCurveTo(80, 48, 68, 48);
        ctx.stroke();

        // 底座
        ctx.beginPath();
        ctx.moveTo(50, 62);
        ctx.lineTo(50, 76);
        ctx.moveTo(34, 86);
        ctx.lineTo(66, 86);
        ctx.moveTo(38, 76);
        ctx.lineTo(62, 76);
        ctx.stroke();
    }

    function paintGrid(ctx) {
        // 九宫格：配对游戏的「牌」意象
        ctx.lineWidth = 6;
        ctx.globalAlpha = 0.25;
        ctx.beginPath();
        ctx.roundedRect(14, 14, 30, 30, 8, 8);
        ctx.roundedRect(56, 14, 30, 30, 8, 8);
        ctx.roundedRect(14, 56, 30, 30, 8, 8);
        ctx.roundedRect(56, 56, 30, 30, 8, 8);
        ctx.fill();
        ctx.globalAlpha = 1;

        ctx.beginPath();
        ctx.roundedRect(14, 14, 30, 30, 8, 8);
        ctx.roundedRect(56, 14, 30, 30, 8, 8);
        ctx.roundedRect(14, 56, 30, 30, 8, 8);
        ctx.roundedRect(56, 56, 30, 30, 8, 8);
        ctx.stroke();
    }

    function paintSudoku(ctx) {
        // 数独：四宫格外框 + 中间十字 + 左上格填实（代表已经填好的数字）
        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.roundedRect(14, 14, 72, 72, 14, 14);
        ctx.stroke();

        ctx.lineWidth = 5;
        ctx.beginPath();
        ctx.moveTo(50, 18);
        ctx.lineTo(50, 82);
        ctx.moveTo(18, 50);
        ctx.lineTo(82, 50);
        ctx.stroke();

        ctx.beginPath();
        ctx.roundedRect(20, 20, 26, 26, 7, 7);
        ctx.fill();
    }

    function paintChess(ctx) {
        // 骑士：马头剪影，和小游戏棋盘上的棋子同一套轮廓
        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.moveTo(22, 74);
        ctx.quadraticCurveTo(24, 58, 40, 50);
        ctx.lineTo(34, 38);
        ctx.lineTo(24, 40);
        ctx.lineTo(32, 20);
        ctx.lineTo(41, 31);
        ctx.quadraticCurveTo(62, 24, 70, 44);
        ctx.quadraticCurveTo(78, 60, 74, 74);
        ctx.closePath();
        ctx.globalAlpha = 0.26;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        // 底座
        ctx.beginPath();
        ctx.moveTo(18, 74);
        ctx.lineTo(80, 74);
        ctx.lineTo(80, 86);
        ctx.lineTo(18, 86);
        ctx.closePath();
        ctx.globalAlpha = 0.26;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();
    }

    function paintBlocks(ctx) {
        // 华容道：4×5 的盘 + 左上角那块 2×2 的「曹操」 + 下面的出口刻度
        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.roundedRect(16, 10, 68, 72, 12, 12);
        ctx.stroke();

        ctx.beginPath();
        ctx.roundedRect(20, 14, 30, 28, 8, 8);
        ctx.globalAlpha = 0.55;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        ctx.lineWidth = 5;
        ctx.beginPath();
        ctx.moveTo(42, 82);
        ctx.lineTo(42, 92);
        ctx.moveTo(58, 82);
        ctx.lineTo(58, 92);
        ctx.stroke();
    }

    function paintHammer(ctx) {
        ctx.save();
        ctx.translate(50, 50);
        ctx.rotate(35 * Math.PI / 180);

        // 锤头
        ctx.beginPath();
        ctx.roundedRect(-34, -38, 68, 26, 7, 7);
        ctx.globalAlpha = 0.28;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        // 木柄
        ctx.lineWidth = 12;
        ctx.beginPath();
        ctx.moveTo(0, -14);
        ctx.lineTo(0, 42);
        ctx.stroke();

        ctx.restore();
    }

    function paintRocket(ctx) {
        ctx.save();
        ctx.translate(50, 48);
        ctx.rotate(45 * Math.PI / 180);

        // 箭体
        ctx.beginPath();
        ctx.moveTo(0, -40);
        ctx.quadraticCurveTo(20, -18, 20, 12);
        ctx.lineTo(-20, 12);
        ctx.quadraticCurveTo(-20, -18, 0, -40);
        ctx.closePath();
        ctx.globalAlpha = 0.25;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        // 舷窗
        ctx.beginPath();
        ctx.arc(0, -12, 8, 0, Math.PI * 2);
        ctx.stroke();

        // 尾翼
        ctx.beginPath();
        ctx.moveTo(-20, 0);
        ctx.lineTo(-32, 30);
        ctx.lineTo(-20, 24);
        ctx.moveTo(20, 0);
        ctx.lineTo(32, 30);
        ctx.lineTo(20, 24);
        ctx.stroke();

        // 火焰
        ctx.globalAlpha = 0.6;
        ctx.beginPath();
        ctx.moveTo(-9, 14);
        ctx.quadraticCurveTo(0, 40, 9, 14);
        ctx.closePath();
        ctx.fill();
        ctx.globalAlpha = 1;

        ctx.restore();
    }

    function paintMoon(ctx) {
        // 用「大圆 + 反向小圆」画月牙。QML 的 ctx.fill() 不接受 fill-rule 参数
        // （传了会被忽略，两个同向圆按非零环绕规则并集成一个实心圆），
        // 所以第二个圆必须用 anticlockwise = true 反向走。
        ctx.beginPath();
        ctx.arc(50, 52, 34, 0, Math.PI * 2, false);
        ctx.arc(68, 38, 29, 0, Math.PI * 2, true);
        ctx.fill();
    }

    function paintSpeaker(ctx) {
        ctx.beginPath();
        ctx.moveTo(20, 40);
        ctx.lineTo(38, 40);
        ctx.lineTo(58, 24);
        ctx.lineTo(58, 76);
        ctx.lineTo(38, 60);
        ctx.lineTo(20, 60);
        ctx.closePath();
        ctx.globalAlpha = 0.3;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        // 两道声波
        ctx.beginPath();
        ctx.arc(60, 50, 14, -0.9, 0.9);
        ctx.stroke();
        ctx.beginPath();
        ctx.arc(60, 50, 26, -0.9, 0.9);
        ctx.stroke();
    }

    function paintPlug(ctx) {
        ctx.beginPath();
        ctx.roundedRect(30, 16, 40, 26, 8, 8);
        ctx.globalAlpha = 0.25;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.stroke();

        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.moveTo(40, 16);
        ctx.lineTo(40, 6);
        ctx.moveTo(60, 16);
        ctx.lineTo(60, 6);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(50, 42);
        ctx.lineTo(50, 66);
        ctx.quadraticCurveTo(50, 82, 32, 84);
        ctx.stroke();
    }

    function paintStar(ctx) {
        ctx.beginPath();
        for (let i = 0; i < 10; i++) {
            const r = i % 2 === 0 ? 40 : 17;
            const a = -Math.PI / 2 + i * Math.PI / 5;
            const x = 50 + Math.cos(a) * r;
            const y = 52 + Math.sin(a) * r;
            if (i === 0) {
                ctx.moveTo(x, y);
            } else {
                ctx.lineTo(x, y);
            }
        }
        ctx.closePath();
        ctx.globalAlpha = 0.28;
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.lineWidth = 6;
        ctx.stroke();
    }

    // ---- 头像（彩色，不跟随 color）----
    // 与 Settings.avatarEmoji 里的常用 emoji 一一对应，
    // 见 HomePage.qml 的 avatarGlyph() 映射。

    /*
     * 椭圆路径。
     *
     * 不用 ctx.ellipse()：Qt 的 Canvas 在不同发行版上对它的支持不一致
     * （某些 Qt 6.4 打包版本直接抛 TypeError: Property 'ellipse' of object
     *  CanvasContext2D is not a function），整页图标会集体变空白。
     * 这里用「缩放坐标系 + arc」自己拼，任何 Qt 版本都跑得通。
     * 注意 save/restore 只影响画笔状态，路径点是在添加时按当前变换算好的。
     */
    function oval(ctx, cx, cy, rx, ry) {
        ctx.save();
        ctx.translate(cx, cy);
        ctx.scale(rx / ry, 1);
        ctx.arc(0, 0, ry, 0, Math.PI * 2);
        ctx.restore();
    }

    function paintFaceBase(ctx, fur, ear, muzzle) {
        // 耳朵
        ctx.fillStyle = ear;
        ctx.beginPath();
        ctx.arc(28, 30, 15, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.arc(72, 30, 15, 0, Math.PI * 2);
        ctx.fill();

        // 脸
        ctx.fillStyle = fur;
        ctx.beginPath();
        ctx.arc(50, 56, 34, 0, Math.PI * 2);
        ctx.fill();

        // 嘴部浅色区
        if (muzzle) {
            ctx.fillStyle = muzzle;
            ctx.beginPath();
            ctx.arc(50, 68, 18, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    function paintEyes(ctx, color) {
        ctx.fillStyle = color || "#3B2A21";
        ctx.beginPath();
        ctx.arc(39, 52, 4.2, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.arc(61, 52, 4.2, 0, Math.PI * 2);
        ctx.fill();
    }

    function paintBear(ctx) {
        // 对应 emoji 🧸 / 🐻：泰迪熊，圆耳朵 + 大脑袋 + 浅色口鼻。
        // 头要画满 100×100（半径 30），头像圈里 48px 时才不会显小。
        ctx.fillStyle = "#B67C4B";
        ctx.beginPath(); ctx.arc(26, 24, 14, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(74, 24, 14, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = "#E9C6A0";
        ctx.beginPath(); ctx.arc(26, 24, 8, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(74, 24, 8, 0, Math.PI * 2); ctx.fill();

        ctx.fillStyle = "#C98D58";
        ctx.beginPath(); ctx.arc(50, 52, 31, 0, Math.PI * 2); ctx.fill();

        // 腮红
        ctx.fillStyle = "rgba(246, 170, 140, 0.55)";
        ctx.beginPath(); ctx.arc(25, 58, 6.5, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(75, 58, 6.5, 0, Math.PI * 2); ctx.fill();

        // 口鼻
        ctx.fillStyle = "#F3E3CE";
        ctx.beginPath(); oval(ctx, 50, 66, 18, 14); ctx.fill();

        // 眼睛
        ctx.fillStyle = "#3B2A21";
        ctx.beginPath(); ctx.arc(37, 45, 4.6, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(63, 45, 4.6, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = "#FFFFFF";
        ctx.beginPath(); ctx.arc(38.6, 43.4, 1.6, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(64.6, 43.4, 1.6, 0, Math.PI * 2); ctx.fill();

        // 鼻子 + 嘴
        ctx.fillStyle = "#4A3123";
        ctx.beginPath(); oval(ctx, 50, 59, 6, 4.6); ctx.fill();

        ctx.strokeStyle = "#4A3123";
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(50, 63.5);
        ctx.lineTo(50, 67);
        ctx.moveTo(50, 67);
        ctx.quadraticCurveTo(44, 72, 41, 67);
        ctx.moveTo(50, 67);
        ctx.quadraticCurveTo(56, 72, 59, 67);
        ctx.stroke();
    }

    function paintTiger(ctx) {
        // 对应 🐯：橘黄脑袋 + 白口鼻 + 额头三道纹
        ctx.fillStyle = "#E8A33D";
        ctx.beginPath(); ctx.arc(25, 22, 13, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(75, 22, 13, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = "#FFF3D6";
        ctx.beginPath(); ctx.arc(25, 22, 7, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(75, 22, 7, 0, Math.PI * 2); ctx.fill();

        ctx.fillStyle = "#F5A623";
        ctx.beginPath(); ctx.arc(50, 54, 31, 0, Math.PI * 2); ctx.fill();

        // 额头与两颊的条纹
        ctx.fillStyle = "#8D5524";
        ctx.beginPath(); ctx.roundedRect(45, 22, 10, 12, 5, 5); ctx.fill();
        ctx.beginPath(); ctx.roundedRect(30, 30, 7, 10, 3.5, 3.5); ctx.fill();
        ctx.beginPath(); ctx.roundedRect(63, 30, 7, 10, 3.5, 3.5); ctx.fill();
        ctx.beginPath(); ctx.roundedRect(19, 50, 8, 9, 4, 4); ctx.fill();
        ctx.beginPath(); ctx.roundedRect(73, 50, 8, 9, 4, 4); ctx.fill();

        // 白口鼻
        ctx.fillStyle = "#FFFDF5";
        ctx.beginPath(); oval(ctx, 42, 68, 13, 11); ctx.fill();
        ctx.beginPath(); oval(ctx, 58, 68, 13, 11); ctx.fill();

        ctx.fillStyle = "#3B2A21";
        ctx.beginPath(); ctx.arc(37, 47, 4.6, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(63, 47, 4.6, 0, Math.PI * 2); ctx.fill();

        // 鼻头
        ctx.fillStyle = "#C0392B";
        ctx.beginPath();
        ctx.moveTo(44, 60); ctx.lineTo(56, 60); ctx.lineTo(50, 68);
        ctx.closePath(); ctx.fill();
        ctx.strokeStyle = "#8D5524";
        ctx.lineWidth = 2.6;
        ctx.beginPath();
        ctx.moveTo(50, 68);
        ctx.quadraticCurveTo(44, 73, 40, 69);
        ctx.moveTo(50, 68);
        ctx.quadraticCurveTo(56, 73, 60, 69);
        ctx.stroke();
    }

    function paintDog(ctx) {
        // 对应 🐶：垂耳小狗
        ctx.fillStyle = "#8D6E63";
        ctx.beginPath(); oval(ctx, 20, 44, 11, 22); ctx.fill();
        ctx.beginPath(); oval(ctx, 80, 44, 11, 22); ctx.fill();

        ctx.fillStyle = "#D7B98E";
        ctx.beginPath(); ctx.arc(50, 52, 30, 0, Math.PI * 2); ctx.fill();

        ctx.fillStyle = "#FBF1E0";
        ctx.beginPath(); oval(ctx, 50, 66, 17, 13); ctx.fill();

        ctx.fillStyle = "#3B2A21";
        ctx.beginPath(); ctx.arc(38, 46, 4.6, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(62, 46, 4.6, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); oval(ctx, 50, 58, 6.5, 5); ctx.fill();

        // 舌头
        ctx.fillStyle = "#F48FB1";
        ctx.beginPath(); ctx.roundedRect(45, 72, 10, 12, 5, 5); ctx.fill();
    }

    function paintFox(ctx) {
        // 对应 🦊：尖耳、白下巴
        ctx.fillStyle = "#F57C00";
        ctx.beginPath();
        ctx.moveTo(18, 42); ctx.lineTo(26, 8); ctx.lineTo(46, 24);
        ctx.closePath(); ctx.fill();
        ctx.beginPath();
        ctx.moveTo(82, 42); ctx.lineTo(74, 8); ctx.lineTo(54, 24);
        ctx.closePath(); ctx.fill();

        ctx.fillStyle = "#FB8C00";
        ctx.beginPath(); ctx.arc(50, 52, 30, 0, Math.PI * 2); ctx.fill();

        // 白脸颊
        ctx.fillStyle = "#FFF6EA";
        ctx.beginPath();
        ctx.moveTo(22, 58);
        ctx.quadraticCurveTo(50, 96, 78, 58);
        ctx.quadraticCurveTo(50, 74, 22, 58);
        ctx.closePath(); ctx.fill();

        ctx.fillStyle = "#3B2A21";
        ctx.beginPath(); ctx.arc(37, 46, 4.6, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(63, 46, 4.6, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); oval(ctx, 50, 64, 5.5, 4.5); ctx.fill();
    }

    function paintPig(ctx) {
        // 对应 🐷：粉脑袋 + 猪鼻
        ctx.fillStyle = "#F48FB1";
        ctx.beginPath();
        ctx.moveTo(20, 44); ctx.lineTo(24, 12); ctx.lineTo(46, 26);
        ctx.closePath(); ctx.fill();
        ctx.beginPath();
        ctx.moveTo(80, 44); ctx.lineTo(76, 12); ctx.lineTo(54, 26);
        ctx.closePath(); ctx.fill();

        ctx.fillStyle = "#F8BBD0";
        ctx.beginPath(); ctx.arc(50, 52, 30, 0, Math.PI * 2); ctx.fill();

        ctx.fillStyle = "#F48FB1";
        ctx.beginPath(); oval(ctx, 50, 66, 15, 11); ctx.fill();
        ctx.fillStyle = "#C2185B";
        ctx.beginPath(); oval(ctx, 44, 66, 3.2, 4.6); ctx.fill();
        ctx.beginPath(); oval(ctx, 56, 66, 3.2, 4.6); ctx.fill();

        ctx.fillStyle = "#3B2A21";
        ctx.beginPath(); ctx.arc(37, 45, 4.6, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(63, 45, 4.6, 0, Math.PI * 2); ctx.fill();
    }

    function paintCat(ctx) {
        paintFaceBase(ctx, "#F2A65A", "#E08A3C", null);
        // 三角耳
        ctx.fillStyle = "#E08A3C";
        ctx.beginPath();
        ctx.moveTo(24, 34); ctx.lineTo(30, 10); ctx.lineTo(44, 24);
        ctx.closePath();
        ctx.fill();
        ctx.beginPath();
        ctx.moveTo(76, 34); ctx.lineTo(70, 10); ctx.lineTo(56, 24);
        ctx.closePath();
        ctx.fill();

        paintEyes(ctx, "#2F3E2E");
        ctx.strokeStyle = "#7A4A20";
        ctx.lineWidth = 3.2;
        ctx.beginPath();
        ctx.moveTo(44, 66); ctx.quadraticCurveTo(50, 70, 56, 66);
        ctx.stroke();
    }

    function paintRabbit(ctx) {
        ctx.fillStyle = "#EDE7F6";
        ctx.beginPath();
        oval(ctx, 34, 22, 9, 22);
        ctx.fill();
        ctx.beginPath();
        oval(ctx, 66, 22, 9, 22);
        ctx.fill();

        ctx.fillStyle = "#F7D6E0";
        ctx.beginPath();
        oval(ctx, 34, 24, 4.5, 14);
        ctx.fill();
        ctx.beginPath();
        oval(ctx, 66, 24, 4.5, 14);
        ctx.fill();

        ctx.fillStyle = "#F5F0FA";
        ctx.beginPath();
        ctx.arc(50, 58, 31, 0, Math.PI * 2);
        ctx.fill();

        paintEyes(ctx, "#5B4B6E");
        ctx.fillStyle = "#F4A6C0";
        ctx.beginPath();
        ctx.arc(50, 66, 4.5, 0, Math.PI * 2);
        ctx.fill();
    }

    function paintPanda(ctx) {
        ctx.fillStyle = "#2E2E2E";
        ctx.beginPath();
        ctx.arc(28, 30, 14, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.arc(72, 30, 14, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = "#FAFAFA";
        ctx.beginPath();
        ctx.arc(50, 56, 34, 0, Math.PI * 2);
        ctx.fill();

        // 黑眼圈
        ctx.fillStyle = "#333333";
        ctx.beginPath();
        oval(ctx, 39, 52, 9, 11);
        ctx.fill();
        ctx.beginPath();
        oval(ctx, 61, 52, 9, 11);
        ctx.fill();

        ctx.fillStyle = "#FFFFFF";
        ctx.beginPath();
        ctx.arc(39, 52, 3, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.arc(61, 52, 3, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = "#333333";
        ctx.beginPath();
        ctx.arc(50, 66, 4.4, 0, Math.PI * 2);
        ctx.fill();
    }

    // ---- 天气 ----
    // 天气卡上的图标只有 20~28px，线稿在这个尺寸下会糊成一团，所以这几个
    // 一律画实心。形状都用「三团圆弧拼一朵云」的公共路径，云、雨、雪、雷
    // 只在云下面挂不同的东西，视觉上是一套。

    // 云：三团实心圆 + 一条底边，各自 beginPath 后直接填充。
    // 不要写成「一条路径串三个 arc」—— arc 之间会自动拉一条连线，
    // 填出来的云中间会留一道白缺口（看起来像被咬了一口的饼）。
    // 调用方在后面再调一次 fill() 也没关系，只是重复填最后那个圆角矩形。
    function cloudPath(ctx, cx, cy, r) {
        ctx.beginPath(); ctx.arc(cx - r * 0.72, cy + r * 0.14, r * 0.60, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(cx, cy - r * 0.24, r * 0.80, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(cx + r * 0.74, cy + r * 0.12, r * 0.58, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath();
        ctx.roundedRect(cx - r * 0.74, cy + r * 0.06, r * 1.48, r * 0.68, r * 0.32, r * 0.32);
        ctx.fill();
    }

    function paintSun(ctx) {
        ctx.beginPath();
        ctx.arc(50, 50, 19, 0, Math.PI * 2);
        ctx.fill();

        ctx.beginPath();
        for (let i = 0; i < 8; i++) {
            const a = i * Math.PI / 4;
            ctx.moveTo(50 + Math.cos(a) * 28, 50 + Math.sin(a) * 28);
            ctx.lineTo(50 + Math.cos(a) * 40, 50 + Math.sin(a) * 40);
        }
        ctx.stroke();
    }

    function paintPartly(ctx) {
        // 太阳在左上，云压右下；两者同色，小尺寸下会连成一片，
        // 所以云的圆心故意偏下偏右，留出太阳的轮廓
        ctx.beginPath();
        ctx.arc(36, 34, 15, 0, Math.PI * 2);
        ctx.fill();

        ctx.beginPath();
        for (let i = 0; i < 6; i++) {
            const a = -Math.PI / 2 + i * Math.PI / 3;
            ctx.moveTo(36 + Math.cos(a) * 21, 34 + Math.sin(a) * 21);
            ctx.lineTo(36 + Math.cos(a) * 29, 34 + Math.sin(a) * 29);
        }
        ctx.stroke();

        cloudPath(ctx, 62, 68, 24);
        ctx.fill();
    }

    function paintCloud(ctx) {
        cloudPath(ctx, 50, 50, 30);
        ctx.fill();
    }

    function paintRain(ctx) {
        cloudPath(ctx, 50, 40, 26);
        ctx.fill();

        ctx.lineWidth = 8;
        ctx.beginPath();
        ctx.moveTo(34, 66); ctx.lineTo(30, 84);
        ctx.moveTo(50, 66); ctx.lineTo(46, 84);
        ctx.moveTo(66, 66); ctx.lineTo(62, 84);
        ctx.stroke();
    }

    function paintSnow(ctx) {
        cloudPath(ctx, 50, 40, 26);
        ctx.fill();

        ctx.lineWidth = 6;
        for (let k = 0; k < 3; k++) {
            const cx = 32 + k * 18;
            ctx.beginPath();
            for (let i = 0; i < 3; i++) {
                const a = i * Math.PI / 3;
                ctx.moveTo(cx - Math.cos(a) * 7, 76 - Math.sin(a) * 7);
                ctx.lineTo(cx + Math.cos(a) * 7, 76 + Math.sin(a) * 7);
            }
            ctx.stroke();
        }
    }

    function paintThunder(ctx) {
        cloudPath(ctx, 50, 38, 26);
        ctx.fill();

        ctx.beginPath();
        ctx.moveTo(56, 54);
        ctx.lineTo(36, 82);
        ctx.lineTo(50, 82);
        ctx.lineTo(44, 98);
        ctx.lineTo(66, 68);
        ctx.lineTo(52, 68);
        ctx.closePath();
        ctx.fill();
    }

    function paintFog(ctx) {
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.moveTo(26, 40); ctx.lineTo(74, 40);
        ctx.moveTo(16, 58); ctx.lineTo(84, 58);
        ctx.moveTo(30, 76); ctx.lineTo(70, 76);
        ctx.stroke();
    }

    // ---- 安全学堂：10 个主题图标（彩色）----
    // 用法与头像一致：颜色写死，忽略 color 属性。原因同样是 deepin 上没有
    // 彩色 emoji 字体 —— 🚦🔥🍎 在这里只会渲染成黑色线稿，跟 Windows 端的
    // 彩色贴纸差得太远。
    // 尺寸场景是卡片角标 38~44px，所以一律实心、两三种颜色，不做细线。
    // 这里的颜色是「实物色」（红灯红、水蓝、苹果红），与 SafetyData 里
    // 每个主题的 color（偏深，用于左侧色条和「读一读」）是两回事。

    function paintTraffic(ctx) {
        // 灯杆
        ctx.fillStyle = "#546E7A";
        ctx.beginPath(); ctx.roundedRect(45, 72, 10, 22, 5, 5); ctx.fill();
        // 灯箱
        ctx.fillStyle = "#37474F";
        ctx.beginPath(); ctx.roundedRect(30, 4, 40, 76, 14, 14); ctx.fill();
        // 三盏灯
        ctx.fillStyle = "#EF5350";
        ctx.beginPath(); ctx.arc(50, 21, 10, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = "#FFCA28";
        ctx.beginPath(); ctx.arc(50, 42, 10, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = "#66BB6A";
        ctx.beginPath(); ctx.arc(50, 63, 10, 0, Math.PI * 2); ctx.fill();
    }

    function paintHand(ctx) {
        ctx.fillStyle = "#FFB74D";
        // 手腕在下、掌心居中，四指比掌心高出半个指节
        ctx.beginPath(); ctx.roundedRect(36, 78, 28, 18, 9, 9); ctx.fill();
        ctx.beginPath(); oval(ctx, 50, 62, 26, 24); ctx.fill();

        const fingers = [[29, 30, 13, 34], [45, 22, 13, 42], [60, 26, 13, 38], [74, 38, 12, 26]];
        for (let i = 0; i < fingers.length; i++) {
            const f = fingers[i];
            ctx.beginPath();
            ctx.roundedRect(f[0], f[1], f[2], f[3], 6.5, 6.5);
            ctx.fill();
        }

        // 拇指斜向左下
        ctx.save();
        ctx.translate(26, 60);
        ctx.rotate(-0.45);
        ctx.beginPath();
        ctx.roundedRect(-20, -8, 16, 34, 8, 8);
        ctx.fill();
        ctx.restore();
    }

    function paintFlame(ctx) {
        // 外焰：左边收腰、右边顺，顶端一点偏左，比对称的火苗更像火
        ctx.fillStyle = "#FF7043";
        ctx.beginPath();
        ctx.moveTo(50, 6);
        ctx.bezierCurveTo(72, 28, 82, 44, 82, 60);
        ctx.bezierCurveTo(82, 80, 68, 94, 50, 94);
        ctx.bezierCurveTo(32, 94, 18, 80, 18, 60);
        ctx.bezierCurveTo(18, 44, 30, 34, 34, 22);
        ctx.bezierCurveTo(38, 32, 44, 38, 48, 42);
        ctx.bezierCurveTo(50, 28, 46, 16, 50, 6);
        ctx.closePath();
        ctx.fill();

        // 内焰
        ctx.fillStyle = "#FFCA28";
        ctx.beginPath();
        ctx.moveTo(50, 40);
        ctx.bezierCurveTo(64, 54, 68, 64, 68, 72);
        ctx.bezierCurveTo(68, 84, 60, 90, 50, 90);
        ctx.bezierCurveTo(40, 90, 32, 84, 32, 72);
        ctx.bezierCurveTo(32, 62, 42, 54, 50, 40);
        ctx.closePath();
        ctx.fill();
    }

    function paintSocket(ctx) {
        // 插脚
        ctx.fillStyle = "#90A4AE";
        ctx.beginPath(); ctx.roundedRect(36, 10, 10, 24, 5, 5); ctx.fill();
        ctx.beginPath(); ctx.roundedRect(54, 10, 10, 24, 5, 5); ctx.fill();
        // 插头本体
        ctx.fillStyle = "#5C6BC0";
        ctx.beginPath(); ctx.roundedRect(26, 32, 48, 40, 13, 13); ctx.fill();
        ctx.fillStyle = "#E8EAF6";
        ctx.beginPath(); ctx.arc(41, 52, 4.5, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(59, 52, 4.5, 0, Math.PI * 2); ctx.fill();
        // 电线
        ctx.strokeStyle = "#37474F";
        ctx.lineWidth = 8;
        ctx.beginPath();
        ctx.moveTo(50, 72);
        ctx.quadraticCurveTo(50, 88, 66, 90);
        ctx.stroke();
    }

    function paintDrop(ctx) {
        ctx.fillStyle = "#42A5F5";
        ctx.beginPath();
        ctx.moveTo(50, 6);
        ctx.bezierCurveTo(50, 6, 80, 46, 80, 62);
        ctx.bezierCurveTo(80, 79, 66, 92, 50, 92);
        ctx.bezierCurveTo(34, 92, 20, 79, 20, 62);
        ctx.bezierCurveTo(20, 46, 50, 6, 50, 6);
        ctx.closePath();
        ctx.fill();

        // 高光：一颗斜放的小水滴，纯色填充在缩放后仍然清楚
        ctx.fillStyle = "#90CAF9";
        ctx.beginPath();
        ctx.roundedRect(36, 54, 9, 20, 4.5, 4.5);
        ctx.fill();
    }

    function paintApple(ctx) {
        ctx.fillStyle = "#E53935";
        ctx.beginPath(); ctx.arc(39, 58, 24, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(61, 58, 24, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.roundedRect(39, 32, 22, 26, 10, 10); ctx.fill();

        // 果梗
        ctx.strokeStyle = "#6D4C41";
        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.moveTo(50, 36);
        ctx.quadraticCurveTo(52, 24, 56, 18);
        ctx.stroke();

        // 叶子
        ctx.fillStyle = "#66BB6A";
        ctx.beginPath();
        ctx.moveTo(56, 24);
        ctx.bezierCurveTo(68, 12, 84, 14, 84, 22);
        ctx.bezierCurveTo(84, 32, 66, 34, 56, 24);
        ctx.closePath();
        ctx.fill();

        // 高光
        ctx.fillStyle = "#FFCDD2";
        ctx.beginPath(); oval(ctx, 34, 50, 6, 9); ctx.fill();
    }

    function paintStorm(ctx) {
        ctx.fillStyle = "#90A4AE";
        cloudPath(ctx, 50, 40, 27);
        ctx.fill();

        ctx.fillStyle = "#FFCA28";
        ctx.beginPath();
        ctx.moveTo(56, 56);
        ctx.lineTo(36, 84);
        ctx.lineTo(50, 84);
        ctx.lineTo(44, 98);
        ctx.lineTo(66, 70);
        ctx.lineTo(52, 70);
        ctx.closePath();
        ctx.fill();
    }

    function paintLaptop(ctx) {
        // 屏幕
        ctx.fillStyle = "#37474F";
        ctx.beginPath(); ctx.roundedRect(14, 16, 72, 50, 9, 9); ctx.fill();
        ctx.fillStyle = "#4FC3F7";
        ctx.beginPath(); ctx.roundedRect(21, 23, 58, 36, 5, 5); ctx.fill();
        // 屏幕里的光标方块，暗示「上网」
        ctx.fillStyle = "#FFFFFF";
        ctx.beginPath(); ctx.roundedRect(30, 34, 22, 4, 2, 2); ctx.fill();
        ctx.beginPath(); ctx.roundedRect(30, 44, 34, 4, 2, 2); ctx.fill();
        // 底座
        ctx.fillStyle = "#546E7A";
        ctx.beginPath(); ctx.roundedRect(6, 68, 88, 12, 6, 6); ctx.fill();
    }

    function paintGuard(ctx) {
        // 盾牌：紫色底 + 白勾
        ctx.fillStyle = "#5E35B1";
        ctx.beginPath();
        ctx.moveTo(50, 4);
        ctx.lineTo(84, 18);
        ctx.lineTo(84, 50);
        ctx.bezierCurveTo(84, 74, 68, 90, 50, 96);
        ctx.bezierCurveTo(32, 90, 16, 74, 16, 50);
        ctx.lineTo(16, 18);
        ctx.closePath();
        ctx.fill();

        ctx.fillStyle = "#B39DDB";
        ctx.beginPath();
        ctx.moveTo(50, 14);
        ctx.lineTo(74, 24);
        ctx.lineTo(74, 50);
        ctx.bezierCurveTo(74, 66, 62, 78, 50, 84);
        ctx.bezierCurveTo(38, 78, 26, 66, 26, 50);
        ctx.lineTo(26, 24);
        ctx.closePath();
        ctx.fill();

        ctx.strokeStyle = "#FFFFFF";
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.moveTo(35, 50);
        ctx.lineTo(46, 61);
        ctx.lineTo(66, 38);
        ctx.stroke();
    }

    function paintPhone(ctx) {
        // 听筒：一条 U 形「握手」+ 两端各一个圆「话筒」，整体斜放 45°，
        // 让一端朝右上、一端朝左下——这是大家最认得的电话样子。
        // 先前的「粗棒 + 两头方块」画出来像哑铃；不转角度又像个笑脸。
        ctx.save();
        ctx.translate(50, 50);
        ctx.rotate(-Math.PI / 4);

        ctx.strokeStyle = "#E53935";
        ctx.lineWidth = 15;
        ctx.beginPath();
        ctx.arc(0, 0, 24, Math.PI * 0.16, Math.PI * 0.84);
        ctx.stroke();

        ctx.fillStyle = "#E53935";
        ctx.beginPath(); ctx.arc(21, 11.6, 11, 0, Math.PI * 2); ctx.fill();
        ctx.beginPath(); ctx.arc(-21, 11.6, 11, 0, Math.PI * 2); ctx.fill();
        ctx.restore();
    }

    // ======================================================================
    // 桌面环境（文件管理器 / 终端 / 任务管理器）用的界面符号
    //
    // 这批图标比前面的功能图标画得「细」一些：它们出现在窗口标题栏、
    // 工具条按钮上，尺寸只有 13～18px，线宽必须比 38px 的大图标薄，
    // 否则缩小后几个笔画会糊成一团。所以统一用 lineWidth 7 打底、
    // 需要更细的地方局部改 5 或 6。
    // ======================================================================

    function paintClose(ctx) {
        // 两条对角线。端点各缩进 22，让 X 的四个尖不顶到边框。
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.moveTo(24, 24);
        ctx.lineTo(76, 76);
        ctx.moveTo(76, 24);
        ctx.lineTo(24, 76);
        ctx.stroke();
    }

    function paintMaximize(ctx) {
        // 空心方框：右下角留一点缺口，缩小后仍然看得出是「一个框」
        ctx.lineWidth = 8;
        ctx.beginPath();
        ctx.roundedRect(19, 19, 62, 62, 10, 10);
        ctx.stroke();
    }

    function paintRestore(ctx) {
        // 还原：后面一个框 + 前面一个框，错开 16。
        // 后框先画、只画右下一半，避免两条边叠在一起变粗。
        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.moveTo(58, 18);
        ctx.lineTo(82, 18);
        ctx.lineTo(82, 42);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(82, 58);
        ctx.lineTo(82, 82);
        ctx.lineTo(58, 82);
        ctx.stroke();

        ctx.beginPath();
        ctx.roundedRect(18, 18, 46, 46, 8, 8);
        ctx.stroke();
    }

    function paintFolder(ctx) {
        // 文件夹：上面一个梯形小舌（tab）+ 下面的圆角主体。
        // 主体从 y=32 开始，tab 从 18 起，两者要有重叠，否则中间会断一条缝。
        ctx.beginPath();
        ctx.moveTo(10, 30);
        ctx.lineTo(10, 24);
        ctx.quadraticCurveTo(10, 18, 18, 18);
        ctx.lineTo(38, 18);
        ctx.quadraticCurveTo(44, 18, 47, 25);
        ctx.lineTo(50, 32);
        ctx.stroke();

        ctx.beginPath();
        ctx.roundedRect(10, 30, 80, 52, 10, 10);
        ctx.stroke();

        // 中间一道横线当作「里面装了东西」的暗示，纯装饰但很有辨识度
        ctx.lineWidth = 5;
        ctx.globalAlpha = 0.55;
        ctx.beginPath();
        ctx.moveTo(24, 52);
        ctx.lineTo(60, 52);
        ctx.stroke();
        ctx.globalAlpha = 1;
    }

    function paintTerminal(ctx) {
        // 终端：一个圆角框 + 里面一个 "›_" 提示符。
        // 提示符是这个图标唯一有辨识度的部分，所以画得比框粗。
        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.roundedRect(9, 16, 82, 68, 12, 12);
        ctx.stroke();

        ctx.lineWidth = 8;
        ctx.beginPath();
        ctx.moveTo(28, 38);
        ctx.lineTo(43, 50);
        ctx.lineTo(28, 62);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(52, 62);
        ctx.lineTo(70, 62);
        ctx.stroke();
    }

    function paintChip(ctx) {
        // 任务管理器：一颗 CPU 芯片 —— 中间方块 + 四边各两根引脚。
        // 引脚要短（从 30 到 14），太长会在 14px 下连成一片。
        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.roundedRect(28, 28, 44, 44, 9, 9);
        ctx.stroke();

        ctx.lineWidth = 8;
        const pins = [
            [38, 30, 38, 13], [62, 30, 62, 13],
            [38, 70, 38, 87], [62, 70, 62, 87],
            [30, 38, 13, 38], [30, 62, 13, 62],
            [70, 38, 87, 38], [70, 62, 87, 62]
        ];
        ctx.beginPath();
        for (let i = 0; i < pins.length; i++) {
            ctx.moveTo(pins[i][0], pins[i][1]);
            ctx.lineTo(pins[i][2], pins[i][3]);
        }
        ctx.stroke();

        // 芯片正中的小方块：缩小后仍有「有内核」的层次感
        ctx.beginPath();
        ctx.roundedRect(42, 42, 16, 16, 4, 4);
        ctx.fill();
    }

    function paintBack(ctx) {
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.moveTo(60, 20);
        ctx.lineTo(32, 50);
        ctx.lineTo(60, 80);
        ctx.stroke();
    }

    function paintForward(ctx) {
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.moveTo(40, 20);
        ctx.lineTo(68, 50);
        ctx.lineTo(40, 80);
        ctx.stroke();
    }

    function paintUp(ctx) {
        // 向上一级：竖杆 + 箭头，比单纯一个尖角好认（那个太小像三角）
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.moveTo(50, 18);
        ctx.lineTo(50, 82);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(24, 44);
        ctx.lineTo(50, 18);
        ctx.lineTo(76, 44);
        ctx.stroke();
    }

    function paintSearch(ctx) {
        // 放大镜：圆 + 斜柄。半径 26 让圆和柄的比例接近常见图标。
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.arc(43, 43, 25, 0, Math.PI * 2);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(62, 62);
        ctx.lineTo(84, 84);
        ctx.stroke();
    }

    function paintTrash(ctx) {
        // 垃圾桶：盖子 + 桶身 + 两道竖纹。
        // 盖子画成一根粗横线而不是梯形 —— 梯形在 14px 下会变成一个点。
        ctx.lineWidth = 8;
        ctx.beginPath();
        ctx.moveTo(16, 28);
        ctx.lineTo(84, 28);
        ctx.stroke();

        // 提手
        ctx.beginPath();
        ctx.moveTo(38, 28);
        ctx.lineTo(38, 18);
        ctx.lineTo(62, 18);
        ctx.lineTo(62, 28);
        ctx.stroke();

        // 桶身
        ctx.beginPath();
        ctx.moveTo(24, 28);
        ctx.lineTo(30, 84);
        ctx.quadraticCurveTo(31, 88, 36, 88);
        ctx.lineTo(64, 88);
        ctx.quadraticCurveTo(69, 88, 70, 84);
        ctx.lineTo(76, 28);
        ctx.stroke();

        // 两道竖纹
        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.moveTo(43, 42);
        ctx.lineTo(45, 74);
        ctx.moveTo(57, 42);
        ctx.lineTo(55, 74);
        ctx.stroke();
    }

    function paintPlus(ctx) {
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.moveTo(50, 20);
        ctx.lineTo(50, 80);
        ctx.moveTo(20, 50);
        ctx.lineTo(80, 50);
        ctx.stroke();
    }

    function paintRefresh(ctx) {
        // 刷新：一个缺口圆 + 缺口处的箭头。
        // 圆用 arc 画 300°，剩下 60° 的空当正好放箭头。
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.arc(50, 50, 30, Math.PI * 0.62, Math.PI * 2.28);
        ctx.stroke();

        // 箭头三角，落在圆右上方的缺口里
        ctx.save();
        ctx.translate(72, 34);
        ctx.rotate(Math.PI * 0.18);
        ctx.beginPath();
        ctx.moveTo(0, -12);
        ctx.lineTo(12, 6);
        ctx.lineTo(-12, 6);
        ctx.closePath();
        ctx.fill();
        ctx.restore();
    }

    function paintDoc(ctx) {
        // 文件：右上角折角的纸 + 三条横线。折角是「这是文件」的关键特征。
        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.moveTo(24, 12);
        ctx.lineTo(60, 12);
        ctx.lineTo(80, 32);
        ctx.lineTo(80, 88);
        ctx.lineTo(24, 88);
        ctx.closePath();
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(60, 12);
        ctx.lineTo(60, 32);
        ctx.lineTo(80, 32);
        ctx.stroke();

        ctx.lineWidth = 5;
        ctx.beginPath();
        ctx.moveTo(36, 50);
        ctx.lineTo(68, 50);
        ctx.moveTo(36, 64);
        ctx.lineTo(68, 64);
        ctx.stroke();
    }

    function paintPicture(ctx) {
        // 图片：相框 + 里面一座小山 + 一个太阳。
        // 剪影比「画个风景」在小尺寸下更稳 —— 只要看得出有山有太阳就够。
        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.roundedRect(12, 20, 76, 60, 9, 9);
        ctx.stroke();

        ctx.beginPath();
        ctx.arc(34, 40, 7, 0, Math.PI * 2);
        ctx.fill();

        ctx.beginPath();
        ctx.moveTo(16, 74);
        ctx.lineTo(40, 50);
        ctx.lineTo(56, 66);
        ctx.lineTo(68, 56);
        ctx.lineTo(84, 74);
        ctx.stroke();
    }

    function paintArrow(ctx) {
        // 实心右箭头，用在「前往」这类按钮上
        ctx.beginPath();
        ctx.moveTo(26, 22);
        ctx.lineTo(78, 50);
        ctx.lineTo(26, 78);
        ctx.closePath();
        ctx.fill();
    }

    function paintHome(ctx) {
        // 房子：屋顶两个斜边 + 一个方形屋身 + 中间的门。
        // 门画成实心小方块而不是描边，14px 下才看得见。
        ctx.lineWidth = 8;
        ctx.beginPath();
        ctx.moveTo(14, 48);
        ctx.lineTo(50, 16);
        ctx.lineTo(86, 48);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(24, 44);
        ctx.lineTo(24, 86);
        ctx.lineTo(76, 86);
        ctx.lineTo(76, 44);
        ctx.stroke();

        ctx.beginPath();
        ctx.roundedRect(42, 60, 16, 26, 3, 3);
        ctx.fill();
    }

    function paintMonitor(ctx) {
        // 显示器：屏幕 + 支架 + 底座
        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.roundedRect(10, 14, 80, 54, 8, 8);
        ctx.stroke();

        ctx.lineWidth = 8;
        ctx.beginPath();
        ctx.moveTo(50, 68);
        ctx.lineTo(50, 82);
        ctx.moveTo(30, 86);
        ctx.lineTo(70, 86);
        ctx.stroke();
    }

    function paintDownload(ctx) {
        // 下载：向下的箭头落进一个敞口的托盘
        ctx.lineWidth = 8;
        ctx.beginPath();
        ctx.moveTo(50, 14);
        ctx.lineTo(50, 60);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(30, 42);
        ctx.lineTo(50, 62);
        ctx.lineTo(70, 42);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(16, 66);
        ctx.lineTo(16, 84);
        ctx.lineTo(84, 84);
        ctx.lineTo(84, 66);
        ctx.stroke();
    }

    function paintDisk(ctx) {
        // 硬盘：一个带指示灯的长方盒，右上角一条斜切边
        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.roundedRect(8, 26, 84, 48, 9, 9);
        ctx.stroke();

        // 指示灯
        ctx.beginPath();
        ctx.arc(76, 50, 6, 0, Math.PI * 2);
        ctx.fill();

        // 左侧两条散热纹
        ctx.lineWidth = 5;
        ctx.beginPath();
        ctx.moveTo(24, 42);
        ctx.lineTo(52, 42);
        ctx.moveTo(24, 58);
        ctx.lineTo(52, 58);
        ctx.stroke();
    }
}
