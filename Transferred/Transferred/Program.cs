// Program.cs
// dotnet add package Microsoft.Data.Sqlite

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;

internal class Program
{
    static int Main(string[] args)
    {
        try
        {
            // args:
            // [0] dataRoot (folder contains company.txt, Rcompany.txt, com/)
            // [1] outputDbPath (default: output.db)
            var dataRoot = args.Length >= 1 ? args[0] : FindProjectRoot();
            var outputDb = args.Length >= 2 ? args[1] : Path.Combine(dataRoot, "output.db");

            var companyPath = Path.Combine(dataRoot, "sourceData", "company.txt");
            var rcompanyPath = Path.Combine(dataRoot, "sourceData", "Rcompany.txt");
            var comDir = Path.Combine(dataRoot, "sourceData", "com");

            if (!File.Exists(companyPath))
                throw new FileNotFoundException($"找不到 company.txt: {companyPath}");
            if (!File.Exists(rcompanyPath))
                Console.WriteLine($"提醒：找不到 Rcompany.txt（可忽略）: {rcompanyPath}");
            if (!Directory.Exists(comDir))
                Console.WriteLine($"提醒：找不到 com/ 資料夾（可忽略）: {comDir}");

            Console.WriteLine($"資料根目錄: {dataRoot}");
            Console.WriteLine($"輸出 DB: {outputDb}");

            Directory.CreateDirectory(Path.GetDirectoryName(outputDb)!);

            // Big5 (CP950)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var big5 = Encoding.GetEncoding(950);

            using var conn = new SqliteConnection($"Data Source={outputDb}");
            conn.Open();

            CreateTables(conn);

            using var tx = conn.BeginTransaction();

            int companyCount = ImportCompanies(conn, tx, companyPath, comDir, big5);
            int rcompanyCount = File.Exists(rcompanyPath)
                ? ImportRCompanies(conn, tx, rcompanyPath, big5)
                : 0;

            tx.Commit();

            Console.WriteLine($"完成：companies 匯入/更新 {companyCount} 筆");
            Console.WriteLine($"完成：rcompanies 匯入/更新 {rcompanyCount} 筆");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    static string FindProjectRoot()
    {
        // 從執行檔所在資料夾開始往上找 *.csproj
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var csproj = dir.GetFiles("*.csproj", SearchOption.TopDirectoryOnly)
                            .FirstOrDefault();
            if (csproj != null)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // 找不到就退回目前工作目錄（至少不會死）
        return Directory.GetCurrentDirectory();
    }
    static void CreateTables(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();

        // companies: number + 42 fields + equipment_text + source_line
        var companyFields = string.Join(",\n", Enumerable.Range(1, 42).Select(i => $"  f{i:00} TEXT"));
        cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS companies (
  number TEXT PRIMARY KEY,
{companyFields},
  equipment_text TEXT,
  source_line TEXT,
  updated_at TEXT
);

CREATE TABLE IF NOT EXISTS rcompanies (
  code TEXT PRIMARY KEY,
  f01 TEXT,
  f02 TEXT,
  f03 TEXT,
  f04 TEXT,
  f05 TEXT,
  f06 TEXT,
  f07 TEXT,
  f08 TEXT,
  source_line TEXT,
  updated_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_companies_f02 ON companies(f02);
CREATE INDEX IF NOT EXISTS idx_companies_f08 ON companies(f08);
";
        cmd.ExecuteNonQuery();
    }

    static int ImportCompanies(SqliteConnection conn, SqliteTransaction tx, string companyPath, string comDir, Encoding big5)
    {
        int count = 0;

        using var upsert = conn.CreateCommand();
        upsert.Transaction = tx;

        // Prepare UPSERT SQL
        var columns = new List<string> { "number" };
        columns.AddRange(Enumerable.Range(1, 42).Select(i => $"f{i:00}"));
        columns.AddRange(new[] { "equipment_text", "source_line", "updated_at" });

        var paramNames = columns.Select(c => $"@{c}").ToList();

        var updateSet = string.Join(", ", columns
            .Where(c => c != "number")
            .Select(c => $"{c}=excluded.{c}"));

        upsert.CommandText = $@"
INSERT INTO companies ({string.Join(",", columns)})
VALUES ({string.Join(",", paramNames)})
ON CONFLICT(number) DO UPDATE SET {updateSet};
";

        // Pre-create parameters
        foreach (var c in columns)
            upsert.Parameters.Add(new SqliteParameter($"@{c}", ""));

        foreach (var line in ReadLines(companyPath, big5))
        {
            var raw = line;
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var parts = raw.Split('|');
            if (parts.Length != 42)
            {
                Console.WriteLine($"[WARN] company.txt 欄位數不是 42：{parts.Length}，line={raw}");
                continue;
            }

            var number = parts[0].Trim();
            if (string.IsNullOrEmpty(number))
            {
                Console.WriteLine($"[WARN] company.txt number 空白，跳過，line={raw}");
                continue;
            }

            var equipment = ReadEquipmentText(comDir, number, big5);

            upsert.Parameters["@number"].Value = number;

            for (int i = 1; i <= 42; i++)
            {
                var v = parts[i - 1];
                upsert.Parameters[$"@f{i:00}"].Value = v;
            }

            upsert.Parameters["@equipment_text"].Value = equipment ?? "";
            upsert.Parameters["@source_line"].Value = raw;
            upsert.Parameters["@updated_at"].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            upsert.ExecuteNonQuery();
            count++;

            if (count % 200 == 0)
                Console.WriteLine($"companies 已處理 {count} 筆...");
        }

        return count;
    }

    static int ImportRCompanies(SqliteConnection conn, SqliteTransaction tx, string rcompanyPath, Encoding big5)
    {
        int count = 0;

        using var upsert = conn.CreateCommand();
        upsert.Transaction = tx;

        upsert.CommandText = @"
INSERT INTO rcompanies (code,f01,f02,f03,f04,f05,f06,f07,f08,source_line,updated_at)
VALUES (@code,@f01,@f02,@f03,@f04,@f05,@f06,@f07,@f08,@source_line,@updated_at)
ON CONFLICT(code) DO UPDATE SET
f01=excluded.f01,
f02=excluded.f02,
f03=excluded.f03,
f04=excluded.f04,
f05=excluded.f05,
f06=excluded.f06,
f07=excluded.f07,
f08=excluded.f08,
source_line=excluded.source_line,
updated_at=excluded.updated_at;
";

        upsert.Parameters.Add(new SqliteParameter("@code", ""));
        for (int i = 1; i <= 8; i++)
            upsert.Parameters.Add(new SqliteParameter($"@f{i:00}", ""));
        upsert.Parameters.Add(new SqliteParameter("@source_line", ""));
        upsert.Parameters.Add(new SqliteParameter("@updated_at", ""));

        foreach (var line in ReadLines(rcompanyPath, big5))
        {
            var raw = line;
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var parts = raw.Split('|');
            if (parts.Length != 8)
            {
                Console.WriteLine($"[WARN] Rcompany.txt 欄位數不是 8：{parts.Length}，line={raw}");
                continue;
            }

            var code = parts[0].Trim();
            if (string.IsNullOrEmpty(code))
                continue;

            upsert.Parameters["@code"].Value = code;
            for (int i = 1; i <= 8; i++)
                upsert.Parameters[$"@f{i:00}"].Value = parts[i - 1];

            upsert.Parameters["@source_line"].Value = raw;
            upsert.Parameters["@updated_at"].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            upsert.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    static IEnumerable<string> ReadLines(string path, Encoding enc)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs, enc, detectEncodingFromByteOrderMarks: false);
        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (line == null) yield break;
            yield return line;
        }
    }

    static string? ReadEquipmentText(string comDir, string number, Encoding big5)
    {
        if (!Directory.Exists(comDir)) return null;

        var path = Path.Combine(comDir, $"{number}.txt");
        if (!File.Exists(path)) return null;

        // com/*.txt 可能有 \0 padding：要用 binary 讀再 trim
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0) return "";

        // trim end null bytes
        int end = bytes.Length;
        while (end > 0 && bytes[end - 1] == 0x00) end--;

        if (end <= 0) return "";

        var trimmed = new byte[end];
        Buffer.BlockCopy(bytes, 0, trimmed, 0, end);

        // 有些檔案可能是純 0（或含很多 0）
        var s = big5.GetString(trimmed);
        s = s.Replace("\0", "").Trim();
        return s;
    }
}
