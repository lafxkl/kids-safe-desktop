import io
import sqlite3

path = r"D:/WorkBuddy/KidsSafeDesktop/Resources/Raw/questbank/seed_exam.sql"
sql = io.open(path, encoding="utf-8").read()

con = sqlite3.connect(":memory:")
con.executescript(
    """
CREATE TABLE exam_paper(id INTEGER PRIMARY KEY AUTOINCREMENT, code TEXT NOT NULL UNIQUE, grade INTEGER NOT NULL,
    subject TEXT NOT NULL, title TEXT NOT NULL, term TEXT NOT NULL DEFAULT '',
    duration_min INTEGER NOT NULL DEFAULT 60, note TEXT NOT NULL DEFAULT '');
CREATE TABLE exam_question(id INTEGER PRIMARY KEY AUTOINCREMENT, paper_id INTEGER NOT NULL, ordinal INTEGER NOT NULL,
    kind TEXT NOT NULL, stem TEXT NOT NULL, options TEXT NOT NULL DEFAULT '',
    answer TEXT NOT NULL, explain TEXT NOT NULL DEFAULT '');
"""
)
con.executescript(sql)

print("试卷数:", con.execute("SELECT COUNT(*) FROM exam_paper").fetchone()[0])
print("题目数:", con.execute("SELECT COUNT(*) FROM exam_question").fetchone()[0])
print("年级分布:", con.execute("SELECT grade, COUNT(*) FROM exam_paper GROUP BY grade").fetchall())
print("科目分布:", con.execute("SELECT subject, COUNT(*) FROM exam_paper GROUP BY subject").fetchall())
print("每套题量:", sorted({r[0] for r in con.execute("SELECT COUNT(*) FROM exam_question GROUP BY paper_id")}))
print("题型分布:", con.execute("SELECT kind, COUNT(*) FROM exam_question GROUP BY kind").fetchall())

bad = []
rows = con.execute(
    "SELECT paper_id, ordinal, id, kind, options, answer, stem, explain, "
    "(SELECT grade FROM exam_paper p WHERE p.id = q.paper_id), "
    "(SELECT subject FROM exam_paper p WHERE p.id = q.paper_id) FROM exam_question q"
).fetchall()

for pid, ordinal, qid, kind, opts, ans, stem, explain, grade, subj in rows:
    os_ = [o for o in opts.split("|") if o]
    if kind == "choice":
        if len(os_) != 4:
            bad.append((qid, grade, subj, "选项数=%d" % len(os_)))
        elif ans not in "ABCD" or "ABCD".index(ans) >= len(os_):
            bad.append((qid, grade, subj, "答案越界 %s" % ans))
    elif kind == "judge":
        if ans not in ("对", "错"):
            bad.append((qid, grade, subj, "判断答案=%s" % ans))
        elif os_:
            bad.append((qid, grade, subj, "判断题不该有选项"))
    else:
        if not ans.strip():
            bad.append((qid, grade, subj, "填空无答案"))
        elif os_:
            bad.append((qid, grade, subj, "填空题不该有选项"))
    if not stem.strip():
        bad.append((qid, grade, subj, "空题干"))
    if not explain.strip():
        bad.append((qid, grade, subj, "空解析"))
    # 题干里允许英文撇号（如 father's），但答案里出现单引号通常是 SQL 转义写错
    if "'" in ans:
        bad.append((qid, grade, subj, "答案含单引号"))

ord_bad = [
    r for r in con.execute("SELECT paper_id, GROUP_CONCAT(ordinal) FROM exam_question GROUP BY paper_id")
    if r[1] != "1,2,3,4,5,6,7,8,9,10,11,12"
]
print("题号异常:", ord_bad if ord_bad else "无")

# 跨年级难度抽查：打印每套卷的题目类型序列
print("\n每套卷题型序列：")
for pid, title in con.execute("SELECT id, title FROM exam_paper ORDER BY id"):
    seq = "".join(
        {"choice": "选", "fill": "填", "judge": "判"}[k]
        for (k,) in con.execute("SELECT kind FROM exam_question WHERE paper_id = ? ORDER BY ordinal", (pid,))
    )
    print(" ", pid, title, seq)

print("\n问题条目:", len(bad))
for b in bad:
    print("  ", b)

# ============ 验证客户端实际使用的两条查询 ============
print("\n=== Papers(grade) 查询 ===")
for g in (3, 6, 9):
    rows = con.execute(
        """SELECT p.id, p.grade, p.subject, p.title, p.term, p.duration_min, p.note,
                  (SELECT COUNT(*) FROM exam_question q WHERE q.paper_id = p.id)
           FROM exam_paper p
           WHERE p.grade = ?
           ORDER BY CASE p.subject WHEN 'chinese' THEN 0 WHEN 'math' THEN 1 ELSE 2 END, p.id""",
        (g,),
    ).fetchall()
    print(" 年级", g, "→", [(r[2], r[3], r[7]) for r in rows])

print("\n=== Questions(paperId) 查询 ===")
rows = con.execute(
    """SELECT id, paper_id, ordinal, kind, stem, options, answer, explain
       FROM exam_question WHERE paper_id = 14 ORDER BY ordinal, id"""
).fetchall()
print(" 卷 14 取到", len(rows), "题；第 1 题：", rows[0][3], rows[0][4][:24])
