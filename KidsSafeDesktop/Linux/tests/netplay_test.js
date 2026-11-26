/*
 * 联机对战的离线验证：起一个真的服务端，模拟两台客户端对着下。
 *
 * 跑法（在 Linux/ 目录下）：
 *     node tests/netplay_test.js
 *
 * 它验两件事，都是「不测就只能等孩子玩到一半才发现」的那种：
 *
 * 1) findMove 往返：走法在网络上只传 {f, t, p}，重放时要能换回完整的
 *    着法对象。随机游走若干步，每步都比对「原始 applyMove」和
 *    「序列化再 findMove 回来的 applyMove」结果是否完全一致 ——
 *    棋盘、轮次、易位权、吃过路兵标记，一个都不能差。这里差一点，
 *    两台机器就会走出两个局面来。
 *
 * 2) 两个客户端各自从开局重放同一串走法，最终局面必须一模一样。
 *    顺带把「不能连走两步」「假 token 读不到房间」这些边界也打一遍。
 *
 * 服务端只用到标准库，脚本也只用 node 内置模块，不装任何依赖。
 * 房间只存内存，所以测试结束不会在仓库里留下任何文件。
 */

const { spawn } = require("child_process");
const fs = require("fs");
const http = require("http");
const net = require("net");
const path = require("path");

const ROOT = path.resolve(__dirname, "..");
const ENGINE = path.join(ROOT, "src", "qml", "ChessEngine.js");
const SERVER = path.join(ROOT, "server", "kidssafe_story_server.py");
const DATA = path.join(ROOT, "server", "data");

const src = fs.readFileSync(ENGINE, "utf8").replace(/^\.pragma library\s*/, "");
const E = new Function(
    src + "\nreturn { newGame, fromFen, legalMoves, applyMove, findMove, status, inCheck };")();

let passed = 0;
let failed = 0;

