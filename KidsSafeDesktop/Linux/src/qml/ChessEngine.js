.pragma library

/*
 * 国际象棋规则引擎（纯逻辑，不含任何界面）。
 *
 * 分成「规则」和「界面」两层的原因很简单：走子规则是最容易写错的部分
 * （吃过路兵、王车易位、升变、将军判定的边界情况），放在 QML 里既没法单独
 * 测试，也没法复用。所以规则全在这个文件里，用普通 JS 写，不 import 任何
 * Qt 类型 —— 这样可以直接用 node 跑 perft 校验（见文件末尾的说明）。
 *
 * 坐标约定：sq = y * 8 + x，x 从左（a 线）到右（h 线），y 从上（第 8 横线）
 * 到下（第 1 横线）。所以白棋在下面（y = 6、7），黑棋在上面（y = 0、1），
 * 和孩子看到的棋盘方向一致，界面不用做翻转。
 *
 * 棋盘里一格的值是空串或两字符的 "wP" / "bK" 这种：
 *   颜色 w/b + 兵种 P N B R Q K。
 *
 * 状态对象（每次走子返回新对象，旧的不动）：
 *   { board, turn, castle: {wk,wq,bk,bq}, ep, half, full }
 *   ep    —— 可以吃过路兵的目标格下标，没有就是 -1
 *   half  —— 五十回合规则用的半步计数
 *
 * 走子对象：
 *   { from, to, piece, cap, promo, castle, ep }
 *   promo  —— "Q"/"R"/"B"/"N"，普通走子为空串
 *   castle —— "K"/"Q"，不是易位为空串
 *   ep     —— 是不是吃过路兵
 */

var VALUE = { "P": 100, "N": 320, "B": 330, "R": 500, "Q": 900, "K": 20000 };

var START_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

// ---- 建局 / 解析 ----

function newGame() {
    return fromFen(START_FEN);
}

function fromFen(fen) {
    var parts = fen.split(" ");
    var board = [];
    for (var i = 0; i < 64; i++) {
        board.push("");
    }
    var rows = parts[0].split("/");
    for (var y = 0; y < 8 && y < rows.length; y++) {
        var x = 0;
        var row = rows[y];
        for (var k = 0; k < row.length; k++) {
            var ch = row.charAt(k);
            if (ch >= "1" && ch <= "8") {
                x += parseInt(ch, 10);
            } else if (x < 8) {
                var color = (ch === ch.toUpperCase()) ? "w" : "b";
                board[y * 8 + x] = color + ch.toUpperCase();
                x++;
            }
        }
    }

    var castleText = parts.length > 2 ? parts[2] : "-";
    return {
        board: board,
        turn: (parts.length > 1 && parts[1] === "b") ? "b" : "w",
        castle: {
            wk: castleText.indexOf("K") >= 0,
            wq: castleText.indexOf("Q") >= 0,
            bk: castleText.indexOf("k") >= 0,
            bq: castleText.indexOf("q") >= 0
        },
        ep: (parts.length > 3 && parts[3] !== "-") ? squareOf(parts[3]) : -1,
        half: parts.length > 4 ? (parseInt(parts[4], 10) || 0) : 0,
        full: parts.length > 5 ? (parseInt(parts[5], 10) || 1) : 1
    };
}

function squareOf(name) {
    if (name.length !== 2) {
        return -1;
    }
    var x = name.charCodeAt(0) - 97;
    var y = 8 - parseInt(name.charAt(1), 10);
    if (x < 0 || x > 7 || y < 0 || y > 7) {
        return -1;
    }
    return y * 8 + x;
}

// ---- 小工具 ----

function colorOf(piece) {
    return piece.charAt(0);
}

function typeOf(piece) {
    return piece.charAt(1);
}

function other(color) {
    return color === "w" ? "b" : "w";
}

function kingSquare(b, color) {
    var target = color + "K";
    for (var i = 0; i < 64; i++) {
        if (b[i] === target) {
            return i;
        }
    }
    return -1;
}

