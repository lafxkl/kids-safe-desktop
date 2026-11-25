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

注意这不是单元测试框架，只是一个「改完内容或接口后三十秒能确认没弄坏」的脚本。
不依赖任何第三方包，和服务器本体保持一致。
"""

import json
import os
import socket
import subprocess
import sys
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


def main():
    port = free_port()
    base = "http://127.0.0.1:%d" % port

    print("启动临时实例：%s" % base)
    process = subprocess.Popen(
        [sys.executable, SERVER, "--host", "127.0.0.1", "--port", str(port)],
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

        # 家长后台页面
        try:
            with urllib.request.urlopen(base + "/", timeout=5) as response:
                html = response.read().decode("utf-8")
            check("家长后台页面可打开", "故事服务端" in html and response.status == 200)
        except Exception as error:
            check("家长后台页面可打开", False, str(error))

    finally:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()

    print("\n%d 项通过，%d 项失败" % (passed, failed))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