function ok(name, condition, detail) {
    if (condition) {
        passed += 1;
        console.log("  PASS " + name);
    } else {
        failed += 1;
        console.log("  FAIL " + name + (detail ? "  << " + detail : ""));
    }
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function freePort() {
    return new Promise((resolve) => {
        const srv = net.createServer();
        srv.listen(0, "127.0.0.1", () => {
            const port = srv.address().port;
            srv.close(() => resolve(port));
        });
    });
}

function request(base, method, pathname, body) {
    return new Promise((resolve, reject) => {
        const data = body ? Buffer.from(JSON.stringify(body)) : null;
        const req = http.request(base + pathname, {
            method,
            headers: data
                ? { "Content-Type": "application/json", "Content-Length": data.length }
                : {},
        }, (res) => {
            let chunks = "";
            res.on("data", (c) => { chunks += c; });
            res.on("end", () => {
                let json = null;
                try {
                    json = JSON.parse(chunks);
                } catch (e) {
                    json = null;
                }
                resolve({ status: res.statusCode, body: json });
            });
        });
        req.on("error", reject);
        if (data) {
            req.write(data);
        }
        req.end();
    });
}

// ---- 1. findMove 往返 ------------------------------------------

function sameState(a, b) {
    if (a.turn !== b.turn || a.ep !== b.ep || a.half !== b.half) {
        return false;
    }
    if (a.castle.wk !== b.castle.wk || a.castle.wq !== b.castle.wq
        || a.castle.bk !== b.castle.bk || a.castle.bq !== b.castle.bq) {
        return false;
    }
    for (let i = 0; i < 64; i++) {
        if (a.board[i] !== b.board[i]) {
            return false;
        }
    }
    return true;
}

function testRoundTrip() {
    console.log("\n走法往返（网络上只传 from / to / promo）：");

    let st = E.newGame();
    let checked = 0;
    let mismatch = null;
    // 每局随机走 40 步，走 6 局：覆盖到易位、吃过路兵、升变的机会比一局多
    for (let round = 0; round < 6 && !mismatch; round++) {
        st = E.newGame();
        for (let step = 0; step < 40; step++) {
            const moves = E.legalMoves(st);
            if (moves.length === 0) {
                break;
            }
            const mv = moves[Math.floor(Math.random() * moves.length)];
            const direct = E.applyMove(st, mv);

            const raw = { f: mv.from, t: mv.to, p: mv.promo };
            const back = E.findMove(st, raw.f, raw.t, raw.p || "");
            if (!back) {
                mismatch = "findMove 找不到 " + JSON.stringify(raw);
                break;
            }
            const replayed = E.applyMove(st, back);
            if (!sameState(direct, replayed)) {
                mismatch = "重放结果不同 " + JSON.stringify(raw);
                break;
            }
            checked += 1;
            st = direct;
        }
    }
    ok("随机 " + checked + " 步往返结果一致", mismatch === null, mismatch);

    // 升变这一步最容易漏：promo 空串和 "Q" 必须能区分开
    const promoSt = E.fromFen("8/4P3/8/8/8/8/8/K6k w - - 0 1");
    const promoMove = E.findMove(promoSt, 12, 4, "Q");
    ok("升变能按 promo 找回", promoMove !== null && promoMove.promo === "Q");
    const plain = E.findMove(promoSt, 12, 4, "");
    ok("升变局面里不传 promo 找不到", plain === null);
}

// ---- 2. 两台客户端对下 -----------------------------------------

function newClient(name) {
    return {
        name,
        seat: "",
        token: "",
        code: "",
        game: E.newGame(),
        applied: 0,
        pending: null,
    };
}

// 和 QML 里 playMove 的联机分支同一套逻辑：本地先落子，再发服务器
async function localMove(base, p, mv) {
    p.game = E.applyMove(p.game, mv);
    p.pending = mv;
    const r = await request(base, "POST", "/api/game/move", {
        code: p.code, token: p.token, from: mv.from, to: mv.to, promo: mv.promo,
    });
    return r;
}

// 和 QML 里 applyRemoteMoves 同一套逻辑：只补没重放过的，认得自己那步
function syncFromRoom(p, room) {
    const moves = room.moves || [];
    while (p.applied < moves.length) {
        const raw = moves[p.applied];
        const promo = raw.p || "";
        if (p.pending !== null
            && raw.f === p.pending.from && raw.t === p.pending.to
            && promo === p.pending.promo) {
            p.pending = null;
            p.applied += 1;
            continue;
        }
        const mv = E.findMove(p.game, raw.f, raw.t, promo);
        if (!mv) {
            throw new Error("重放失败：第 " + p.applied + " 步 " + JSON.stringify(raw));
        }
        p.applied += 1;
        p.pending = null;
        p.game = E.applyMove(p.game, mv);
    }
}

async function poll(base, p) {
    const r = await request(base, "GET",
        "/api/game/state?code=" + p.code + "&token=" + p.token);
    if (r.status !== 200) {
        throw new Error("轮询失败 " + r.status + " " + JSON.stringify(r.body));
    }
    syncFromRoom(p, r.body.room);
    return r.body.room;
}

async function testChess(base) {
    console.log("\n国际象棋联机：");

    let r = await request(base, "POST", "/api/game/create", { game: "chess", level: 2 });
    const a = newClient("A");
    a.code = r.body.room.code;
    a.token = r.body.room.token;
    a.seat = "a";

    r = await request(base, "POST", "/api/game/join", { code: a.code });
    const b = newClient("B");
    b.code = a.code;
    b.token = r.body.room.token;
    b.seat = "b";
    ok("两边座位不同", a.seat !== b.seat && a.token !== b.token);

    // 白方连走两次：第二次必须被服务端挡掉
    const first = E.legalMoves(a.game)[0];
    await localMove(base, a, first);
    let room = await poll(base, a);
    syncFromRoom(b, room);
    const second = await request(base, "POST", "/api/game/move", {
        code: a.code, token: a.token,
        from: E.legalMoves(a.game)[0].from, to: E.legalMoves(a.game)[0].to, promo: "",
    });
    ok("白方不能连走两步", second.status === 409);

    // 接下来由黑方走，交替 20 步
    let drift = null;
    for (let i = 0; i < 20 && !drift; i++) {
        const mover = i % 2 === 0 ? b : a;
        const moves = E.legalMoves(mover.game);
        if (moves.length === 0) {
            break;
        }
        const mv = moves[Math.floor(Math.random() * moves.length)];
        const res = await localMove(base, mover, mv);
        if (res.status !== 200) {
            drift = "第 " + i + " 步被拒：" + JSON.stringify(res.body);
            break;
        }
        room = await poll(base, mover);
        syncFromRoom(mover === a ? b : a, room);
        if (!sameState(a.game, b.game)) {
            drift = "第 " + i + " 步之后两台机器的局面不同";
            break;
        }
    }
    ok("交替 20 步后两边局面一致", drift === null, drift);
    ok("走法序列长度对得上", room.moves.length === a.applied && room.moves.length === b.applied,
        "moves=" + room.moves.length + " a=" + a.applied + " b=" + b.applied);

    // 假 token 读不到别人的房间
    const fake = await request(base, "GET",
        "/api/game/state?code=" + a.code + "&token=nope");
    ok("假 token 读不到房间", fake.status === 403);

    // 报结果之后不能再走
    await request(base, "POST", "/api/game/finish",
        { code: a.code, token: a.token, result: "a" });
    const after = await request(base, "POST", "/api/game/move", {
        code: a.code, token: b.token, from: 8, to: 16, promo: "",
    });
    ok("结束后走不动", after.status === 409);
}

async function testKlotski(base) {
    console.log("\n华容道竞速：");

    let r = await request(base, "POST", "/api/game/create", { game: "klotski", level: 3 });
    const code = r.body.room.code;
    const ta = r.body.room.token;

    r = await request(base, "POST", "/api/game/join", { code });
    const tb = r.body.room.token;
    const room = r.body.room;

    const waitMs = (room.startAt - room.now) * 1000;
    ok("到齐后有几秒倒计时", waitMs > 2000 && waitMs < 4000, "waitMs=" + Math.round(waitMs));

    // 计时起点在倒计时结束时，所以「刚 join 就交成绩」必须被拒
    const early = await request(base, "POST", "/api/game/finish",
        { code, token: ta, steps: 51 });
    ok("倒计时里交不了成绩", early.status === 409);

    await sleep(waitMs + 300);
    r = await request(base, "POST", "/api/game/finish", { code, token: ta, steps: 51 });
    ok("先解出的人记成赢家", r.body.room.winner === "a");
    ok("还在等进行中", r.body.room.status === "playing");

    await sleep(400);
    r = await request(base, "POST", "/api/game/finish", { code, token: tb, steps: 70 });
    const done = r.body.room;
    ok("两个人都交完才结束", done.status === "done" && done.results.length === 2);
    ok("赢家不会被后来者顶掉", done.winner === "a");
    ok("后到的人用时就长一些",
        done.results[1].ms > done.results[0].ms,
        JSON.stringify(done.results));
}

// ---- 主流程 ----------------------------------------------------

async function main() {
    testRoundTrip();

    const port = await freePort();
    const base = "http://127.0.0.1:" + port;
    const python = process.env.PYTHON || "python";

    const proc = spawn(python, [SERVER, "--host", "127.0.0.1", "--port", String(port),
        "--data", DATA], { stdio: "ignore" });

    try {
        let ready = false;
        for (let i = 0; i < 40 && !ready; i++) {
            await sleep(150);
            try {
                const r = await request(base, "GET", "/api/health");
                ready = r.status === 200;
            } catch (e) {
                ready = false;
            }
        }
        if (!ready) {
            console.log("\n服务端没起来，后面的联机测试跳过。");
            failed += 1;
        } else {
            await testChess(base);
            await testKlotski(base);
        }
    } finally {
        proc.kill();
    }

    console.log("\n" + (failed === 0
        ? "全部通过（" + passed + " 项）"
        : "失败 " + failed + " 项，" + passed + " 项通过"));
    process.exit(failed === 0 ? 0 : 1);
}

main().catch((error) => {
    console.error(error);
    process.exit(1);
});
