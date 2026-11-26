#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
故事服务端的自测：起一个临时实例，按客户端真实调用顺序打一遍接口。

跑法：
    python3 server/selftest.py

它会检查客户端 src/core/storyservice.cpp 真正依赖的字段：
    types   → categories[].id / name
    list    → items[].id / title / type / length / readTime / shortDesc、page、hasMore
    search  → 同样的结构，且确实缩小了结果
    detail  → id / title / content

以及学习情况上报（src/core/learnreport.cpp 用的那三个接口）：
    report  → 进度 / 作业都能落盘，最高分不被低分覆盖，full=true 能删空
    summary → children[].child / progress / homework / updatedAt
    reset   → 清空后 children 为空

测试会在临时目录里复制一份 data/ 再跑，不会污染仓库里的内容：
上报会产生 learn.json，那是运行时数据，不该跟着代码提交。

注意这不是单元测试框架，只是一个「改完内容或接口后三十秒能确认没弄坏」的脚本。
不依赖任何第三方包，和服务器本体保持一致。
"""

import json
import os
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
SERVER = os.path.join(HERE, "kidssafe_story_server.py")

passed = 0
failed = 0


def check(name, condition, detail=""):
    global passed, failed
    if condition:
        passed += 1
        print("  ✓ %s" % name)
    else:
        failed += 1
        print("  ✗ %s %s" % (name, detail))


def free_port():
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.bind(("127.0.0.1", 0))
    port = sock.getsockname()[1]
    sock.close()
    return port


def get(base, path, expect=200):
    try:
        with urllib.request.urlopen(base + path, timeout=5) as response:
            return response.status, json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        return error.code, None


def post(base, path, payload=None, raw=None):
    body = raw if raw is not None else json.dumps(payload or {}).encode("utf-8")
    request = urllib.request.Request(
        base + path, data=body, headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(request, timeout=5) as response:
            return response.status, json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        try:
            return error.code, json.loads(error.read().decode("utf-8"))
        except ValueError:
            return error.code, None


def main():
    port = free_port()
    base = "http://127.0.0.1:%d" % port

    # 复制一份内容目录再跑：自测会往 data/ 里写 learn.json，
    # 不能污染仓库里那份（家长可能正拿它当真实数据）。
    temp_root = tempfile.mkdtemp(prefix="kidssafe-selftest-")
    work_dir = os.path.join(temp_root, "data")
    shutil.copytree(os.path.join(HERE, "data"), work_dir)

    print("启动临时实例：%s（数据目录 %s）" % (base, work_dir))
    process = subprocess.Popen(
        [sys.executable, SERVER, "--host", "127.0.0.1", "--port", str(port),
         "--data", work_dir],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )

    try:
        # 等端口就绪，最多 8 秒
        ready = False
        for _ in range(40):
            if process.poll() is not None:
                break
            try:
                status, _ = get(base, "/api/health")
                if status == 200:
                    ready = True
                    break
            except Exception:
                pass
            time.sleep(0.2)

        if not ready:
            output = b""
            if process.poll() is not None:
                output = process.stdout.read() if process.stdout else b""
            print("服务没起来：\n%s" % output.decode("utf-8", "replace"))
            return 1

        print("\n接口契约：")

        status, health = get(base, "/api/health")
        check("health 可用", status == 200 and health.get("ok") is True)
        check("health 有篇数", isinstance(health.get("stories"), int) and health["stories"] > 0,
              "stories=%r" % (health.get("stories"),))

        status, types = get(base, "/api/story/types")
        categories = (types or {}).get("categories") or []
        check("types 返回分类", status == 200 and len(categories) > 0)
        check("分类字段完整",
              all(isinstance(c.get("id"), int) and c.get("name") for c in categories))
        first_id = categories[0]["id"] if categories else 1

        status, listing = get(base, "/api/story/list?typeId=%d&page=1" % first_id)
        items = (listing or {}).get("items") or []
        check("list 返回条目", status == 200 and len(items) > 0)
        check("list 分页字段",
              isinstance((listing or {}).get("page"), int)
              and isinstance((listing or {}).get("hasMore"), bool))
        required = ("id", "title", "type", "length", "readTime", "shortDesc")
        check("条目字段完整",
              all(all(key in item for key in required) for item in items),
              str(items[0] if items else None))
        check("条目都属于该分类",
              all(item["type"] == categories[0]["name"] for item in items))

        story_id = items[0]["id"] if items else 0
        status, detail = get(base, "/api/story/detail?storyId=%d" % story_id)
        check("detail 返回正文",
              status == 200 and bool((detail or {}).get("content")), str(status))
        check("detail 字数与列表一致",
              (detail or {}).get("length") == items[0]["length"] if items else False)

        keyword = items[0]["title"][:2] if items else "月"
        status, search = get(base, "/api/story/search?keyword=%s&page=1" %
                             urllib.parse.quote(keyword))
        found = (search or {}).get("items") or []
        check("search 有结果", status == 200 and len(found) > 0, "keyword=%s" % keyword)
        check("search 结果更收敛", len(found) <= len(items))

        status, _ = get(base, "/api/story/detail?storyId=999999")
        check("不存在的故事返回 404", status == 404)

        status, _ = get(base, "/api/story/list?typeId=9999&page=1")
        check("不存在的分类返回空列表而不是报错", status == 200)

        status, page2 = get(base, "/api/story/list?typeId=0&page=2")
        check("第二页可访问", status == 200 and (page2 or {}).get("page") == 2)

        # 学习情况上报（客户端 core/learnreport.cpp 的接口）
        print("\n学习情况上报：")

        status, summary = get(base, "/api/learn/summary")
        check("初始没有记录", status == 200 and summary.get("children") == [])

        report = {
            "child": "默认",
            "source": "linux",
            "device": "selftest",
            "progress": {
                "done": 3, "total": 26,
                "passed": ["q-order-1", "q-order-2", "q-order-3"],
                "chapters": [{"name": "第一章 · 顺序", "done": 3, "total": 4},
                             {"name": "第二章 · 重复", "done": 0, "total": 5}],
            },
            "homework": {"hw-order": {"score": 4, "total": 5, "best": 4,
                                      "title": "第 1 课 · 顺序与重复",
                                      "at": "2026-11-26T13:40:30"}},
        }
        status, result = post(base, "/api/learn/report", report)
        check("上报成功", status == 200 and (result or {}).get("ok") is True, str(result))
        check("返回孩子名", (result or {}).get("child") == "默认")

        status, summary = get(base, "/api/learn/summary")
        kid = ((summary or {}).get("children") or [{}])[0]
        progress = kid.get("progress") or {}
        check("汇总里有进度", progress.get("done") == 3 and progress.get("total") == 26)
        check("章节进度完整",
              all("name" in c and "done" in c and "total" in c
                  for c in (progress.get("chapters") or [])))
        check("汇总里有作业", (kid.get("homework") or {}).get("hw-order", {}).get("best") == 4)

        # 再交一次、分数更低：最高分要留住，最近一次分数要更新
        post(base, "/api/learn/report",
             {"homework": {"hw-order": {"score": 2, "total": 5, "title": "第 1 课 · 顺序与重复"}}})
        status, summary = get(base, "/api/learn/summary")
        item = ((summary or {}).get("children") or [{}])[0].get("homework", {}).get("hw-order", {})
        check("最高分保留", item.get("best") == 4 and item.get("score") == 2, str(item))

        status, _ = post(base, "/api/learn/report", raw=b"{oops")
        check("坏 JSON 返回 400", status == 400)

        # 家长在孩子端点「清空成绩」后，客户端会用 full=true 再报一次空表
        post(base, "/api/learn/report", {"full": True, "homework": {}})
        status, summary = get(base, "/api/learn/summary")
        check("full=true 能删掉已清空的作业",
              "hw-order" not in (((summary or {}).get("children") or [{}])[0]
                                 .get("homework") or {}))

        status, result = post(base, "/api/learn/report",
                              {"homework": {"bad": {"score": 9, "total": 0}}})
        check("total=0 的作业被忽略", status == 200 and (result or {}).get("ok") is True)
        status, summary = get(base, "/api/learn/summary")
        check("坏作业没进记录",
              "bad" not in (((summary or {}).get("children") or [{}])[0].get("homework") or {}))

        status, health = get(base, "/api/health")
        check("health 带学习上报数", isinstance((health or {}).get("learnChildren"), int))

        status, result = post(base, "/api/learn/reset", {"child": "默认"})
        check("清空成功", status == 200 and (result or {}).get("children") == 0)
        status, summary = get(base, "/api/learn/summary")
        check("清空后没有记录", (summary or {}).get("children") == [])

        # 家长后台页面
        try:
            with urllib.request.urlopen(base + "/", timeout=5) as response:
                html = response.read().decode("utf-8")
            check("家长后台页面可打开", "故事服务端" in html and response.status == 200)
            check("页面上有学习情况", "学习情况" in html and "/api/learn/summary" in html)
        except Exception as error:
            check("家长后台页面可打开", False, str(error))

        # ── 联机对战 ──────────────────────────────────────────
        # 两个「人」用两套 token 打同一个房间，顺序和孩子端 Netplay 完全一致。
        print("\n联机对战：")

        status, result = post(base, "/api/game/create", {"game": "chess", "level": 1})
        room = (result or {}).get("room") or {}
        code = room.get("code") or ""
        a_token = room.get("token") or ""
        check("建房成功", status == 200 and (result or {}).get("ok") is True)
        check("房间号是 4 位数字", len(code) == 4 and code.isdigit())
        check("房主坐 a 座、等人中", room.get("seat") == "a" and room.get("status") == "waiting")
        check("房主拿到 token", len(a_token) >= 16)
        check("房间视图里不含座位表", "seats" not in room)

        status, _ = post(base, "/api/game/move",
                         {"code": code, "token": a_token, "from": 52, "to": 44, "promo": ""})
        check("对手没进来不能走子", status == 409)

        status, _ = post(base, "/api/game/join", {"code": "0000"})
        check("不存在的房间进不去", status == 404)

        status, result = post(base, "/api/game/join", {"code": code})
        joined = (result or {}).get("room") or {}
        b_token = joined.get("token") or ""
        check("对手进房坐 b 座", joined.get("seat") == "b" and len(b_token) >= 16)
        check("两个人 token 不同", b_token != a_token)
        check("两人到齐自动开局", joined.get("status") == "playing")
        check("能看到对手已经到了", joined.get("opponent") is True)

        status, _ = post(base, "/api/game/join", {"code": code})
        check("第三个人进不去", status == 409)

        status, result = post(base, "/api/game/move",
                              {"code": code, "token": a_token, "from": 52, "to": 44, "promo": ""})
        check("轮到白方就能走", status == 200)
        check("走完轮到黑方", ((result or {}).get("room") or {}).get("turn") == "b")

        status, _ = post(base, "/api/game/move",
                         {"code": code, "token": a_token, "from": 62, "to": 45, "promo": "N"})
        check("不能连走两步", status == 409)

        status, _ = get(base, "/api/game/state?code=%s&token=deadbeef" % code)
        check("假 token 读不到房间", status == 403)

        status, _ = post(base, "/api/game/move",
                         {"code": code, "token": b_token, "from": 5, "to": 5, "promo": ""})
        check("不能原地不动", status == 400)

        status, _ = post(base, "/api/game/move",
                         {"code": code, "token": b_token, "from": 12, "to": 28, "promo": "K"})
        check("乱填升变被挡住", status == 400)

        status, result = post(base, "/api/game/move",
                              {"code": code, "token": b_token, "from": 12, "to": 28, "promo": ""})
        check("黑方走子成功",
              status == 200 and len(((result or {}).get("room") or {}).get("moves") or []) == 2)

        status, result = get(base, "/api/game/state?code=%s&token=%s" % (code, b_token))
        room = (result or {}).get("room") or {}
        check("轮询拿到完整走法", len(room.get("moves") or []) == 2)
        check("轮询回的是自己的座位", room.get("seat") == "b" and room.get("turn") == "a")

        status, result = post(base, "/api/game/finish",
                              {"code": code, "token": a_token, "result": "a"})
        room = (result or {}).get("room") or {}
        check("象棋报结果", room.get("winner") == "a" and room.get("status") == "done")

        status, _ = post(base, "/api/game/move",
                         {"code": code, "token": a_token, "from": 51, "to": 35, "promo": ""})
        check("结束后不能再走", status == 409)

        # 华容道：同一道题、同一时刻开始、比谁先解出来
        status, result = post(base, "/api/game/create", {"game": "klotski", "level": 3})
        room = (result or {}).get("room") or {}
        k_code = room.get("code") or ""
        ka_token = room.get("token") or ""
        check("华容道建房带难度", room.get("game") == "klotski" and room.get("level") == 3)

        status, result = post(base, "/api/game/join", {"code": k_code})
        kb_token = ((result or {}).get("room") or {}).get("token") or ""
        check("华容道两人到齐", ((result or {}).get("room") or {}).get("status") == "playing")

        status, _ = post(base, "/api/game/move",
                         {"code": k_code, "token": ka_token, "from": 1, "to": 2, "promo": ""})
        check("华容道房间不接受走子", status == 409)

        status, _ = post(base, "/api/game/finish",
                         {"code": k_code, "token": ka_token, "steps": 20})
        check("倒计时没结束不能交成绩", status == 409)

        time.sleep(3.3)
        status, result = post(base, "/api/game/finish",
                              {"code": k_code, "token": ka_token, "steps": 27})
        room = (result or {}).get("room") or {}
        first = (room.get("results") or [{}])[0]
        check("先解出的人获胜", room.get("winner") == "a")
        check("成绩里记了步数", first.get("steps") == 27)
        # 等了 3.3 秒，但其中 3 秒是倒计时，只有多出来的那点算进成绩：
        # 断言「小于 2 秒」正好证明计时起点是服务器的 startAt，不是请求到达时刻
        check("耗时从倒计时结束时开始算", 0 <= first.get("ms", -1) < 2000)

        status, result = post(base, "/api/game/finish",
                              {"code": k_code, "token": kb_token, "steps": 40})
        room = (result or {}).get("room") or {}
        check("后到的人也能交成绩", len(room.get("results") or []) == 2)
        check("冠军不会被后来者顶掉", room.get("winner") == "a")

        status, _ = post(base, "/api/game/finish",
                         {"code": k_code, "token": kb_token, "steps": 40})
        check("同一个人不能重复交", status == 409)

        status, _ = post(base, "/api/game/leave", {"code": k_code, "token": ka_token})
        check("房主退房", status == 200)
        status, _ = get(base, "/api/game/state?code=%s&token=%s" % (k_code, kb_token))
        check("房主走了房间就拆掉", status == 404)

        status, health = get(base, "/api/health")
        check("health 带房间数", isinstance((health or {}).get("rooms"), dict))

    finally:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
        shutil.rmtree(temp_root, ignore_errors=True)

    print("\n%d 项通过，%d 项失败" % (passed, failed))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
