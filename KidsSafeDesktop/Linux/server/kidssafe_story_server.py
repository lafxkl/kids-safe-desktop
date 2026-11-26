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

学习情况上报（客户端 src/core/learnreport.{h,cpp}）：

    POST /api/learn/report                    → 孩子端上报关卡进度 + 作业成绩
    GET  /api/learn/summary                   → 家长后台网页读汇总
    POST /api/learn/reset                     → 清空某个（或全部）孩子的记录

联机对战（客户端 src/core/netplay.{h,cpp}）：

    POST /api/game/create   {game, level}     → 建房，回 4 位房间号 + 自己的 token
    POST /api/game/join     {code, token?}    → 进房；带上自己的 token 就是重连
    GET  /api/game/state?code=&token=         → 轮询整个房间（1.5 秒一次）
    POST /api/game/move     {code, token, from, to, promo}
    POST /api/game/finish   {code, token, result}  国际象棋：报结果 a/b/draw
                            {code, token, steps}   华容道：报「我解出来了」
    POST /api/game/leave    {code, token}     → 退房

    没有公开匹配、没有房间列表、没有聊天：只能凭 4 位房间号进熟人开的房间。
    这是有意的 —— 孩子不该在这台机器上遇到陌生人。

额外还带一个只读的家长后台网页（`/`）：看分类、搜故事、读正文、确认服务在跑，
以及看孩子端上报上来的学习情况。

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
import secrets
import socket
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, unquote, urlparse

VERSION = "1.2.0"
PAGE_SIZE = 20
CHARS_PER_MINUTE = 300

# 上报接口一次最多收 256 KB：正常一份学习情况只有几 KB，
# 限一下是为了别让人拿这个端口当上传盘用。
LEARN_MAX_BODY = 256 * 1024
LEARN_MAX_CHILDREN = 20


def now_text():
    return time.strftime("%Y-%m-%dT%H:%M:%S")


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


# ── 学习情况 ────────────────────────────────────────────────────


