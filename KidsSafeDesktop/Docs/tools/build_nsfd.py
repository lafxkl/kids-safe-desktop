#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把开源「语音测试系统」(nsfd) 改造成【本地离线可运行】版本，并打包为
KidsSafeDesktop 的 Resources/Raw/Nsfd.zip，运行时由 NsfdAssets 释放到
应用数据目录，再用 WebView 以 file:// 加载。

改造要点（解决 file:// 下无法离线运行的问题）：
  1. 所有绝对路径（/common/、/testN/、/picture/、/voice/、/test7/...）
     改为相对路径，使其在 file:// 下能正确解析。
  2. test7 原本用 fetch('questions.json') 加载题目，而 WebView 在 file://
     下会因同源策略拦截 fetch —— 改为把题目 JSON 内联进 window.__NSFD_QUESTIONS__。
  3. 精简未使用的语音目录（voice1 下仅 3,9及training刺激 被引用；其余
     11刺激 / 4，10及training刺激 / 5.1，5.2刺激 约 68MB 均未被任何测试引用，剔除）。
  4. 精简未使用的图片目录（第三部分 / 第五部分 未被引用，剔除）。

运行：python3 Docs/tools/build_nsfd.py
依赖：仅标准库。
"""
import os
import shutil
import json
import zipfile

SRC = r"D:/Downloads/nsfd"
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # KidsSafeDesktop
RAW = os.path.join(ROOT, "Resources", "Raw")
STAGE = os.path.join(RAW, "_nsfd_stage", "Nsfd")
ZIP = os.path.join(RAW, "Nsfd.zip")


def cp_bin(src, dst):
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(src, dst)


def cp_text(src, dst, repl=None):
    with open(src, encoding="utf-8") as f:
        s = f.read()
    if repl:
        for a, b in repl:
            s = s.replace(a, b)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    with open(dst, "w", encoding="utf-8") as f:
        f.write(s)


def copy_names(src_dir, dst_dir, names):
    os.makedirs(dst_dir, exist_ok=True)
    for name in names:
        s = os.path.join(src_dir, name)
        d = os.path.join(dst_dir, name)
        if os.path.isdir(s):
            shutil.copytree(s, d)
        elif os.path.isfile(s):
            shutil.copy2(s, d)


def main():
    if os.path.exists(STAGE):
        shutil.rmtree(STAGE)
    if os.path.exists(ZIP):
        os.remove(ZIP)
    os.makedirs(STAGE, exist_ok=True)

    # 1) 根目录文件（index.html/style.css/script.js 无绝对路径，原样复制）
    for f in ["index.html", "style.css", "script.js"]:
        cp_bin(os.path.join(SRC, f), os.path.join(STAGE, f))

    # 2) 公共用户信息模块
    os.makedirs(os.path.join(STAGE, "common"), exist_ok=True)
    for f in ["user-info.html", "user-info.css", "user-info.js"]:
        cp_bin(os.path.join(SRC, "common", f), os.path.join(STAGE, "common", f))

    # 3) 图片：仅保留被引用的子目录
    copy_names(os.path.join(SRC, "picture"), os.path.join(STAGE, "picture"),
               ["第二部分（pdf）", "第四部分", "test5", "test6"])

    # 4) 语音：仅保留被引用的目录（voice1/3,9及training刺激 整目录；test5/test6）
    os.makedirs(os.path.join(STAGE, "voice"), exist_ok=True)
    shutil.copytree(os.path.join(SRC, "voice", "voice1", "3,9及training刺激"),
                    os.path.join(STAGE, "voice", "voice1", "3,9及training刺激"))
    copy_names(os.path.join(SRC, "voice"), os.path.join(STAGE, "voice"), ["test5", "test6"])

    # 5) 测试模块 1-7
    for n in range(1, 8):
        sdir = os.path.join(SRC, f"test{n}")
        ddir = os.path.join(STAGE, f"test{n}")
        os.makedirs(ddir, exist_ok=True)

        html_repl = [
            ('href="/common/user-info.css"', 'href="../common/user-info.css"'),
            ('src="/common/user-info.js"', 'src="../common/user-info.js"'),
            (f'href="/test{n}/style.css"', 'href="style.css"'),
            (f'src="/test{n}/script.js"', 'src="script.js"'),
        ]
        if n == 5:
            html_repl.append(('src="/picture/test5/sen_corr_pract.jpg"',
                              'src="../picture/test5/sen_corr_pract.jpg"'))
        cp_text(os.path.join(sdir, "index.html"), os.path.join(ddir, "index.html"), html_repl)

        js_repl = []
        if n == 5:
            js_repl.append(('const audioPath = "/voice/test5/";',
                            'const audioPath = "../voice/test5/";'))
        if n == 6:
            js_repl.append(("const instructionAudioPath = '/voice/test6/instruction6.wav';",
                            "const instructionAudioPath = '../voice/test6/instruction6.wav';"))
            js_repl.append(("'/picture/test6/", "'../picture/test6/"))
            js_repl.append(("'/voice/test6/", "'../voice/test6/"))
        cp_text(os.path.join(sdir, "script.js"), os.path.join(ddir, "script.js"), js_repl)

        cp_bin(os.path.join(sdir, "style.css"), os.path.join(ddir, "style.css"))

        if n == 7:
            # introduction.html 路径修正
            intro_repl = [
                ('href="/test7/style.css"', 'href="style.css"'),
                ('src="/test7/voice/instruction11.wav"', 'src="voice/instruction11.wav"'),
                ('src="/test7/script.js"', 'src="script.js"'),
                ('href="/common/user-info.css"', 'href="../common/user-info.css"'),
                ('src="/common/user-info.js"', 'src="../common/user-info.js"'),
            ]
            cp_text(os.path.join(sdir, "introduction.html"),
                    os.path.join(ddir, "introduction.html"), intro_repl)

            # 复制 test7 自身的图片/语音
            shutil.copytree(os.path.join(sdir, "picture"), os.path.join(ddir, "picture"))
            shutil.copytree(os.path.join(sdir, "voice"), os.path.join(ddir, "voice"))

            # 题目 JSON 内联（绝对路径 -> 相对路径），并剔除引用了缺失文件的题目
            with open(os.path.join(sdir, "questions.json"), encoding="utf-8") as f:
                qlist = json.load(f)
            kept = []
            for q in qlist:
                ok = True
                for key in ("centerImage", "leftToneImage", "rightToneImage", "audio1", "audio2"):
                    rel = q.get(key, "")
                    if not rel:
                        continue
                    rel = rel.replace("/test7/picture/", "./picture/").replace("/test7/voice/", "./voice/")
                    if not os.path.isfile(os.path.join(sdir, rel)):
                        ok = False
                        break
                if ok:
                    kept.append(q)
                else:
                    print(f"  [跳过] test7 题目引用缺失文件，已剔除: {q.get('centerImage')}")
            qj = json.dumps(kept, ensure_ascii=False)

            idx_path = os.path.join(ddir, "index.html")
            with open(idx_path, encoding="utf-8") as f:
                html = f.read()
            injection = '<script>window.__NSFD_QUESTIONS__ = ' + qj.strip() + ';</script>\n    '
            html = html.replace('<script src="../common/user-info.js"></script>',
                                injection + '<script src="../common/user-info.js"></script>', 1)
            with open(idx_path, "w", encoding="utf-8") as f:
                f.write(html)

            # 改写 script.js：放弃 fetch，改用内联全局变量
            sj = os.path.join(ddir, "script.js")
            with open(sj, encoding="utf-8") as f:
                s = f.read()
            start = s.index('// 从JSON文件加载题目配置')
            end = s.index('// Fisher-Yates')
            new_fn = (
                "// 题目直接内联在 index.html 的 window.__NSFD_QUESTIONS__，离线可用"
                "（file:// 下 fetch 会被同源策略拦截）\n"
                "function loadQuestions() {\n"
                "    questions = window.__NSFD_QUESTIONS__ || [];\n"
                "    questions = shuffleArray(questions);\n"
                "    console.log('题目加载成功，共', questions.length, '题，顺序已打乱');\n"
                "    return questions;\n"
                "}\n\n"
            )
            s = s[:start] + new_fn + s[end:]
            with open(sj, "w", encoding="utf-8") as f:
                f.write(s)

    # 6) 打包为 Nsfd.zip（顶层为 Nsfd/ 目录）
    print("正在打包 Nsfd.zip ...")
    with zipfile.ZipFile(ZIP, "w", zipfile.ZIP_DEFLATED) as z:
        for root, _, files in os.walk(STAGE):
            for fn in files:
                fp = os.path.join(root, fn)
                arc = os.path.relpath(fp, os.path.dirname(STAGE))  # -> Nsfd/...
                z.write(fp, arc)
    shutil.rmtree(os.path.join(RAW, "_nsfd_stage"))

    size = os.path.getsize(ZIP) / 1024 / 1024
    print(f"完成：{ZIP}  ({size:.1f} MB)")


if __name__ == "__main__":
    main()
