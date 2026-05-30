// Spike 5 – FTS5 external-content indexing benchmark
// THROWAWAY: validates NFR-OGMA-004 (≤500 ms P95 warm, FTS5 on SQLite)
// Methodology:
//   • Synthetic corpus: 2,000 books × 3 pages = 6,000 rows, ~300 words/page
//   • FTS5 external-content table with insert/delete triggers (per ADR-0006)
//   • 10 representative queries × 50 warm iterations each
//   • P50/P95 measured via Stopwatch; printed as results table
//
// All measurements labelled "dev-box trend, not gated" (not W-REF-01 hardware).

using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;

Benchmark.Run();

internal static class Benchmark
{
    private const int NumBooks = 2_000;
    private const int PagesPerBook = 3;
    private const int TotalRows = NumBooks * PagesPerBook; // 6,000
    private const int WordsPerPage = 300;

    private static readonly string[] Vocab =
    [
        "library", "book", "chapter", "author", "publisher", "edition", "fiction",
        "history", "science", "poetry", "novel", "title", "index", "glossary",
        "bibliography", "footnote", "annotation", "reader", "manuscript", "archive",
        "collection", "catalogue", "reference", "study", "knowledge", "culture",
        "language", "narrative", "character", "setting", "plot", "theme", "genre",
        "discovery", "research", "document", "paragraph", "sentence", "word",
        "volume", "page", "print", "digital", "format", "binding", "cover",
        "university", "academic", "journal", "review", "abstract", "theory",
        "evidence", "analysis", "conclusion", "introduction", "summary", "content",
        "information", "data", "source", "citation", "quote", "excerpt", "passage",
        "literature", "classic", "modern", "ancient", "medieval", "renaissance",
        "biography", "autobiography", "memoir", "essay", "article", "paper",
        "thesis", "dissertation", "monograph", "anthology", "series", "collection",
        "philosophy", "religion", "mythology", "folklore", "legend", "fable",
        "comedy", "tragedy", "drama", "mystery", "thriller", "adventure", "romance",
        "horror", "fantasy", "dystopian", "utopian", "satirical", "allegory",
        "metaphor", "symbol", "imagery", "tone", "voice", "perspective", "style",
        "grammar", "syntax", "vocabulary", "etymology", "linguistics", "semantics",
        "typography", "calligraphy", "illustration", "diagram", "table", "figure",
        "appendix", "preface", "foreword", "dedication", "epigraph", "colophon",
        "section", "subsection", "clause", "heading", "subheading",
        "indent", "margin", "spacing", "alignment", "column",
        "ogma", "bookshelf", "spine", "shelf", "catalog", "lending", "borrowing",
        "librarian", "patron", "checkout", "return", "reserve", "interlibrary",
        "illuminated", "codex", "scroll", "tablet", "papyrus",
        "inscription", "translation", "commentary", "interpretation", "exegesis",
        "parable", "anecdote", "vignette", "sketch", "portrait", "silhouette",
        "century", "decade", "period", "era", "epoch", "chronology", "timeline",
        "geography", "cartography", "atlas", "map", "region", "territory",
        "chronicle", "annals", "record", "ledger", "register",
        "scripture", "liturgy", "hymn", "psalm", "prayer", "sermon", "homily",
        "almanac", "encyclopedia", "dictionary", "lexicon", "thesaurus", "gazetteer",
        "novella", "short", "story", "tale", "saga", "epic", "ballad",
        "sonnet", "ode", "elegy", "haiku",
        "rhetoric", "oratory", "discourse", "dialogue", "debate", "argument",
        "hypothesis", "experiment", "observation", "measurement", "calculation",
        "mathematics", "algebra", "geometry", "calculus", "statistics", "probability",
        "physics", "chemistry", "biology", "ecology", "astronomy", "geology",
        "archaeology", "anthropology", "sociology", "psychology", "economics",
        "politics", "diplomacy", "governance", "legislation", "regulation", "law",
        "justice", "ethics", "morality", "virtue", "wisdom", "truth",
        "beauty", "art", "music", "painting", "sculpture", "architecture", "design",
    ];

