using System.Linq;
using System.Text;
using KidsSafeDesktop.Models;
using Microsoft.Data.Sqlite;

namespace KidsSafeDesktop.Services;

/// <summary>
/// SQLite 题库：数学口算（五～九年级）+ 英语词库（一～九年级）。
///
/// 为什么用 SQL 而不是 C# 数组：
/// ① 题量上千后，静态数组会让程序集变大、改一道题就要重新编译；
/// ② 按年级 / 题型 / 分类筛选，SQL 的 WHERE + ORDER BY RANDOM() 一句就能做，
///    不用在内存里反复过滤排序；
/// ③ 词库、题库以后要持续补，改 .sql 比改代码安全。
///
/// 库文件在首次运行时创建：建表 → 执行随包发布的 seed_math.sql / seed_english.sql /
/// seed_exam.sql（MauiAsset，位于 Resources/Raw/questbank），完成后写入 meta.seed_version
/// 与 meta.seed_fingerprint。
///
/// 为什么要指纹：只比版本号的话，改了 .sql 却忘了 +1，老用户永远读旧题。
/// 指纹是两份脚本内容的 SHA-256，内容一变就自动重灌，不依赖人记性。
///
/// 全部方法都做了异常兜底：题库不可用时 <see cref="IsAvailable"/> 为 false，
/// 调用方自行降级（口算回落到程序出题、英语回落到内置词表），不让页面崩。
/// </summary>
public sealed class QuestionBank
{
    /// <summary>种子数据版本：仅作文档与诊断用，真正的重灌判断看脚本指纹。</summary>
    private const int SeedVersion = 1;

    private const string DbFileName = "questbank.db";

    private static readonly string[] SeedScripts =
    {
        "questbank/seed_math.sql",
        "questbank/seed_english.sql",
        "questbank/seed_exam.sql"
    };

    private static readonly string TableSql = """
        CREATE TABLE IF NOT EXISTS meta(
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS math_problem(
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            grade      INTEGER NOT NULL,
            topic      TEXT    NOT NULL,
            category   TEXT    NOT NULL,
            expression TEXT    NOT NULL,
            answer     REAL    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_math_grade_topic ON math_problem(grade, topic);
        CREATE TABLE IF NOT EXISTS english_word(
            id       INTEGER PRIMARY KEY AUTOINCREMENT,
            grade    INTEGER NOT NULL,
            category TEXT    NOT NULL,
            word     TEXT    NOT NULL,
            meaning  TEXT    NOT NULL,
            phonetic TEXT    NOT NULL DEFAULT '',
            emoji    TEXT    NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS ix_word_grade ON english_word(grade);
        CREATE TABLE IF NOT EXISTS exam_paper(
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            code         TEXT    NOT NULL UNIQUE,
            grade        INTEGER NOT NULL,
            subject      TEXT    NOT NULL,
            title        TEXT    NOT NULL,
            term         TEXT    NOT NULL DEFAULT '',
            duration_min INTEGER NOT NULL DEFAULT 60,
            note         TEXT    NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS ix_exam_paper_grade ON exam_paper(grade, subject);
        CREATE TABLE IF NOT EXISTS exam_question(
            id       INTEGER PRIMARY KEY AUTOINCREMENT,
            paper_id INTEGER NOT NULL,
            ordinal  INTEGER NOT NULL,
            kind     TEXT    NOT NULL,
            stem     TEXT    NOT NULL,
            options  TEXT    NOT NULL DEFAULT '',
            answer   TEXT    NOT NULL,
            explain  TEXT    NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS ix_exam_question_paper ON exam_question(paper_id, ordinal);
        """;

    public static QuestionBank Instance { get; } = new();

    private readonly object _gate = new();
    private readonly Dictionary<int, List<EnglishWord>> _wordCache = new();
    private List<EnglishWord>? _allWords;
    private bool _ready;
    private bool _broken;

    private QuestionBank() { }

    /// <summary>题库是否可用。首次调用 <see cref="EnsureReady"/> 之前恒为 false。</summary>
    public bool IsAvailable
    {
        get { lock (_gate) return _ready && !_broken; }
    }

    /// <summary>题目总数（仅用于帮助页/诊断，失败返回 0）。</summary>
    public int MathCount
    {
        get
        {
            EnsureReady();
            return Scalar("SELECT COUNT(*) FROM math_problem");
        }
    }

    /// <summary>词库总数。</summary>
    public int WordCount
    {
        get
        {
            EnsureReady();
            return Scalar("SELECT COUNT(*) FROM english_word");
        }
    }