// ---- 走子生成 ----

var KNIGHT_DELTAS = [[1, 2], [2, 1], [2, -1], [1, -2], [-1, -2], [-2, -1], [-2, 1], [-1, 2]];
var KING_DELTAS = [[1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1], [0, -1], [1, -1]];
var BISHOP_DIRS = [[1, 1], [1, -1], [-1, -1], [-1, 1]];
var ROOK_DIRS = [[1, 0], [0, 1], [-1, 0], [0, -1]];

function newMove(from, to, piece, cap) {
    return {
        from: from,
        to: to,
        piece: piece,
        cap: cap || "",
        promo: "",
        castle: "",
        ep: false
    };
}

/*
 * 生成伪合法走子（不检查走完之后自己的王是不是被将）。
 * from 传了（>= 0）就只生成那一格的 —— 界面上「点了一个棋子」时用，
 * 省得为了高亮一个棋子的落点算整盘。
 */
function genPseudo(state, from) {
    var b = state.board;
    var me = state.turn;
    var out = [];
    var first = (from === undefined || from === null || from < 0) ? 0 : from;
    var last = (from === undefined || from === null || from < 0) ? 63 : from;

    for (var sq = first; sq <= last; sq++) {
        var p = b[sq];
        if (p === "" || colorOf(p) !== me) {
            continue;
        }
        var x = sq & 7;
        var y = sq >> 3;
        var t = typeOf(p);

        if (t === "P") {
            genPawn(state, sq, x, y, p, out);
        } else if (t === "N") {
            for (var i = 0; i < KNIGHT_DELTAS.length; i++) {
                var nx = x + KNIGHT_DELTAS[i][0];
                var ny = y + KNIGHT_DELTAS[i][1];
                if (nx < 0 || nx > 7 || ny < 0 || ny > 7) {
                    continue;
                }
                var to = ny * 8 + nx;
                if (b[to] === "" || colorOf(b[to]) !== me) {
                    out.push(newMove(sq, to, p, b[to]));
                }
            }
        } else if (t === "K") {
            for (var j = 0; j < KING_DELTAS.length; j++) {
                var kx = x + KING_DELTAS[j][0];
                var ky = y + KING_DELTAS[j][1];
                if (kx < 0 || kx > 7 || ky < 0 || ky > 7) {
                    continue;
                }
                var kto = ky * 8 + kx;
                if (b[kto] === "" || colorOf(b[kto]) !== me) {
                    out.push(newMove(sq, kto, p, b[kto]));
                }
            }
            genCastle(state, sq, x, y, p, out);
        } else {
            var dirs = (t === "B") ? BISHOP_DIRS
                     : (t === "R") ? ROOK_DIRS
                                   : BISHOP_DIRS.concat(ROOK_DIRS);
            for (var d = 0; d < dirs.length; d++) {
                var cx = x + dirs[d][0];
                var cy = y + dirs[d][1];
                while (cx >= 0 && cx <= 7 && cy >= 0 && cy <= 7) {
                    var target = cy * 8 + cx;
                    if (b[target] === "") {
                        out.push(newMove(sq, target, p, ""));
                    } else {
                        if (colorOf(b[target]) !== me) {
                            out.push(newMove(sq, target, p, b[target]));
                        }
                        break;   // 挡住了，后面再远的格子走不到
                    }
                    cx += dirs[d][0];
                    cy += dirs[d][1];
                }
            }
        }
    }
    return out;
}

