// 国际象棋引擎的离线校验（不需要 Qt，只要能跑 node）。
//
//   node tests/chess_engine_test.js
//
// 为什么单独拿出来测：走子规则里最容易错的是「易位 / 吃过路兵 / 升变 / 牵制」
// 这些边界情况，塞在 QML 里就只能靠眼睛盯棋盘发现问题。引擎是纯 JS，可以直接
// 用 perft（把局面往后数 N 层，统计所有合法着法的总数）跟公认的数字对齐。
//
// perft 的参考值来自 chessprogramming.org，任何一条对不上都说明着法生成有问题。

const fs = require('fs');
const path = require('path');

const FILE = path.join(__dirname, '..', 'src', 'qml', 'ChessEngine.js');
const source = fs.readFileSync(FILE, 'utf8').replace(/^\.pragma library\s*/, '');

const E = new Function(
    source +
    '\nreturn { newGame, fromFen, legalMoves, legalMovesFrom, applyMove, status,' +
    ' inCheck, bestMove, evaluateWhite, squareOf };'
)();

let fail = 0;

function ok(name, got, want) {
    const pass = JSON.stringify(got) === JSON.stringify(want);
    if (!pass) {
        fail++;
    }
    console.log((pass ? 'PASS ' : 'FAIL ') + name
                + (pass ? '' : `  got=${JSON.stringify(got)} want=${JSON.stringify(want)}`));
}

function perft(state, depth) {
    if (depth === 0) {
        return 1;
    }
    let n = 0;
    const moves = E.legalMoves(state);
    for (let i = 0; i < moves.length; i++) {
        n += perft(E.applyMove(state, moves[i]), depth - 1);
    }
    return n;
}

// ---- 1. perft：开局 ----
const start = E.newGame();
ok('开局走法 = 20', E.legalMoves(start).length, 20);
ok('perft(1) = 20', perft(start, 1), 20);
ok('perft(2) = 400', perft(start, 2), 400);
ok('perft(3) = 8902', perft(start, 3), 8902);

// ---- 2. perft：Kiwipete（易位 / 吃过路兵 / 牵制都在这一张图上）----
const kiwi = E.fromFen('r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1');
ok('kiwipete perft(1) = 48', perft(kiwi, 1), 48);
ok('kiwipete perft(2) = 2039', perft(kiwi, 2), 2039);
ok('kiwipete perft(3) = 97862', perft(kiwi, 3), 97862);

// ---- 3. perft：升变密集的局面 ----
const promo = E.fromFen('n1n5/PPPk4/8/8/8/8/4Kppp/5N1N b - - 0 1');
ok('升变局面 perft(1) = 24', perft(promo, 1), 24);
ok('升变局面 perft(2) = 496', perft(promo, 2), 496);

// ---- 4. 规则边界 ----
const epPos = E.fromFen('rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3');
const epMove = E.legalMoves(epPos).filter(m => m.ep)[0];
ok('能生成吃过路兵', !!epMove, true);
if (epMove) {
    const after = E.applyMove(epPos, epMove);
    ok('过路兵从盘上消失', after.board[E.squareOf('f5')], '');
    ok('兵落在 f6', after.board[E.squareOf('f6')], 'wP');
}

const cas = E.fromFen('r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1');
const kingSide = E.legalMoves(cas).filter(m => m.castle === 'K')[0];
ok('能生成短易位', !!kingSide, true);
if (kingSide) {
    const after = E.applyMove(cas, kingSide);
    ok('易位后王在 g1', after.board[E.squareOf('g1')], 'wK');
    ok('易位后车在 f1', after.board[E.squareOf('f1')], 'wR');
    ok('易位后 h1 空了', after.board[E.squareOf('h1')], '');
}

const checked = E.fromFen('r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1');
checked.board[E.squareOf('e8')] = '';
checked.board[E.squareOf('e7')] = 'bR';      // 黑车正对白王
ok('被将军时不能易位', E.legalMoves(checked).filter(m => m.castle !== '').length, 0);

const promPos = E.fromFen('8/P7/8/8/8/8/8/K6k w - - 0 1');
ok('升变有 4 种选择', E.legalMoves(promPos).filter(m => m.promo !== '').length, 4);

// 傻瓜将死：1.f3 e5 2.g4 Qh4#
let fool = E.newGame();
for (const [a, b] of [['f2', 'f3'], ['e7', 'e5'], ['g2', 'g4'], ['d8', 'h4']]) {
    const mv = E.legalMoves(fool).filter(m => m.from === E.squareOf(a) && m.to === E.squareOf(b))[0];
    if (!mv) {
        throw new Error('找不到着法 ' + a + b);
    }
    fool = E.applyMove(fool, mv);
}
ok('将死判定', E.status(fool), 'checkmate');
ok('将死局面没有着法', E.legalMoves(fool).length, 0);

ok('逼和判定', E.status(E.fromFen('7k/5Q2/6K1/8/8/8/8/8 b - - 0 1')), 'stalemate');
ok('双王算和棋', E.status(E.fromFen('8/8/4k3/8/8/3K4/8/8 w - - 0 1')), 'material');

// ---- 5. 电脑选步 ----
// 白后就在黑车的嘴边，看两步以上必须吃掉
const hanging = E.fromFen('4k3/8/8/8/8/8/4r3/3QK3 w - - 0 1');
for (const lv of [2, 3]) {
    ok(`level ${lv} 吃掉没保护的子`, E.bestMove(hanging, lv).to, E.squareOf('e2'));
}

// 一步杀：三档难度都得看得见
const mate1 = E.fromFen('6k1/5ppp/8/8/8/8/5PPP/R5K1 w - - 0 1');
for (const lv of [1, 2, 3]) {
    ok(`level ${lv} 找到一步杀`, E.bestMove(mate1, lv).to, E.squareOf('a8'));
}

ok('将死局面返回 null', E.bestMove(fool, 3), null);

// 开局第一步：三档都不能超时（QML 里是同步跑的，卡住就是界面卡住）
for (const lv of [1, 2, 3]) {
    const t = Date.now();
    const mv = E.bestMove(E.newGame(), lv);
    const ms = Date.now() - t;
    ok(`level ${lv} 开局能给出着法`, !!mv, true);
    ok(`level ${lv} 开局用时 ${ms}ms（上限 4000）`, ms < 4000, true);
}

// ---- 6. 自对弈：一整盘能正常下完，不会走出非法着法 ----
for (let g = 0; g < 2; g++) {
    let st = E.newGame();
    let plies = 0;
    let bad = '';
    while (plies < 240) {
        const s = E.status(st);
        if (s === 'checkmate' || s === 'stalemate' || s === 'draw50' || s === 'material') {
            break;
        }
        const mv = E.bestMove(st, 2);
        if (!mv) {
            bad = '还有棋可走却返回 null（status=' + s + '）';
            break;
        }
        const legal = E.legalMoves(st).some(m => m.from === mv.from && m.to === mv.to && m.promo === mv.promo);
        if (!legal) {
            bad = '走出了非法着法';
            break;
        }
        st = E.applyMove(st, mv);
        plies++;
    }
    ok(`自对弈第 ${g + 1} 盘全程合法`, bad, '');
    console.log(`     第 ${g + 1} 盘：${plies} 个半回合，结果 ${E.status(st)}`);
}

console.log(fail === 0 ? '\n全部通过' : `\n失败 ${fail} 项`);
process.exit(fail === 0 ? 0 : 1);
