#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
儿童安全桌面 · Linux 故事服务端

给 Linux 版（Qt / Kirigami 前端）用的最小内容服务。只用 Python 3 标准库，
不装任何第三方包、不建数据库，`python3 kidssafe_story_server.py` 就能跑。

接口与客户端 src/core/storyservice.{h,cpp} 的契约严格一致：

    GET /api/story/types                      → {"categories": [{"id", "name"}]}
    GET /api/story/list?typeId=&page=         → {"items": [...], "page", "hasMore", "total"}
    GET /api/story/search?keyword=&page=      → 同上
    GET /api/story/detail?storyId=            → {id, title, type, length, readTime, content}
    GET /api/health                           → {"ok": true, ...}
    POST /api/reload                          → 重新读盘（家长改完内容不用重启）

额外还带一个只读的家长后台网页（`/`）：看分类、搜故事、读正文、确认服务在跑。

内容怎么加：
  1. 改 data/stories.json（推荐，字段见文件头注释）；
  2. 或者往 data/stories/<分类名>/<标题>.txt 丢文本文件，启动/请求时自动并入，
     适合家长把现成的故事文本直接放进来。

设计上沿用原工程的一条铁律：内容服务永远是「可选增强」。它挂了、地址填错了，
客户端只是退回「家长还没开启在线故事」，不影响锁定与计时。
"""

import argparse
import json
import os
import re
import socket
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, unquote, urlparse

VERSION = "1.0.0"
PAGE_SIZE = 20
CHARS_PER_MINUTE = 300


# ── 内容库 ──────────────────────────────────────────────────────


class Library(object):
    """把 stories.json 与 data/stories/ 下的 txt 合并成一份可查询的内存索引。

    json 在每次请求时按 mtime 判断是否重新加载，家长改完文件刷新页面即可，
    不用重启服务（systemd 用户服务重启一次并不麻烦，但家长不一定知道）。
    """

    def __init__(self, data_dir):
        self.data_dir = data_dir
        self.json_path = os.path.join(data_dir, "stories.json")
        self.txt_root = os.path.join(data_dir, "stories")
        # 用可重入锁：maybe_reload() 持锁后会调 load()，而 load() 自己也要持锁。
        # 普通 Lock 在这里会直接自锁死，服务表现为「第一个请求就卡住」。
        self._lock = threading.RLock()
        self._stamp = None
        self.categories = []
        self.stories = []
        self.source_errors = []
        self.load()

    # -- 加载 ----------------------------------------------------

    def _stamp_now(self):
        """用文件 mtime + 文件数当版本戳，避免每次请求都重新解析 JSON。"""
        parts = []
        for path in (self.json_path, self.txt_root):
            try:
                parts.append("%s:%d" % (path, os.path.getmtime(path)))
            except OSError:
                parts.append("%s:-" % path)
        try:
            parts.append(str(len(os.listdir(self.txt_root))))
        except OSError:
            pass
        return "|".join(parts)

    def maybe_reload(self):
        stamp = self._stamp_now()
        if stamp != self._stamp:
            with self._lock:
                if stamp != self._stamp:
                    self.load()

    def load(self):
        with self._lock:
            self._stamp = self._stamp_now()
            self.source_errors = []
            raw_categories = []
            raw_stories = []

            if os.path.exists(self.json_path):
                try:
                    with open(self.json_path, "r", encoding="utf-8") as handle:
                        raw = json.load(handle)
                    raw_categories = raw.get("categories") or []
                    raw_stories = raw.get("stories") or []
                except (ValueError, OSError) as error:
                    # 内容文件坏了不能让服务起不来：记一条错误，继续用 txt 兜底
                    self.source_errors.append("stories.json 解析失败：%s" % error)

            categories = []
            seen_ids = set()
            for index, item in enumerate(raw_categories):
                if not isinstance(item, dict):
                    continue
                name = str(item.get("name") or "").strip()
                if not name:
                    continue
                try:
                    cid = int(item.get("id"))
                except (TypeError, ValueError):
                    cid = index + 1
                if cid in seen_ids:
                    cid = max(seen_ids) + 1
                seen_ids.add(cid)
                categories.append({"id": cid, "name": name})

            name_to_id = {c["name"]: c["id"] for c in categories}
            next_id = max(seen_ids) + 1 if seen_ids else 1

            stories = []
            used_story_ids = set()

            for index, item in enumerate(raw_stories):
                if not isinstance(item, dict):
                    continue
                content = str(item.get("content") or "").strip()
                title = str(item.get("title") or "").strip()
                if not content or not title:
                    continue

                type_name = str(item.get("type") or "").strip()
                type_id = item.get("typeId")
                try:
                    type_id = int(type_id) if type_id is not None else None
                except (TypeError, ValueError):
                    type_id = None
                if type_id is None:
                    type_id = name_to_id.get(type_name, 0)
                if type_name and type_name not in name_to_id:
                    # json 里出现了未登记的分类名：补一个，避免故事"没有分类"
                    name_to_id[type_name] = next_id
                    categories.append({"id": next_id, "name": type_name})
                    next_id += 1
                    type_id = name_to_id[type_name]
                if not type_name:
                    type_name = self._category_name(categories, type_id)

                try:
                    sid = int(item.get("id"))
                except (TypeError, ValueError):
                    sid = 900000 + index
                if sid in used_story_ids:
                    sid = max(used_story_ids) + 1
                used_story_ids.add(sid)

                stories.append(
                    self._build(sid, type_id, type_name, title,
                                str(item.get("shortDesc") or ""), content)
                )

            # 目录里的 txt：data/stories/<分类名>/<标题>.txt
            for type_name, title, content in self._scan_txt(categories):
                if type_name not in name_to_id:
                    name_to_id[type_name] = next_id
                    categories.append({"id": next_id, "name": type_name})
                    next_id += 1
                sid = 900000 + len(stories)
                while sid in used_story_ids:
                    sid += 1
                used_story_ids.add(sid)
                stories.append(
                    self._build(sid, name_to_id[type_name], type_name, title, "", content)
                )

            if not categories:
                categories = [{"id": 1, "name": "故事"}]

            self.categories = categories
            self.stories = stories

    @staticmethod
    def _category_name(categories, type_id):
        for item in categories:
            if item["id"] == type_id:
                return item["name"]
        return "故事"

    @staticmethod
    def _build(sid, type_id, type_name, title, short_desc, content):
        plain = re.sub(r"\s+", "", content)
        length = len(plain)
        minutes = max(1, int(round(float(length) / CHARS_PER_MINUTE)))
        if not short_desc:
            short_desc = plain[:48] + ("…" if len(plain) > 48 else "")
        return {
            "id": sid,
            "typeId": type_id,
            "type": type_name,
            "title": title,
            "shortDesc": short_desc,
            "content": content,
            "length": length,
            "readTime": "约 %d 分钟" % minutes,
        }

    def _scan_txt(self, categories):
        """扫描 data/stories/<分类>/<标题>.txt，产出 (分类名, 标题, 正文)。"""
        result = []
        if not os.path.isdir(self.txt_root):
            return result

        for entry in sorted(os.listdir(self.txt_root)):
            folder = os.path.join(self.txt_root, entry)
            if os.path.isdir(folder):
                category = entry
                files = sorted(os.listdir(folder))
                for name in files:
                    if not name.lower().endswith(".txt"):
                        continue
                    path = os.path.join(folder, name)
                    body = self._read_text(path)
                    if body:
                        result.append((category, os.path.splitext(name)[0], body))
            elif entry.lower().endswith(".txt"):
                # 直接丢在根目录的 txt 归到第一个分类
                body = self._read_text(folder)
                if body:
                    category = categories[0]["name"] if categories else "故事"
                    result.append((category, os.path.splitext(entry)[0], body))
        return result

    @staticmethod
    def _read_text(path):
        try:
            with open(path, "r", encoding="utf-8") as handle:
                return handle.read().strip()
        except (OSError, UnicodeDecodeError):
            return ""

    # -- 查询 ----------------------------------------------------

    def _summary(self, story):
        return {
            "id": story["id"],
            "title": story["title"],
            "type": story["type"],
            "length": story["length"],
            "readTime": story["readTime"],
            "shortDesc": story["shortDesc"],
        }

    def _page(self, items, page):
        page = max(1, page)
        start = (page - 1) * PAGE_SIZE
        chunk = items[start:start + PAGE_SIZE]
        return {
            "items": [self._summary(s) for s in chunk],
            "page": page,
            "hasMore": start + PAGE_SIZE < len(items),
            "total": len(items),
        }

    def types(self):
        return {"categories": [dict(c) for c in self.categories]}

    def list(self, type_id, page):
        if type_id <= 0:
            items = list(self.stories)
        else:
            items = [s for s in self.stories if s["typeId"] == type_id]
        return self._page(items, page)

    def search(self, keyword, page):
        key = keyword.strip().lower()
        if not key:
            return self._page([], page)
        items = [
            s for s in self.stories
            if key in s["title"].lower()
            or key in s["type"].lower()
            or key in s["shortDesc"].lower()
            or key in s["content"].lower()
        ]
        return self._page(items, page)

    def detail(self, story_id):
        for story in self.stories:
            if story["id"] == story_id:
                return {
                    "id": story["id"],
                    "title": story["title"],
                    "type": story["type"],
                    "length": story["length"],
                    "readTime": story["readTime"],
                    "content": story["content"],
                }
        return None

    def stats(self):
        per_type = []
        for category in self.categories:
            count = len([s for s in self.stories if s["typeId"] == category["id"]])
            per_type.append({"id": category["id"], "name": category["name"], "count": count})
        return {
            "ok": True,
            "version": VERSION,
            "stories": len(self.stories),
            "categories": per_type,
            "errors": self.source_errors,
        }


# ── HTTP 层 ─────────────────────────────────────────────────────


class Handler(BaseHTTPRequestHandler):
    server_version = "KidsSafeStory/" + VERSION
    protocol_version = "HTTP/1.1"

    library = None
    web_dir = None

    # 让 Ctrl+C 时终端干净一点，不要把每条请求都打出来
    def log_message(self, fmt, *args):
        if os.environ.get("KIDSSAFE_STORY_VERBOSE") == "1":
            sys.stderr.write("[%s] %s\n" % (time.strftime("%H:%M:%S"), fmt % args))

    # -- 工具 ----------------------------------------------------

    def _json(self, payload, status=200):
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _file(self, path, content_type):
        try:
            with open(path, "rb") as handle:
                body = handle.read()
        except OSError:
            self._json({"ok": False, "error": "not found"}, 404)
            return
        self.send_response(200)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _int(self, query, name, default=0):
        try:
            return int(query.get(name, [str(default)])[0])
        except (TypeError, ValueError):
            return default

    # -- 路由 ----------------------------------------------------

    def do_GET(self):
        parsed = urlparse(self.path)
        path = unquote(parsed.path)
        query = parse_qs(parsed.query)

        lib = self.library
        lib.maybe_reload()

        try:
            if path in ("/", "/index.html"):
                self._file(os.path.join(self.web_dir, "index.html"),
                           "text/html; charset=utf-8")
                return

            if path == "/api/health":
                self._json(lib.stats())
                return

            if path == "/api/story/types":
                self._json(lib.types())
                return

            if path == "/api/story/list":
                type_id = self._int(query, "typeId", 0)
                page = self._int(query, "page", 1)
                self._json(lib.list(type_id, page))
                return

            if path == "/api/story/search":
                keyword = query.get("keyword", [""])[0]
                page = self._int(query, "page", 1)
                self._json(lib.search(keyword, page))
                return

            if path == "/api/story/detail":
                story_id = self._int(query, "storyId", -1)
                detail = lib.detail(story_id)
                if detail is None:
                    self._json({"ok": False, "error": "story not found"}, 404)
                else:
                    self._json(detail)
                return

            self._json({"ok": False, "error": "unknown path"}, 404)
        except BrokenPipeError:
            pass

    def do_POST(self):
        parsed = urlparse(self.path)
        if parsed.path == "/api/reload":
            length = int(self.headers.get("Content-Length") or 0)
            if length:
                self.rfile.read(length)
            self.library.load()
            self._json({"ok": True, "stats": self.library.stats()})
            return
        self._json({"ok": False, "error": "unknown path"}, 404)


def local_ips():
    """列出本机局域网地址，虚拟网卡排到后面。孩子端要填的是其中一个。"""
    ips = []
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.settimeout(0.3)
        sock.connect(("223.5.5.5", 80))
        ips.append(sock.getsockname()[0])
        sock.close()
    except OSError:
        pass

    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            ip = info[4][0]
            if ip not in ips:
                ips.append(ip)
    except OSError:
        pass

    def rank(ip):
        if ip.startswith("192.168."):
            return 0
        if ip.startswith("10."):
            return 1
        if re.match(r"^172\.(1[6-9]|2\d|3[01])\.", ip):
            return 3
        if ip.startswith("169.254.") or ip.startswith("127."):
            return 9
        return 2

    return sorted([ip for ip in ips if not ip.startswith("127.")], key=rank)


def main(argv=None):
    parser = argparse.ArgumentParser(description="儿童安全桌面 · Linux 故事服务端")
    parser.add_argument("--host", default="0.0.0.0", help="监听地址，默认 0.0.0.0（局域网可访问）")
    parser.add_argument("--port", type=int, default=8080, help="监听端口，默认 8080")
    parser.add_argument("--data", default=None, help="内容目录，默认脚本同级的 data/")
    args = parser.parse_args(argv)

    here = os.path.dirname(os.path.abspath(__file__))
    data_dir = args.data or os.path.join(here, "data")
    web_dir = os.path.join(here, "web")

    if not os.path.isdir(data_dir):
        sys.stderr.write("内容目录不存在：%s\n" % data_dir)
        return 2

    library = Library(data_dir)
    Handler.library = library
    Handler.web_dir = web_dir

    try:
        httpd = ThreadingHTTPServer((args.host, args.port), Handler)
    except OSError as error:
        sys.stderr.write("端口 %d 起不来：%s\n" % (args.port, error))
        sys.stderr.write("换个端口试试：--port 8081\n")
        return 2

    httpd.daemon_threads = True

    print("儿童安全桌面 · 故事服务端 v%s" % VERSION)
    print("内容：%d 篇 / %d 类" % (len(library.stories), len(library.categories)))
    for error in library.source_errors:
        print("  注意：%s" % error)
    print("家长后台：http://127.0.0.1:%d/" % args.port)
    for ip in local_ips():
        print("孩子端可填：http://%s:%d" % (ip, args.port))
    print("按 Ctrl+C 停止。")

    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n已停止。")
    finally:
        httpd.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
