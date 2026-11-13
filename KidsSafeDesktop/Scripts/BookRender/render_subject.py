#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
整科批量：一个科目目录下多个版本，一次渲染完并合并进 books.json。

用法：
    python render_subject.py --subject math --name 数学 --src "D:/小学数学" \\
        --out "D:/WorkBuddy/KidsSafeServer/wwwroot/books"

    --subject   科目代号（chinese / math / english …）
    --name      科目中文名
    --icon      科目图标，默认 🔢
    --src       科目根目录，其下一层是各版本文件夹（人教版 / 北师大版 …）
    --out       服务端 wwwroot/books
    --dpi       页图分辨率，默认 120
    --only      只跑指定书号，逗号分隔
    --editions  只跑指定版本文件夹，逗号分隔
    --force     已有页图也重新渲染（默认跳过，便于断点续跑）

单个版本的渲染细节（CLI 优先、WinRT 兜底）全部复用同目录的 render.py。
"""

import argparse
import json
import os
import shutil
import sys
import tempfile
import traceback

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render as R

# 版本文件夹名 → (版本代号, 显示名, 芯片短名, 最大年级)
# 五四制的最大年级是 5（小学五年），六三制是 6。
EDITIONS = {
    "人教版": ("rj", "人教版", "人教", 6),
    "冀教版": ("jj", "冀教版", "冀教", 6),
    "北京版": ("bj", "北京版", "北京", 6),
    "北师大版": ("bsd", "北师大版", "北师大", 6),
    "沪教版【五四制】": ("hj54", "沪教版（五四制）", "沪教五四", 5),
    "苏教版": ("sj", "苏教版", "苏教", 6),
    "西南大学版": ("xndx", "西南大学版", "西大", 6),
    "青岛版": ("qd", "青岛版", "青岛", 6),
    "青岛版【五四制】": ("qd54", "青岛版（五四制）", "青岛五四", 5),
}


def log(msg, fh=None):
    print(msg, flush=True)
    if fh:
        fh.write(msg + "\n")
        fh.flush()


def render_one(book, pages_root, pdf_root, dpi, work, logfh):
    """渲染一本，返回 (页数, 页图总字节)。0 表示失败。"""
    bid = book["id"]
    target = os.path.join(pages_root, bid)
    shutil.rmtree(target, ignore_errors=True)

    # CLI 读不了非 ASCII 路径，先拷成 ASCII 名再喂给它
    stage = os.path.join(work, bid + ".pdf")
    shutil.copy2(book["src"], stage)
    try:
        pages = R.render_cli(stage, target, dpi)
        if pages == 0:
            log("      CLI 解析失败，改用 Windows 渲染器…", logfh)
            shutil.rmtree(target, ignore_errors=True)
            pages = R.render_winrt(stage, target, int(0.708 * dpi * 10))
    finally:
        if os.path.exists(stage):
            os.remove(stage)

    if pages == 0:
        return 0, 0

    # 原件：同盘硬链接不占空间，跨盘退回复制
    dst_pdf = os.path.join(pdf_root, bid + ".pdf")
    if os.path.exists(dst_pdf):
        os.remove(dst_pdf)
    try:
        os.link(book["src"], dst_pdf)
    except OSError:
        shutil.copy2(book["src"], dst_pdf)

    size = sum(os.path.getsize(os.path.join(target, f)) for f in os.listdir(target))
    return pages, size


def count_pages(pages_root, bid):
    d = os.path.join(pages_root, bid)
    if not os.path.isdir(d):
        return 0
    return len([f for f in os.listdir(d) if f.startswith("page-")])


def merge_manifest(out, subject, subject_name, icon, edition_defs, done):
    """把本科目写进 books.json：覆盖本科目的 editions 与 books，其余科目原样保留。"""
    path = os.path.join(out, "books.json")
    data = {"subjects": [], "editions": [], "books": []}
    if os.path.exists(path):
        try:
            with open(path, encoding="utf-8") as f:
                data = json.load(f)
        except Exception as e:
            print(f"  ! 旧 books.json 读不动（{e}），将重建")

    subjects = [s for s in data.get("subjects", []) if s.get("id") != subject]
    subjects.append({"id": subject, "name": subject_name, "icon": icon})

    # 别的科目原样保留；本科目「按 id 覆盖 + 保留未参与本次渲染的」。
    # 不能用整段替换：补渲染失败的几本时，会把之前渲染好的书从清单里冲掉。
    editions = {e["id"]: e for e in data.get("editions", [])}
    books = {b["id"]: b for b in data.get("books", [])}

    for aid, meta in edition_defs.items():
        editions[aid] = {
            "id": aid,
            "name": meta[1],
            "short": meta[2],
            "subject": subject,
            "maxGrade": meta[3],
        }
    for b in done:
        books[b["id"]] = b

    editions = list(editions.values())
    books = list(books.values())

    # 版本顺序按科目内定义走，科目顺序按 subjects 数组走
    order = {s["id"]: i for i, s in enumerate(subjects)}
    aidx = {e["id"]: i for i, e in enumerate(editions)}
    editions.sort(key=lambda e: (order.get(e.get("subject"), 99), aidx[e["id"]]))
    books.sort(key=lambda b: (order.get(b.get("subject"), 99),
                              aidx.get(b.get("edition"), 99),
                              b.get("grade", 0), b.get("term", 0), bool(b.get("legacy"))))

    out_data = {
        "note": data.get("note") or
                "电子课本清单：新增一本书时，把页图放到 wwwroot/books/pages/<id>/page-0001.jpg 起，"
                "再在这里加一条，不用重启服务（按文件时间缓存）。",
        "subjects": subjects,
        "editions": editions,
        "books": books,
    }
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out_data, f, ensure_ascii=False, indent=2)
    return len(editions), len(books)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--subject", required=True)
    ap.add_argument("--name", required=True)
    ap.add_argument("--icon", default="🔢")
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--dpi", type=int, default=120)
    ap.add_argument("--only", default="")
    ap.add_argument("--editions", default="")
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()

    pages_root = os.path.join(a.out, "pages")
    pdf_root = os.path.join(a.out, "pdf")
    os.makedirs(pages_root, exist_ok=True)
    os.makedirs(pdf_root, exist_ok=True)

    only_books = {x.strip() for x in a.only.split(",") if x.strip()}
    only_ed = {x.strip() for x in a.editions.split(",") if x.strip()}

    # 按 EDITIONS 的定义顺序处理（人教版在前），而不是按目录名字典序，
    # 这样版本芯片的顺序是人工排好的，不会因为「北师大版 / 北京版」的排序跑偏。
    folders = [d for d in EDITIONS if os.path.isdir(os.path.join(a.src, d))]
    for extra in sorted(os.listdir(a.src)):
        if os.path.isdir(os.path.join(a.src, extra)) and extra not in EDITIONS:
            print(f"  跳过未知版本目录：{extra}")

    used_defs = {}
    plan = []
    for folder in folders:
        if only_ed and folder not in only_ed:
            continue
        eid, ename, eshort, max_grade = EDITIONS[folder]
        aid = f"{a.subject}-{eid}"
        used_defs[aid] = (aid, ename, eshort, max_grade)
        books = R.plan(os.path.join(a.src, folder), aid, aid)
        for b in books:
            b["subject"] = a.subject
        plan.extend(books)

    if only_books:
        plan = [b for b in plan if b["id"] in only_books]

    # 日志不能放进 wwwroot：会被当成静态资源参与打包（文件正被写时会锁住构建）
    log_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "logs")
    os.makedirs(log_dir, exist_ok=True)
    log_path = os.path.join(log_dir, f"render_{a.subject}.log")
    logfh = open(log_path, "a", encoding="utf-8")
    log(f"\n=== {a.name} 共 {len(plan)} 本，开始 {__import__('datetime').datetime.now():%H:%M:%S}", logfh)

    work = tempfile.mkdtemp(prefix=f"bookstage-{a.subject}-")
    done, failed, skipped = [], [], []
    total_pages = total_bytes = 0

    try:
        for i, b in enumerate(plan, 1):
            bid = b["id"]
            name = os.path.basename(b["src"])
            have = count_pages(pages_root, bid)
            if have and not a.force:
                log(f"[{i}/{len(plan)}] {bid:<14} 已有 {have} 页，跳过", logfh)
                skipped.append(bid)
                total_pages += have
                continue

            log(f"[{i}/{len(plan)}] {bid:<14} {name}", logfh)
            try:
                pages, size = render_one(b, pages_root, pdf_root, a.dpi, work, logfh)
            except Exception:
                log("      ! 渲染抛异常：\n" + traceback.format_exc(), logfh)
                pages, size = 0, 0

            if pages == 0:
                log("      ! 两条路都没渲染出来，跳过", logfh)
                failed.append(bid)
                continue

            log(f"      {pages} 页，{size / 1048576:.1f} MB", logfh)
            total_pages += pages
            total_bytes += size
            done.append(dict(id=bid, subject=a.subject, edition=b["edition"], grade=b["grade"],
                             term=b["term"], year=b["year"], legacy=bool(b["legacy"])))
    finally:
        shutil.rmtree(work, ignore_errors=True)

    ne, nb = merge_manifest(a.out, a.subject, a.name, a.icon, used_defs, done)
    log(f"\n=== 成功 {len(done)} 本，跳过 {len(skipped)} 本，失败 {len(failed)} 本", logfh)
    log(f"    本次新增 {total_pages} 页，{total_bytes / 1048576:.0f} MB", logfh)
    if failed:
        log("    失败：" + ", ".join(failed), logfh)
    log(f"    清单已更新：{ne} 个版本 / {nb} 本；日志 {log_path}", logfh)
    logfh.close()


if __name__ == "__main__":
    main()