function genPawn(state, sq, x, y, p, out) {
    var b = state.board;
    var me = colorOf(p);
    var dir = (me === "w") ? -1 : 1;
    var startRank = (me === "w") ? 6 : 1;
    var lastRank = (me === "w") ? 0 : 7;

    var ny = y + dir;
    if (ny >= 0 && ny <= 7) {
        var one = ny * 8 + x;
        if (b[one] === "") {
            pushPawnMove(out, sq, one, p, "", ny === lastRank);
            // 起手两格：中间那格也得是空的（上面已经确认了前格是空的）
            if (y === startRank && b[(y + 2 * dir) * 8 + x] === "") {
                var two = newMove(sq, (y + 2 * dir) * 8 + x, p, "");
                out.push(two);
            }
        }
        for (var dx = -1; dx <= 1; dx += 2) {
            var cx = x + dx;
            if (cx < 0 || cx > 7) {
                continue;
            }
            var capSq = ny * 8 + cx;
            if (b[capSq] !== "" && colorOf(b[capSq]) !== me) {
                pushPawnMove(out, sq, capSq, p, b[capSq], ny === lastRank);
            } else if (b[capSq] === "" && capSq === state.ep) {
                var epMove = newMove(sq, capSq, p, other(me) + "P");
                epMove.ep = true;
                out.push(epMove);
            }
        }
    }
}

function pushPawnMove(out, from, to, p, cap, promote) {
    if (!promote) {
        out.push(newMove(from, to, p, cap));
        return;
    }
    var kinds = ["Q", "R", "B", "N"];
    for (var i = 0; i < kinds.length; i++) {
        var m = newMove(from, to, p, cap);
        m.promo = kinds[i];
        out.push(m);
    }
}

function genCastle(state, sq, x, y, p, out) {
    var b = state.board;
    var me = colorOf(p);
    // 王必须还在原位，被将着也不能易位
    if (me === "w" && sq !== 60) {
        return;
    }
    if (me === "b" && sq !== 4) {
        return;
    }
    if (attacked(b, sq, other(me))) {
        return;
    }
    var rightK = (me === "w") ? state.castle.wk : state.castle.bk;
    var rightQ = (me === "w") ? state.castle.wq : state.castle.bq;

    if (rightK && b[sq + 1] === "" && b[sq + 2] === ""
            && !attacked(b, sq + 1, other(me)) && !attacked(b, sq + 2, other(me))) {
        var k = newMove(sq, sq + 2, p, "");
        k.castle = "K";
        out.push(k);
    }
    if (rightQ && b[sq - 1] === "" && b[sq - 2] === "" && b[sq - 3] === ""
            && !attacked(b, sq - 1, other(me)) && !attacked(b, sq - 2, other(me))) {
        var q = newMove(sq, sq - 2, p, "");
        q.castle = "Q";
        out.push(q);
    }
}

/*
 * 某一格是否被 by 方攻击。只看格子，不看好坏 —— 将死判定、易位合法性都靠它。
 */
function attacked(b, sq, by) {
    if (sq < 0) {
        return false;
    }
    var x = sq & 7;
    var y = sq >> 3;

    // 兵：白兵朝上走，所以攻击下方一格的是「y + 1」上的白兵
    var pd = (by === "w") ? 1 : -1;
    var py = y + pd;
    if (py >= 0 && py <= 7) {
        var pawn = by + "P";
        if (x > 0 && b[py * 8 + x - 1] === pawn) {
            return true;
        }
        if (x < 7 && b[py * 8 + x + 1] === pawn) {
            return true;
        }
    }

    var knight = by + "N";
    for (var i = 0; i < KNIGHT_DELTAS.length; i++) {
        var nx = x + KNIGHT_DELTAS[i][0];
        var ny = y + KNIGHT_DELTAS[i][1];
        if (nx >= 0 && nx <= 7 && ny >= 0 && ny <= 7 && b[ny * 8 + nx] === knight) {
            return true;
        }
    }

    var king = by + "K";
    for (var j = 0; j < KING_DELTAS.length; j++) {
        var kx = x + KING_DELTAS[j][0];
        var ky = y + KING_DELTAS[j][1];
        if (kx >= 0 && kx <= 7 && ky >= 0 && ky <= 7 && b[ky * 8 + kx] === king) {
            return true;
        }
    }

    for (var d = 0; d < BISHOP_DIRS.length; d++) {
        if (scan(b, x, y, BISHOP_DIRS[d], by + "B", by + "Q")) {
            return true;
        }
    }
    for (var r = 0; r < ROOK_DIRS.length; r++) {
        if (scan(b, x, y, ROOK_DIRS[r], by + "R", by + "Q")) {
            return true;
        }
    }
    return false;
}

