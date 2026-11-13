#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把一批 PDF 电子课本整理进服务端（页图 + 原件 + 清单片段）。

用法：
    python render.py --src "D:/小学全科电子课本/小学数学" --edition rj --out "D:/WorkBuddy/KidsSafeServer/wwwroot/books"

    --src       PDF 所在目录（会递归找 *.pdf）
    --edition   版本代号，如 rj（人教版）/ rj54（五四制）
    --out      服务端 wwwroot/books 目录
    --prefix    书号前缀，默认同 --edition
    --dpi       页图分辨率，默认 120（870px 宽，约 115KB/页）
    --only      只处理匹配的书号，逗号分隔（补渲染时有用）

输出：
    <out>/pages/<书号>/page-0001.jpg …
    <out>/pdf/<书号>.pdf
    <out>/books.fragment.json     ← 粘到 books.json 的 books 数组里

两条渲染路线：
    ① 首选 workbuddy-pdf 的 CLI（快，120 DPI 约 13 秒/本）；
    ② CLI 解析不了的 PDF（静默返回 127）自动退到 Windows 自带的
       Windows.Data.Pdf 渲染器（winrt-render/），渲成 PNG 再用 Pillow 转 jpg。
"""
import argparse
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

CN = {"一": 1, "二": 2, "三": 3, "四": 4, "五": 5, "六": 6}
CLI = os.environ.get(
    "WORKBUDDY_PDF_RUN",
    r"D:/Program Files/WorkBuddy/resources/app.asar.unpacked/resources/plugins/"
    r"workbuddy-builtin/skills/workbuddy-pdf/scripts/run.sh",
)
WINRT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                     "winrt-render", "bin", "Release", "net10.0-windows10.0.19041.0", "winrt-render.exe")


def parse_name(path):
    name = os.path.basename(path)
    m = re.search(r"([一二三四五六])年级", name)
    grade = CN[m.group(1)] if m else 0
    term = 2 if "下册" in name else 1
    y = re.search(r"[（(](\d{4}[春秋]版)[）)]", name)
    return grade, term, (y.group(1) if y else "")


def plan(src, prefix, edition):
    books = []
    for p in sorted(glob.glob(os.path.join(src, "**", "*.pdf"), recursive=True)):
        grade, term, year = parse_name(p)
        if not grade:
            print(f"  跳过（认不出年级）：{os.path.basename(p)}")
            continue
        books.append(dict(edition=edition, grade=grade, term=term, year=year,
                          src=p.replace("\\", "/")))

    # 同册有多个版本时，带年份的是现行版，不带的记为旧版存档
    for b in books:
        peers = [x for x in books if (x["grade"], x["term"]) == (b["grade"], b["term"])]
        b["legacy"] = (not b["year"]) and any(x["year"] for x in peers)

    used = set()
    for b in books:
        base = f'{prefix}-{b["grade"]}{"a" if b["term"] == 1 else "b"}'
        bid = base + ("-old" if b["legacy"] else "")
        while bid in used:                      # 同册多份又都没年份时，加序号
            bid += "x"
        used.add(bid)
        b["id"] = bid

    return books


def render_cli(pdf, outdir, dpi):
    """workbuddy-pdf CLI。返回页数，0 表示这条路不通。"""
    os.makedirs(outdir, exist_ok=True)
    r = subprocess.run(["sh", CLI, pdf, outdir + "/", "-f", "jpg", "--dpi", str(dpi), "-q"],
                       capture_output=True)
    pages = len([f for f in os.listdir(outdir) if f.startswith("page-")])
    return pages if r.returncode == 0 and pages > 0 else 0


def render_winrt(pdf, outdir, width):
    """Windows 自带渲染器兜底：先出 PNG，再转 jpg。"""
    if not os.path.exists(WINRT):
        print("    ! WinRT 渲染器未编译，跳过兜底", file=sys.stderr)
        return 0
    tmp = tempfile.mkdtemp(prefix="bookpng-")
    try:
        subprocess.run([WINRT, pdf, tmp, str(width)], check=True)
        from PIL import Image
        os.makedirs(outdir, exist_ok=True)
        pngs = sorted(glob.glob(os.path.join(tmp, "*.png")))
        for f in pngs:
            Image.open(f).convert("RGB").save(
                os.path.join(outdir, os.path.basename(f).replace(".png", ".jpg")),
                "JPEG", quality=82, optimize=True, progressive=True)
        return len(pngs)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--edition", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--prefix", default="")
    ap.add_argument("--dpi", type=int, default=120)
    ap.add_argument("--only", default="")
    a = ap.parse_args()

    prefix = a.prefix or a.edition
    pages_root = os.path.join(a.out, "pages")
    pdf_root = os.path.join(a.out, "pdf")
    os.makedirs(pages_root, exist_ok=True)
    os.makedirs(pdf_root, exist_ok=True)

    books = plan(a.src, prefix, a.edition)
    only = {x.strip() for x in a.only.split(",") if x.strip()}
    if only:
        books = [b for b in books if b["id"] in only]

    print(f"共 {len(books)} 本待处理")
    work = tempfile.mkdtemp(prefix="bookstage-")
    done, failed = [], []

    for b in books:
        bid = b["id"]
        print(f'  {bid:<12} {os.path.basename(b["src"])}')
        stage = os.path.join(work, bid + ".pdf")
        shutil.copy2(b["src"], stage)          # CLI 读不了非 ASCII 路径，先拷成 ASCII 名

        target = os.path.join(pages_root, bid)
        shutil.rmtree(target, ignore_errors=True)

        pages = render_cli(stage, target, a.dpi)
        if pages == 0:
            print("    CLI 解析失败，改用 Windows 渲染器…")
            shutil.rmtree(target, ignore_errors=True)
            pages = render_winrt(stage, target, int(0.708 * a.dpi * 10))

        if pages == 0:
            print("    ! 两条路都没渲染出来，跳过")
            failed.append(bid)
            os.remove(stage)
            continue

        # 原件：同盘直接硬链接，跨盘退回复制
        dst_pdf = os.path.join(pdf_root, bid + ".pdf")
        if os.path.exists(dst_pdf):
            os.remove(dst_pdf)
        try:
            os.link(b["src"], dst_pdf)
        except OSError:
            shutil.copy2(b["src"], dst_pdf)

        size = sum(os.path.getsize(os.path.join(target, f)) for f in os.listdir(target)) / 1048576
        print(f"    {pages} 页，{size:.1f} MB")
        done.append(dict(id=bid, edition=b["edition"], grade=b["grade"],
                         term=b["term"], year=b["year"], legacy=bool(b["legacy"])))
        os.remove(stage)

    shutil.rmtree(work, ignore_errors=True)

    frag = os.path.join(a.out, "books.fragment.json")
    with open(frag, "w", encoding="utf-8") as f:
        json.dump(done, f, ensure_ascii=False, indent=2)

    print(f"\n成功 {len(done)} 本，失败 {len(failed)} 本")
    if failed:
        print("失败：", ", ".join(failed))
    print(f"清单片段已写入 {frag}，把它合并进 books.json 的 books 数组即可。")


if __name__ == "__main__":
    main()