class LearnStore(object):
    """孩子端上报的关卡进度与作业成绩，落盘成 data/learn.json。

    为什么不写进 stories.json：那个文件是家长手改的，这个文件是程序写的，
    混在一起迟早被家长一次「另存为」覆盖掉。两个文件，互不干扰。
    写入走「临时文件 + os.replace」，断电最多丢最后一次，不会留下半个 json。

    合并规则（重要，客户端依赖它）：
      * 作业：`best` 取历史最高，`score`/`at` 取最新一次。孩子重新做一遍
        分数变低时，家长看到的仍然是「最高 4/5」，而不是被最新的 2/5 盖掉。
      * 进度：整份快照替换。客户端每次都带全量 `passed`，以最后一次为准；
        不做并集是因为孩子点「重置进度」后并集会让旧记录永远删不掉。
    """

    def __init__(self, data_dir):
        self.path = os.path.join(data_dir, "learn.json")
        self._lock = threading.Lock()
        self.children = {}
        self.error = ""
        self.load()

    # -- 工具 ----------------------------------------------------

    @staticmethod
    def _clean(value, limit):
        """去掉控制字符并截断：这些字符串最后会进 HTML 和文件名。"""
        text = str(value or "")
        text = re.sub(r"[\x00-\x1f\x7f]", "", text).strip()
        return text[:limit]

    @staticmethod
    def _as_int(value, default=0):
        try:
            return int(value)
        except (TypeError, ValueError):
            return default

    # -- 读写 ----------------------------------------------------

    def load(self):
        with self._lock:
            self.error = ""
            self.children = {}
            if not os.path.exists(self.path):
                return
            try:
                with open(self.path, "r", encoding="utf-8") as handle:
                    raw = json.load(handle)
            except (ValueError, OSError) as error:
                # 文件坏了不能让服务起不来：记一条错误，家长后台会把它显示出来
                self.error = "learn.json 解析失败：%s" % error
                return

            children = raw.get("children") if isinstance(raw, dict) else None
            if not isinstance(children, dict):
                return

            cleaned = {}
            for name, record in children.items():
                if not isinstance(record, dict):
                    continue
                child = self._clean(name, 32)
                if not child:
                    continue
                homework = {}
                raw_homework = record.get("homework")
                if isinstance(raw_homework, dict):
                    for key, value in raw_homework.items():
                        if isinstance(value, dict):
                            homework[self._clean(key, 64)] = value
                progress = record.get("progress")
                cleaned[child] = {
                    "homework": homework,
                    "progress": progress if isinstance(progress, dict) else None,
                    "updatedAt": self._clean(record.get("updatedAt"), 32),
                    "reports": self._as_int(record.get("reports")),
                }
            self.children = cleaned

    def _save_locked(self):
        payload = {"version": 1, "children": self.children}
        tmp = self.path + ".tmp"
        try:
            with open(tmp, "w", encoding="utf-8") as handle:
                json.dump(payload, handle, ensure_ascii=False, indent=2)
                handle.write("\n")
            os.replace(tmp, self.path)
        except OSError as error:
            self.error = "写入 learn.json 失败：%s" % error

    # -- 写入 ----------------------------------------------------

    def report(self, payload):
        child = self._clean(payload.get("child"), 32) or "默认"

        with self._lock:
            if child not in self.children and len(self.children) >= LEARN_MAX_CHILDREN:
                return {"ok": False, "error": "孩子数量超过 %d 个" % LEARN_MAX_CHILDREN}

            record = self.children.setdefault(
                child, {"homework": {}, "progress": None, "updatedAt": "", "reports": 0})

            # 作业成绩
            homework = payload.get("homework")
            if isinstance(homework, dict):
                kept = set()
                for key, value in homework.items():
                    lesson_id = self._clean(key, 64)
                    if not lesson_id or not isinstance(value, dict):
                        continue
                    total = self._as_int(value.get("total"))
                    if total <= 0 or total > 1000:
                        continue    # 0/0 这种只会把统计弄乱
                    score = max(0, min(total, self._as_int(value.get("score"))))
                    best = max(0, min(total, self._as_int(value.get("best"), score)))
                    at = self._clean(value.get("at"), 32)
                    title = self._clean(value.get("title"), 48)
                    old = record["homework"].get(lesson_id) or {}
                    old_best = self._as_int(old.get("best"), -1)
                    record["homework"][lesson_id] = {
                        "title": title or self._clean(old.get("title"), 48),
                        "score": score,
                        "total": total,
                        "best": max(best, old_best) if old_best >= 0 else best,
                        "at": at or self._clean(old.get("at"), 32),
                    }
                    kept.add(lesson_id)
                # full=true 表示「这是孩子端本机的全量」：没带上的作业说明
                # 本机已经清掉了（家长点了「清空成绩」），服务端也要跟着删。
                if payload.get("full") is True:
                    for stale in list(record["homework"].keys()):
                        if stale not in kept:
                            del record["homework"][stale]

            # 关卡进度（整份快照）
            progress = payload.get("progress")
            if isinstance(progress, dict):
                record["progress"] = self._clean_progress(progress)

            record["updatedAt"] = now_text()
            record["reports"] = self._as_int(record.get("reports")) + 1
            self._save_locked()

            return {
                "ok": True,
                "child": child,
                "homework": len(record["homework"]),
                "progress": bool(record["progress"]),
                "reports": record["reports"],
            }

    def _clean_progress(self, progress):
        total = max(0, self._as_int(progress.get("total")))
        done = max(0, self._as_int(progress.get("done")))
        if total:
            done = min(done, total)

        passed = []
        for item in (progress.get("passed") or [])[:500]:
            text = self._clean(item, 64)
            if text:
                passed.append(text)

        chapters = []
        for item in (progress.get("chapters") or [])[:40]:
            if not isinstance(item, dict):
                continue
            name = self._clean(item.get("name"), 48)
            if not name:
                continue
            chapter_total = max(0, self._as_int(item.get("total")))
            chapter_done = max(0, self._as_int(item.get("done")))
            if chapter_total:
                chapter_done = min(chapter_done, chapter_total)
            chapters.append({"name": name, "done": chapter_done, "total": chapter_total})

        return {"done": done, "total": total, "passed": passed,
                "chapters": chapters, "at": now_text()}

    def reset(self, child):
        with self._lock:
            if child:
                self.children.pop(child, None)
            else:
                self.children.clear()
            self._save_locked()
        return {"ok": True, "children": len(self.children)}

    # -- 读取 ----------------------------------------------------

    def summary(self):
        with self._lock:
            children = []
            for name, record in self.children.items():
                children.append({
                    "child": name,
                    "homework": record.get("homework") or {},
                    "progress": record.get("progress"),
                    "updatedAt": record.get("updatedAt") or "",
                    "reports": self._as_int(record.get("reports")),
                })
            # 最近上报的排在前面：家长打开页面先看到的应该是今天那一份
            children.sort(key=lambda item: item.get("updatedAt") or "", reverse=True)
            return {"ok": True, "children": children,
                    "count": len(children), "error": self.error}