function scan(b, x, y, dir, slider, queen) {
    var cx = x + dir[0];
    var cy = y + dir[1];
    while (cx >= 0 && cx <= 7 && cy >= 0 && cy <= 7) {
        var p = b[cy * 8 + cx];
        if (p !== "") {
            return (p === slider || p === queen);
        }
        cx += dir[0];
        cy += dir[1];
    }
    return false;
}

function inCheck(state, color) {
    return attacked(state.board, kingSquare(state.board, color), other(color));
}

/*
 * 真正能走的着法：伪合法走子逐个试走，走完自己的王还挨着将就丢掉。
 * 这一步也顺带把「被牵制的子不能乱动」「易位不能路过被攻击的格子」兜住了
 * （后者在 genCastle 里已经单独查过，两处都不算多余）。
 */
function legalMoves(state) {
    var pseudo = genPseudo(state, -1);
    var out = [];
    for (var i = 0; i < pseudo.length; i++) {
        var ns = applyMove(state, pseudo[i]);
        var ks = kingSquare(ns.board, state.turn);
        if (ks >= 0 && !attacked(ns.board, ks, ns.turn)) {
            out.push(pseudo[i]);
        }
    }
    return out;
}

function legalMovesFrom(state, from) {
    var pseudo = genPseudo(state, from);
    var me = state.turn;
    var out = [];
    for (var i = 0; i < pseudo.length; i++) {
        var ns = applyMove(state, pseudo[i]);
        var ks = kingSquare(ns.board, me);
        if (ks >= 0 && !attacked(ns.board, ks, other(me))) {
            out.push(pseudo[i]);
        }
    }
    return out;
}

// ---- 执行走子 ----

function applyMove(state, m) {
    var b = state.board.slice();
    var castle = {
        wk: state.castle.wk, wq: state.castle.wq,
        bk: state.castle.bk, bq: state.castle.bq
    };
    var piece = b[m.from];
    var me = colorOf(piece);
    var t = typeOf(piece);
    var ep = -1;
    var half = state.half + 1;

    b[m.from] = "";
    if (m.ep) {
        // 被吃的兵不在目标格上，而在它「身后」那一格
        b[m.to + (me === "w" ? 8 : -8)] = "";
    }
    b[m.to] = m.promo !== "" ? (me + m.promo) : piece;

    if (m.castle === "K") {
        b[m.to - 1] = b[m.to + 1];   // 车从 h 线挪到 f 线
        b[m.to + 1] = "";
    } else if (m.castle === "Q") {
        b[m.to + 1] = b[m.to - 2];   // 车从 a 线挪到 d 线
        b[m.to - 2] = "";
    }

    if (t === "P" || m.cap !== "" || m.ep) {
        half = 0;
    }

    if (t === "P" && Math.abs((m.to >> 3) - (m.from >> 3)) === 2) {
        ep = (m.from + m.to) / 2;
    }

    if (t === "K") {
        if (me === "w") {
            castle.wk = false;
            castle.wq = false;
        } else {
            castle.bk = false;
            castle.bq = false;
        }
    }
    if (t === "R") {
        if (m.from === 56) { castle.wq = false; }
        if (m.from === 63) { castle.wk = false; }
        if (m.from === 0) { castle.bq = false; }
        if (m.from === 7) { castle.bk = false; }
    }
    // 车被吃掉了，对应的易位权也没了
    if (m.to === 56) { castle.wq = false; }
    if (m.to === 63) { castle.wk = false; }
    if (m.to === 0) { castle.bq = false; }
    if (m.to === 7) { castle.bk = false; }

    return {
        board: b,
        turn: other(me),
        castle: castle,
        ep: ep,
        half: half,
        full: state.full + (me === "b" ? 1 : 0)
    };
}