    private static string GeneratePage(int bookId, int pageNum)
    {
        var sb = new StringBuilder(WordsPerPage * 8);
        int seed = bookId * 1000 + pageNum;
        for (int i = 0; i < WordsPerPage; i++)
        {
            sb.Append(Vocab[(seed + i * 7 + i % 13) % Vocab.Length]);
            sb.Append(' ');
        }
        return sb.ToString().TrimEnd();
    }

    private static double Percentile(List<double> sorted, int pct)
    {
        if (sorted.Count == 0) return 0;
        double rank = (pct / 100.0) * (sorted.Count - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (rank - lower) * (sorted[upper] - sorted[lower]);
    }

    public static void Run()
    {
        Console.WriteLine("=== Spike 5: FTS5 External-Content Indexing Benchmark ===");
        Console.WriteLine($"Corpus: {NumBooks} books × {PagesPerBook} pages = {TotalRows} rows, ~{WordsPerPage} words/page");
        Console.WriteLine($"Platform: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine();

        // ── In-memory SQLite connection ──────────────────────────────────────
        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = ":memory:",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        using var conn = new SqliteConnection(connStr);
        conn.Open();

        // ── Content table ────────────────────────────────────────────────────
        Console.Write("Creating SearchChunks content table... ");
        Exec(conn, """
            CREATE TABLE SearchChunks (
                rowid    INTEGER PRIMARY KEY,
                book_id  INTEGER NOT NULL,
                page_num INTEGER NOT NULL,
                content  TEXT    NOT NULL
            );
            """);
        Console.WriteLine("done.");

        // ── FTS5 external-content virtual table (ADR-0006 pattern) ──────────
        Console.Write("Creating FTS5 virtual table (porter ascii tokenizer)... ");
        Exec(conn, """
            CREATE VIRTUAL TABLE SearchChunks_fts USING fts5(
                content,
                content='SearchChunks',
                content_rowid='rowid',
                tokenize='porter ascii'
            );
            """);
        Console.WriteLine("done.");

        // ── Triggers (ADR-0006 external-content pattern) ─────────────────────
        Exec(conn, """
            CREATE TRIGGER SearchChunks_ai AFTER INSERT ON SearchChunks BEGIN
                INSERT INTO SearchChunks_fts(rowid, content) VALUES (new.rowid, new.content);
            END;

            CREATE TRIGGER SearchChunks_ad AFTER DELETE ON SearchChunks BEGIN
                INSERT INTO SearchChunks_fts(SearchChunks_fts, rowid, content)
                    VALUES('delete', old.rowid, old.content);
            END;

            CREATE TRIGGER SearchChunks_au AFTER UPDATE ON SearchChunks BEGIN
                INSERT INTO SearchChunks_fts(SearchChunks_fts, rowid, content)
                    VALUES('delete', old.rowid, old.content);
                INSERT INTO SearchChunks_fts(rowid, content) VALUES (new.rowid, new.content);
            END;
            """);

        // ── Insert synthetic corpus ──────────────────────────────────────────
        Console.Write($"Inserting {TotalRows} rows (triggers populate FTS index)... ");
        var sw = Stopwatch.StartNew();
        using (var tx = conn.BeginTransaction())
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT INTO SearchChunks (book_id, page_num, content) VALUES (@b, @p, @c)";
            var pBook = insertCmd.Parameters.Add("@b", SqliteType.Integer);
            var pPage = insertCmd.Parameters.Add("@p", SqliteType.Integer);
            var pContent = insertCmd.Parameters.Add("@c", SqliteType.Text);

            for (int book = 1; book <= NumBooks; book++)
            {
                for (int page = 1; page <= PagesPerBook; page++)
                {
                    pBook.Value = book;
                    pPage.Value = page;
                    pContent.Value = GeneratePage(book, page);
                    insertCmd.ExecuteNonQuery();
                }
            }
            tx.Commit();
        }
        sw.Stop();
        Console.WriteLine($"done in {sw.ElapsedMilliseconds} ms.");

        // ── Verify row count ─────────────────────────────────────────────────
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM SearchChunks";
            var rowCount = (long)(cmd.ExecuteScalar() ?? 0L);
            Console.WriteLine($"Verified row count: {rowCount} (expected {TotalRows})");
        }

        // ── FTS5 integrity check ─────────────────────────────────────────────
        Console.Write("FTS5 integrity-check... ");
        Exec(conn, "INSERT INTO SearchChunks_fts(SearchChunks_fts) VALUES('integrity-check')");
        Console.WriteLine("PASSED.");
        Console.WriteLine();

        // ────────────────────────────────────────────────────────────────────
        // Benchmark queries
        // ────────────────────────────────────────────────────────────────────

        const int WarmupIterations = 5;
        const int BenchIterations = 50;

        var queries = new (string Label, string Sql)[]
        {
            ("Q01 single-term:ogma",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH 'ogma' LIMIT 50"),

            ("Q02 single-term:library",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH 'library' LIMIT 50"),

            ("Q03 single-term:philosophy",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH 'philosophy' LIMIT 50"),

            ("Q04 phrase:\"medieval illuminated\"",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH '\"medieval illuminated\"' LIMIT 50"),

            ("Q05 phrase:\"library book\"",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH '\"library book\"' LIMIT 50"),

            ("Q06 boolean-OR:history OR science",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH 'history OR science' LIMIT 50"),

            ("Q07 boolean-AND:author AND publisher",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH 'author AND publisher' LIMIT 50"),

            ("Q08 prefix:libr*",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH 'libr*' LIMIT 50"),

            ("Q09 multi-term:fiction mystery thriller",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH 'fiction mystery thriller' LIMIT 50"),

            ("Q10 AND:knowledge AND wisdom AND truth",
             "SELECT rowid FROM SearchChunks_fts WHERE content MATCH 'knowledge AND wisdom AND truth' LIMIT 50"),
        };

        Console.WriteLine($"{"Query",-42} {"Hits",5} {"P50 ms",8} {"P95 ms",8} {"Max ms",8}  Pass?");
        Console.WriteLine(new string('-', 82));

        double globalMaxP95 = 0;
        bool allPass = true;

        foreach (var (label, sql) in queries)
        {
            // Warm up (exercise query plan cache)
            for (int i = 0; i < WarmupIterations; i++)
            {
                using var wCmd = conn.CreateCommand();
                wCmd.CommandText = sql;
                using var wRdr = wCmd.ExecuteReader();
                while (wRdr.Read()) { }
            }

            // Benchmark
            var latencies = new List<double>(BenchIterations);
            int hits = 0;
            for (int i = 0; i < BenchIterations; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                using var bCmd = conn.CreateCommand();
                bCmd.CommandText = sql;
                int count = 0;
                using (var rdr = bCmd.ExecuteReader())
                {
                    while (rdr.Read()) { count++; }
                }
                double elapsed = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                latencies.Add(elapsed);
                if (i == 0) hits = count;
            }

            latencies.Sort();
            double p50 = Percentile(latencies, 50);
            double p95 = Percentile(latencies, 95);
            double max = latencies[^1];

            if (p95 > globalMaxP95) globalMaxP95 = p95;
            bool pass = p95 <= 500.0;
            if (!pass) allPass = false;

            Console.WriteLine($"{label,-42} {hits,5} {p50,8:F3} {p95,8:F3} {max,8:F3}  {(pass ? "PASS" : "FAIL")}");
        }

        Console.WriteLine(new string('-', 82));
        Console.WriteLine();
        Console.WriteLine($"Overall worst P95: {globalMaxP95:F3} ms");
        Console.WriteLine($"NFR-OGMA-004 threshold: 500 ms P95 warm");
        Console.WriteLine($"Spike 5 verdict: {(allPass ? "PASS" : "FAIL")}");
        Console.WriteLine();
        Console.WriteLine("NOTE: measurements are 'dev-box trend, not gated' (not W-REF-01 hardware).");
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