# ── 联机对战 ─────────────────────────────────────────────────────

# 房间只活在内存里：对战是实时状态，服务重启后旧棋局已经没人在等，落盘只会
# 让下次启动读到一堆僵尸房间。30 分钟没请求就回收，够孩子中途去吃顿饭。
ROOM_TTL_SEC = 30 * 60
ROOM_MAX = 64
GAME_KINDS = ("chess", "klotski")
GAME_COUNTDOWN_SEC = 3.0   # 华容道：两人到齐后留 3 秒看清棋盘
GAME_MAX_MOVES = 600       # 一盘棋理论上不会超过 300 多步，留点余量


class RoomError(Exception):
    """业务错误，路由层直接翻成 {"ok": false, "error": ...} + 状态码。"""

    def __init__(self, message, status=400):
        Exception.__init__(self, message)
        self.status = status


class RoomStore(object):
    """两个孩子对战的房间表。

    为什么房间存的是「一串走法」而不是「当前棋盘快照」：
      * 快照要求双方对「当前局面」达成一致，一旦有一方的包晚到或重发，
        两边就会各走各的，而且没有权威版本来纠正；
      * 走法序列天然可重放：两个客户端各自从开局走一遍，局面必然一致，
        谁落后了只要补上缺的那几步就行（客户端就是按 moves 长度做增量的）；
      * 客户端本来就有 ChessEngine.legalMoves / applyMove，重放不需要新代码。

    服务端**不校验走子是否合规**，只校验「轮到你了吗」。原因：在 Python 里
    再实现一套走子规则等于维护两份规则，两份迟早不一致，而这一版服务端没有
    可复用的引擎。代价是孩子理论上能改客户端走假棋 —— 这个风险可以接受：
    赢一盘棋的收益，远小于「必须先注册账号才能玩」把他挡在门外的代价。
    真要防，正确做法是把 ChessEngine.js 原样搬到服务端用 node 跑（同一份
    规则），而不是重写一遍。

    身份用随机 token 而不是座位号：座位号是自报家门的协议，两个孩子互相知道
    对方是 a 还是 b，随手就能冒充对面落子。

    刻意没有聊天：孩子之间不需要、家长也不想要一个能发任意文字给陌生人的框。
    房间里只传走法、步数和时间，一个字都传不了。
    """

    def __init__(self):
        self._lock = threading.Lock()
        self.rooms = {}

    # -- 工具 ----------------------------------------------------

    @staticmethod
    def _make_code(rooms):
        """4 位数字：孩子要能照着念给电话那头的人听。"""
        for _ in range(200):
            code = str(secrets.randbelow(9000) + 1000)
            if code not in rooms:
                return code
        raise RoomError("这台服务器上的房间满了，等一会儿再试", 503)

    def _sweep(self, now):
        """回收空闲房间。挂在每次进房前做，省一个后台线程。"""
        for code in [key for key, room in self.rooms.items()
                     if now - room["touched"] > ROOM_TTL_SEC]:
            del self.rooms[code]

    def _seat(self, code, token, now):
        """按 token 找回自己的座位；找不到就是「这台机器不属于这个房间」。"""
        room = self.rooms.get(code)
        if room is None:
            raise RoomError("房间已经关闭了，重新开一个吧", 404)
        for seat in ("a", "b"):
            if room["seats"].get(seat) and room["seats"][seat] == token:
                room["touched"] = now
                return room, seat
        raise RoomError("这个房间不认识这台机器", 403)

    def _view(self, room, seat, now, token=""):
        """发给客户端的房间视图。

        只回「自己」的 token，绝不下发对手的 —— 拿到它就能替对方落子。
        `now` 和 `startAt` 一起给，是为了让客户端算自己的开始时刻：
        两台机器的系统时钟不可能一样准，只能以服务器的时间轴为准。
        """
        other = "b" if seat == "a" else "a"
        view = {
            "code": room["code"],
            "game": room["game"],
            "level": room["level"],
            "status": room["status"],
            "seat": seat,
            "turn": room["turn"],
            "moves": room["moves"],
            "opponent": bool(room["seats"].get(other)),
            "startAt": room["startAt"],
            "results": room["results"],
            "winner": room["winner"],
            "reason": room.get("reason", ""),
            "version": room["version"],
            "now": now,
        }
        if token:
            view["token"] = token
        return view

    # -- 建房 / 进房 ---------------------------------------------

    def create(self, game, level):
        now = time.time()
        if game not in GAME_KINDS:
            raise RoomError("不认识的游戏")
        with self._lock:
            self._sweep(now)
            if len(self.rooms) >= ROOM_MAX:
                raise RoomError("这台服务器上的房间满了，等一会儿再试", 503)
            try:
                level = int(level)
            except (TypeError, ValueError):
                level = 2
            level = max(1, min(3, level))
            code = self._make_code(self.rooms)
            token = secrets.token_hex(8)
            room = {
                "code": code,
                "game": game,
                "level": level,
                "status": "waiting",
                "created": now,
                "touched": now,
                "seats": {"a": token, "b": ""},
                "turn": "a",
                "moves": [],
                "startAt": 0.0,
                "results": [],
                "winner": "",
                # 结束原因："finish" 正常下完 / "leave" 对手退出。
                # 客户端要分得清这两种：同样是「你赢了」，一个值得高兴，
                # 另一个其实是对面走开了，说成「小伙伴离开了」才诚实。
                "reason": "",
                "version": 1,
            }
            self.rooms[code] = room
            return self._view(room, "a", now, token=token)

    def join(self, code, token=""):
        """进房。带自己的 token 就是「回到原来的座位」，用来处理断线重连。"""
        now = time.time()
        code = (code or "").strip()
        if not code:
            raise RoomError("房间号没填")
        with self._lock:
            self._sweep(now)
            room = self.rooms.get(code)
            if room is None:
                raise RoomError("房间不存在，检查一下号码", 404)

            for seat in ("a", "b"):
                if token and room["seats"].get(seat) == token:
                    return self._view(room, seat, now, token=token)

            if room["seats"].get("b"):
                raise RoomError("房间里已经有两个小朋友了", 409)
            token = secrets.token_hex(8)
            room["seats"]["b"] = token
            room["status"] = "playing"
            room["turn"] = "a"
            if room["game"] == "klotski":
                # 两个人到齐才开始计时：否则先到的人可以慢慢看题
                room["startAt"] = now + GAME_COUNTDOWN_SEC
            room["touched"] = now
            room["version"] += 1
            return self._view(room, "b", now, token=token)

    # -- 对局中 --------------------------------------------------

    def state(self, code, token):
        now = time.time()
        with self._lock:
            self._sweep(now)
            room, seat = self._seat((code or "").strip(), token, now)
            return self._view(room, seat, now)

    def move(self, code, token, frm, to, promo):
        now = time.time()
        with self._lock:
            self._sweep(now)
            room, seat = self._seat((code or "").strip(), token, now)
            if room["game"] != "chess":
                raise RoomError("这个房间不是国际象棋", 409)
            if not room["seats"].get("b"):
                raise RoomError("对手还没进来", 409)
            if room["status"] == "done":
                raise RoomError("这局已经结束了", 409)
            if room["status"] != "playing":
                raise RoomError("棋盘还没摆好", 409)
            if room["turn"] != seat:
                raise RoomError("还没轮到你走", 409)

            try:
                frm = int(frm)
                to = int(to)
            except (TypeError, ValueError):
                raise RoomError("走法格式不对")
            if not (0 <= frm < 64 and 0 <= to < 64) or frm == to:
                raise RoomError("走法格式不对")
            promo = str(promo or "").upper()
            if promo not in ("", "Q", "R", "B", "N"):
                raise RoomError("升变只能选后、车、象、马")
            if len(room["moves"]) >= GAME_MAX_MOVES:
                raise RoomError("这盘棋太长了，重新开一局吧", 409)

            room["moves"].append({"f": frm, "t": to, "p": promo})
            room["turn"] = "b" if seat == "a" else "a"
            room["touched"] = now
            room["version"] += 1
            return self._view(room, seat, now)

    def finish(self, code, token, payload):
        """国际象棋：报结果（a / b / draw）；华容道：报「我解出来了」+ 步数。"""
        now = time.time()
        with self._lock:
            self._sweep(now)
            room, seat = self._seat((code or "").strip(), token, now)

            if room["game"] == "chess":
                result = str(payload.get("result") or "")
                if result not in ("a", "b", "draw"):
                    raise RoomError("结果只能是 a / b / draw")
                room["winner"] = result
                room["status"] = "done"
                room["reason"] = "finish"
            else:
                if not room["startAt"] or now < room["startAt"]:
                    raise RoomError("比赛还没开始", 409)
                # 先查「是不是你自己又交了一遍」：同一人重复提交要给出准确提示，
                # 而不是笼统地说房间满了
                if any(item["seat"] == seat for item in room["results"]):
                    raise RoomError("你已经交过成绩了", 409)
                if len(room["results"]) >= 2:
                    raise RoomError("两个人都交过成绩了", 409)
                try:
                    steps = int(payload.get("steps") or 0)
                except (TypeError, ValueError):
                    steps = 0
                # 耗时用服务端时间算，不信客户端报的毫秒数：两台机器的计时器
                # 精度和开始时刻都不一样，只有服务器这条时间轴是共同的。
                elapsed_ms = int(max(0.0, now - room["startAt"]) * 1000)
                room["results"].append({
                    "seat": seat,
                    "ms": elapsed_ms,
                    "steps": max(0, min(9999, steps)),
                })
                if not room["winner"]:
                    room["winner"] = seat
                if len(room["results"]) >= 2:
                    # 两个人都解完了才算这一局结束：先解出来的人还要留在
                    # 房间里等对方，不然他看不到「对手用了多久」。
                    room["status"] = "done"
                    room["reason"] = "finish"

            room["touched"] = now
            room["version"] += 1
            return self._view(room, seat, now)

    def leave(self, code, token):
        now = time.time()
        with self._lock:
            room = self.rooms.get((code or "").strip())
            if room is None:
                return {"ok": True}
            for seat in ("a", "b"):
                if token and room["seats"].get(seat) == token:
                    room["seats"][seat] = ""
                    room["touched"] = now
                    if seat == "a":
                        # 房主走了，房间跟着拆：留着也没人能再进来开局
                        del self.rooms[code]
                    else:
                        # 对手退了就算弃权，让留在房间里的人看到「对方离开了」
                        room["status"] = "done"
                        room["winner"] = "a"
                        room["reason"] = "leave"
                        room["version"] += 1
                    break
            return {"ok": True}

    def stats(self):
        now = time.time()
        with self._lock:
            self._sweep(now)
            playing = sum(1 for room in self.rooms.values()
                          if room["status"] == "playing")
            return {"count": len(self.rooms), "playing": playing}