// ---- 局面判定 ----

/*
 * "ok" / "check" / "checkmate" / "stalemate" / "draw50" / "material"
 * 界面只需要认这几个字符串，不用自己再判一次。
 */
function status(state) {
    var moves = legalMoves(state);
    var checked = inCheck(state, state.turn);
    if (moves.length === 0) {
        return checked ? "checkmate" : "stalemate";
    }
    if (state.half >= 100) {
        return "draw50";
    }
    if (insufficient(state.board)) {
        return "material";
    }
    return checked ? "check" : "ok";
}

// 双方都只剩王，或者一边只剩王、另一边只有王 + 单马/单象：够不着将死，算和棋
function insufficient(b) {
    var counts = { "w": { "N": 0, "B": 0, "R": 0, "Q": 0, "P": 0 },
                   "b": { "N": 0, "B": 0, "R": 0, "Q": 0, "P": 0 } };
    for (var i = 0; i < 64; i++) {
        var p = b[i];
        if (p === "") {
            continue;
        }
        var t = typeOf(p);
        if (t !== "K") {
            counts[colorOf(p)][t]++;
        }
    }
    var wHeavy = counts.w.R + counts.w.Q + counts.w.P;
    var bHeavy = counts.b.R + counts.b.Q + counts.b.P;
    if (wHeavy + bHeavy > 0) {
        return false;
    }
    var wMinor = counts.w.N + counts.w.B;
    var bMinor = counts.b.N + counts.b.B;
    return (wMinor + bMinor) <= 1;
}

function isCapture(m) {
    return m.cap !== "" || m.ep;
}

// ---- 评估与搜索 ----

// 从白方视角算分：子力 + 一点位置分（靠中心、兵往前走）
function evaluateWhite(state) {
    var b = state.board;
    var score = 0;
    for (var sq = 0; sq < 64; sq++) {
        var p = b[sq];
        if (p === "") {
            continue;
        }
        var c = colorOf(p);
        var t = typeOf(p);
        var x = sq & 7;
        var y = sq >> 3;
        var v = VALUE[t];
        var cx = Math.abs(3.5 - x);
        var cy = Math.abs(3.5 - y);
        if (t === "N" || t === "B") {
            v += Math.round((7 - cx - cy) * 4);
        } else if (t === "P") {
            v += (c === "w" ? (6 - y) : (y - 1)) * 8;
        }
        score += (c === "w" ? v : -v);
    }
    return score;
}

function sideScore(state) {
    var s = evaluateWhite(state);
    return state.turn === "w" ? s : -s;
}

function orderMoves(state, moves) {
    // 先看吃大子：只为了剪枝，不追求严格排序
    for (var i = 0; i < moves.length; i++) {
        moves[i]._rank = moves[i].cap === "" ? 0
                       : (VALUE[typeOf(moves[i].cap)] - VALUE[typeOf(moves[i].piece)] / 10);
    }
    moves.sort(function (a, b) { return b._rank - a._rank; });
}

function negamax(state, depth, alpha, beta, qleft) {
    if (depth <= 0) {
        // 叶子节点也要认将死：只做静态评估的话，「一步杀」会被当成普通局面
        // 按子力算分，电脑就会放着将死不走（这个 bug 在「一步杀」用例里抓到过）。
        // 将死必然是「被将且无棋可走」，所以先用 inCheck 挡一道，
        // 免得到每个叶子节点都生成一遍全部着法。
        if (inCheck(state, state.turn) && legalMoves(state).length === 0) {
            return -(29000 + depth);
        }
        return quiesce(state, alpha, beta, qleft);
    }
    var moves = legalMoves(state);
    if (moves.length === 0) {
        return inCheck(state, state.turn) ? -(29000 + depth) : 0;
    }
    orderMoves(state, moves);
    var best = -1000000;
    for (var i = 0; i < moves.length; i++) {
        var sc = -negamax(applyMove(state, moves[i]), depth - 1, -beta, -alpha, qleft);
        if (sc > best) {
            best = sc;
        }
        if (best > alpha) {
            alpha = best;
        }
        if (alpha >= beta) {
            break;
        }
    }
    return best;
}