    /// <summary>
    /// 确保库文件已就绪。重复调用只做一次初始化，之后直接返回。
    /// 初始化放在线程池里跑：页面构造函数里是同步调用，若在 UI 线程上
    /// 直接 await 文件读取，WinUI 上可能死锁。
    /// </summary>
    public void EnsureReady()
    {
        if (_ready || _broken) return;
        lock (_gate)
        {
            if (_ready || _broken) return;
            try
            {
                Task.Run(Initialize).GetAwaiter().GetResult();
                _ready = true;
            }
            catch (Exception ex)
            {
                _broken = true;
                System.Diagnostics.Debug.WriteLine($"[QuestionBank] 题库不可用：{ex}");
            }
        }
    }

    // ================= 数学 =================

    /// <summary>按年级 + 题型随机取题；该题型没有题时退回整年级随机。</summary>
    public List<OralProblem> PickMath(int grade, OralTopic topic, int count)
    {
        EnsureReady();
        if (!IsAvailable || count <= 0) return new List<OralProblem>();

        var key = TopicKey(topic);
        try
        {
            var list = QueryMath(grade, key, count);

            // 该题型题少（初中的加法/减法本来就不多）时，用整年级的题补齐，
            // 否则一局只出 3 道就结束了。
            if (key != null && list.Count < count)
            {
                if (list.Count == 0) list = QueryMath(grade, null, count);

                if (list.Count < count)
                {
                    var seen = list.Select(p => p.Expression).ToHashSet();
                    foreach (var p in QueryMath(grade, null, count * 3))
                    {
                        if (list.Count >= count) break;
                        if (!seen.Add(p.Expression)) continue;
                        list.Add(p);
                    }
                }
            }
            return list;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuestionBank] 取数学题失败：{ex.Message}");
            return new List<OralProblem>();
        }
    }

    private static string? TopicKey(OralTopic topic) => topic switch
    {
        OralTopic.Add => "add",
        OralTopic.Sub => "sub",
        OralTopic.Mul => "mul",
        OralTopic.Div => "div",
        OralTopic.Mixed => "mix",
        _ => null
    };

    private static List<OralProblem> QueryMath(int grade, string? topic, int count)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = topic == null
            ? "SELECT category, expression, answer FROM math_problem WHERE grade = $g ORDER BY RANDOM() LIMIT $n"
            : "SELECT category, expression, answer FROM math_problem WHERE grade = $g AND topic = $t ORDER BY RANDOM() LIMIT $n";
        cmd.Parameters.AddWithValue("$g", grade);
        cmd.Parameters.AddWithValue("$n", count);
        if (topic != null) cmd.Parameters.AddWithValue("$t", topic);

        var list = new List<OralProblem>(count);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new OralProblem
            {
                Category = r.GetString(0),
                Expression = r.GetString(1),
                Answer = System.Math.Round(r.GetDouble(2), 4)
            });
        }
        return list;
    }

    // ================= 英语 =================

    /// <summary>某一年级全部单词（按分类排好）。</summary>
    public List<EnglishWord> Words(int grade)
    {
        EnsureReady();
        if (!IsAvailable) return new List<EnglishWord>();

        lock (_gate)
        {
            if (_wordCache.TryGetValue(grade, out var cached)) return cached;
        }

        var list = QueryWords(grade);
        lock (_gate)
        {
            _wordCache[grade] = list;
        }
        return list;
    }

    /// <summary>全部年级单词：出干扰项时跨年级补充用。</summary>
    public List<EnglishWord> AllWords()
    {
        EnsureReady();
        if (!IsAvailable) return new List<EnglishWord>();

        lock (_gate)
        {
            if (_allWords != null) return _allWords;
        }

        var list = QueryWords(null);
        lock (_gate)
        {
            _allWords = list;
        }
        return list;
    }

    private static List<EnglishWord> QueryWords(int? grade)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = grade == null
            ? "SELECT grade, category, word, meaning, phonetic, emoji FROM english_word ORDER BY grade, category, id"
            : "SELECT grade, category, word, meaning, phonetic, emoji FROM english_word WHERE grade = $g ORDER BY category, id";
        if (grade != null) cmd.Parameters.AddWithValue("$g", grade.Value);

        var list = new List<EnglishWord>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new EnglishWord
            {
                Grade = (EnglishGrade)(r.GetInt32(0) - 1),
                Category = r.GetString(1),
                Word = r.GetString(2),
                Meaning = r.GetString(3),
                Phonetic = r.GetString(4),
                Emoji = r.GetString(5)
            });
        }
        return list;
    }

    // ================= 模拟考试 =================

    /// <summary>试卷总数（诊断用）。</summary>
    public int ExamPaperCount
    {
        get
        {
            EnsureReady();
            return Scalar("SELECT COUNT(*) FROM exam_paper");
        }
    }

    /// <summary>考题总数（诊断用）。</summary>
    public int ExamQuestionCount
    {
        get
        {
            EnsureReady();
            return Scalar("SELECT COUNT(*) FROM exam_question");
        }
    }

    /// <summary>
    /// 某一年级可考的全部试卷，按「语文 → 数学 → 英语」固定顺序返回。
    /// 用 CASE 排序而不是按字母序，孩子看到的科目顺序才稳定。
    /// </summary>
    public List<ExamPaper> Papers(int grade)
    {
        EnsureReady();
        if (!IsAvailable) return new List<ExamPaper>();

        var list = new List<ExamPaper>();
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT p.id, p.grade, p.subject, p.title, p.term, p.duration_min, p.note,
                       (SELECT COUNT(*) FROM exam_question q WHERE q.paper_id = p.id)
                FROM exam_paper p
                WHERE p.grade = $g
                ORDER BY CASE p.subject WHEN 'chinese' THEN 0 WHEN 'math' THEN 1 ELSE 2 END, p.id
                """;
            cmd.Parameters.AddWithValue("$g", grade);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var subject = ExamSubjectInfo.FromCode(r.GetString(2));
                if (subject == null) continue;

                list.Add(new ExamPaper
                {
                    Id = r.GetInt32(0),
                    Grade = r.GetInt32(1),
                    Subject = subject.Value,
                    Title = r.GetString(3),
                    Term = r.GetString(4),
                    DurationMin = r.GetInt32(5),
                    Note = r.GetString(6),
                    QuestionCount = r.GetInt32(7)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuestionBank] 取试卷失败：{ex.Message}");
        }
        return list;
    }

    /// <summary>一套卷子的题目，按题号升序。</summary>
    public List<ExamQuestion> Questions(int paperId)
    {
        EnsureReady();
        if (!IsAvailable) return new List<ExamQuestion>();

        var list = new List<ExamQuestion>();
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, paper_id, ordinal, kind, stem, options, answer, explain
                FROM exam_question
                WHERE paper_id = $p
                ORDER BY ordinal, id
                """;
            cmd.Parameters.AddWithValue("$p", paperId);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var kind = r.GetString(3) switch
                {
                    "choice" => ExamQuestionKind.Choice,
                    "judge" => ExamQuestionKind.Judge,
                    _ => ExamQuestionKind.Fill
                };

                var options = new List<string>();
                var raw = r.GetString(5);
                if (!string.IsNullOrWhiteSpace(raw))
                    options.AddRange(raw.Split('|', StringSplitOptions.RemoveEmptyEntries));

                list.Add(new ExamQuestion
                {
                    Id = r.GetInt32(0),
                    PaperId = r.GetInt32(1),
                    Ordinal = r.GetInt32(2),
                    Kind = kind,
                    Stem = r.GetString(4),
                    Options = options,
                    Answer = r.GetString(6),
                    Explain = r.GetString(7)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuestionBank] 取考题失败：{ex.Message}");
        }
        return list;
    }

    // ================= 初始化 =================

    private static void Initialize()
    {
        using var conn = Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = TableSql;
            cmd.ExecuteNonQuery();
        }

        // 先把两份脚本读齐再决定要不要灌：读不全就抛，别写指纹，
        // 这样下次启动还会重试，而不是把半套数据当成"已是最新"。
        var scripts = new List<(string Name, string Sql)>();
        foreach (var name in SeedScripts)
        {
            var sql = ReadSeedScript(name);
            if (string.IsNullOrWhiteSpace(sql))
                throw new InvalidOperationException($"种子脚本 {name} 为空或缺失。");
            scripts.Add((name, sql));
        }

        var fingerprint = Fingerprint(scripts);
        if (ReadMeta(conn, "seed_fingerprint") == fingerprint) return;

        foreach (var (_, sql) in scripts)
        {
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
            tx.Commit();
        }

        WriteMeta(conn, "seed_fingerprint", fingerprint);
        WriteMeta(conn, "seed_version", SeedVersion.ToString());
    }

    /// <summary>两份脚本内容的 SHA-256（小写十六进制），内容一变即不同。</summary>
    private static string Fingerprint(List<(string Name, string Sql)> scripts)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var sb = new StringBuilder();
        foreach (var (name, sql) in scripts)
        {
            sb.Append(name).Append('\n').Append(sql).Append('\u0000');
        }
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>读取随包发布的 SQL 脚本（MauiAsset，逻辑名不带 Resources/Raw 前缀）。</summary>
    private static string ReadSeedScript(string name)
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync(name).GetAwaiter().GetResult();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuestionBank] 读取 {name} 失败：{ex.Message}");
            return string.Empty;
        }
    }

    private static string ReadMeta(SqliteConnection conn, string key)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string ?? string.Empty;
    }

    private static void WriteMeta(SqliteConnection conn, string key, string value)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO meta(key, value) VALUES($k, $v) " +
                          "ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    private int Scalar(string sql)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return System.Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
        catch
        {
            return 0;
        }
    }

    private static SqliteConnection Open()
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(FileSystem.AppDataDirectory, DbFileName),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var conn = new SqliteConnection(cs);
        conn.Open();
        return conn;
    }
}