# ── HTTP 层 ─────────────────────────────────────────────────────


class Handler(BaseHTTPRequestHandler):
    server_version = "KidsSafeStory/" + VERSION
    protocol_version = "HTTP/1.1"

    library = None
    learn = None
    rooms = None
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

    def _body_length(self):
        try:
            return int(self.headers.get("Content-Length") or 0)
        except (TypeError, ValueError):
            return 0

    def _body_json(self):
        """读并校验 JSON 请求体，返回 (payload, 错误说明)。"""
        length = self._body_length()
        if length <= 0:
            return None, "请求体是空的"
        if length > LEARN_MAX_BODY:
            return None, "请求体超过 %d KB" % (LEARN_MAX_BODY // 1024)
        raw = self.rfile.read(length)
        try:
            payload = json.loads(raw.decode("utf-8"))
        except (ValueError, UnicodeDecodeError):
            return None, "请求体不是合法 JSON"
        if not isinstance(payload, dict):
            return None, "请求体应当是 JSON 对象"
        return payload, ""

    def _drain(self):
        """把没被读走的请求体读完：HTTP/1.1 长连接下不读会让下一个请求错位。"""
        length = self._body_length()
        if 0 < length <= LEARN_MAX_BODY:
            self.rfile.read(length)

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
                payload = lib.stats()
                # 家长后台首页顺带显示「有没有收到孩子端的上报」
                payload["learnChildren"] = len(self.learn.children)
                payload["rooms"] = self.rooms.stats() if self.rooms else {"count": 0, "playing": 0}
                self._json(payload)
                return

            if path == "/api/learn/summary":
                self._json(self.learn.summary())
                return

            if path == "/api/game/state":
                if self.rooms is None:
                    self._json({"ok": False, "error": "联机功能没开"}, 503)
                    return
                code = (query.get("code", [""])[0] or "").strip()
                token = (query.get("token", [""])[0] or "").strip()
                try:
                    self._json({"ok": True, "room": self.rooms.state(code, token)})
                except RoomError as error:
                    self._json({"ok": False, "error": str(error)}, error.status)
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
        path = unquote(parsed.path)

        if path == "/api/reload":
            self._drain()
            self.library.load()
            self._json({"ok": True, "stats": self.library.stats()})
            return

        if path == "/api/learn/report":
            payload, error = self._body_json()
            if error:
                self._json({"ok": False, "error": error}, 400)
                return
            self._json(self.learn.report(payload))
            return

        if path == "/api/learn/reset":
            # 请求体可以不传：不传就是「全部清空」
            payload = {}
            if self._body_length() > 0:
                payload, error = self._body_json()
                if error:
                    self._json({"ok": False, "error": error}, 400)
                    return
            child = LearnStore._clean(payload.get("child"), 32)
            self._json(self.learn.reset(child))
            return

        if path.startswith("/api/game/"):
            self._game_route(path[len("/api/game/"):])
            return

        self._drain()
        self._json({"ok": False, "error": "unknown path"}, 404)

    # -- 联机对战 -------------------------------------------------

    def _game_route(self, action):
        """房间相关的 POST：建 / 进 / 走一步 / 交成绩 / 退出。

        统一回 {"ok": true, "room": {...}}，和轮询拿到的结构一模一样：
        客户端只需要一套「把 room 套到界面上」的代码，不用为每个接口
        各写一份解析。
        """
        store = self.rooms
        if store is None:
            self._drain()
            self._json({"ok": False, "error": "联机功能没开"}, 503)
            return

        try:
            payload, error = self._body_json()
            if error:
                raise RoomError(error)
            code = str(payload.get("code") or "")
            token = str(payload.get("token") or "")

            if action == "create":
                room = store.create(payload.get("game"), payload.get("level"))
            elif action == "join":
                room = store.join(code, token)
            elif action == "move":
                room = store.move(code, token, payload.get("from"),
                                  payload.get("to"), payload.get("promo"))
            elif action == "finish":
                room = store.finish(code, token, payload)
            elif action == "leave":
                self._json(store.leave(code, token))
                return
            else:
                self._json({"ok": False, "error": "unknown path"}, 404)
                return
            self._json({"ok": True, "room": room})
        except RoomError as error:
            self._json({"ok": False, "error": str(error)}, error.status)


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
    learn = LearnStore(data_dir)
    rooms = RoomStore()
    Handler.library = library
    Handler.learn = learn
    Handler.rooms = rooms
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
    print("学习情况：已收到 %d 个孩子的上报" % len(learn.children))
    print("联机对战：房间表就绪（最多 %d 个房间，%d 分钟没动静回收）"
          % (ROOM_MAX, ROOM_TTL_SEC // 60))
    for error in library.source_errors:
        print("  注意：%s" % error)
    if learn.error:
        print("  注意：%s" % learn.error)
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