/*
 * 静止搜索：只看吃子。只看 N 层有个明显的毛病 —— 算到「我白吃一个后」
 * 就停手，等于默认对方不还手，电脑会一头撞进兑子陷阱。多看几层吃子
 * 便宜又有效。
 */
function quiesce(state, alpha, beta, left) {
    var stand = sideScore(state);
    if (left <= 0) {
        return stand;
    }
    if (stand >= beta) {
        return beta;
    }
    if (stand > alpha) {
        alpha = stand;
    }
    var all = legalMoves(state);
    var caps = [];
    for (var i = 0; i < all.length; i++) {
        if (isCapture(all[i])) {
            caps.push(all[i]);
        }
    }
    orderMoves(state, caps);
    for (var j = 0; j < caps.length; j++) {
        var sc = -quiesce(applyMove(state, caps[j]), -beta, -alpha, left - 1);
        if (sc >= beta) {
            return beta;
        }
        if (sc > alpha) {
            alpha = sc;
        }
    }
    return alpha;
}

/*
 * 挑一步走。level 1/2/3 对应大厅的三档难度：
 *   1 —— 只看一步，而且分差不多的着法里随机挑，孩子有赢的机会；
 *   2 —— 看两步 + 静止搜索；
 *   3 —— 看三步 + 静止搜索，仍然会在最优解附近留一点随机，免得每局一模一样。
 * 返回 null 表示没棋可走（将死或和棋）。
 *
 * 分两遍筛，是为了「既要随机、又不能选错」：
 *   第一遍只做静态评估（不往下看），把明显吃亏的着法先扔掉；
 *   第二遍只对剩下的候选做深搜，而且用全窗口 —— 只有全窗口算出来的分
 *   才是真实分数。早先的写法在根节点收窄了 alpha，被剪掉的着法会返回一个
 *   「上界」，和最优分相等，结果「白吃一个车」和「随手走一步」被判成同分，
 *   电脑就真的会随手走一步（这个 bug 在离线用例里抓到过一次）。
 */
function bestMove(state, level) {
    var moves = legalMoves(state);
    if (moves.length === 0) {
        return null;
    }
    var depth = (level >= 3) ? 3 : (level === 2 ? 2 : 1);
    var slack = (level >= 3) ? 10 : (level === 2 ? 45 : 150);
    var qleft = (level === 1) ? 0 : 2;

    // 第一遍：静态评估排序，顺便筛掉一眼就亏的着法
    var scored = [];
    for (var i = 0; i < moves.length; i++) {
        scored.push({
            "move": moves[i],
            "quick": -negamax(applyMove(state, moves[i]), 0, -1000000, 1000000, 0)
        });
    }
    scored.sort(function (a, b) { return b.quick - a.quick; });

    var topQuick = scored[0].quick;
    var shortlist = [];
    for (var k = 0; k < scored.length && k < 14; k++) {
        if (scored[k].quick >= topQuick - slack || k < 3) {
            shortlist.push(scored[k].move);
        }
    }

    // 第二遍：候选里深搜，全窗口
    var best = -1000000;
    var pool = [];
    for (var j = 0; j < shortlist.length; j++) {
        var sc = -negamax(applyMove(state, shortlist[j]), depth - 1, -1000000, 1000000, qleft);
        if (sc > best) {
            best = sc;
            pool = [shortlist[j]];
        } else if (sc >= best - slack) {
            pool.push(shortlist[j]);
        }
    }
    return pool[Math.floor(Math.random() * pool.length)];
}
