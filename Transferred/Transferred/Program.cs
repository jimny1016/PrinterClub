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
            // [0] dataRoot (folder contains sourceData/company.txt, sourceData/Rcompany.txt, sourceData/com/)
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

            EnsureSchemaAndMigrateIfNeeded(conn);

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
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var csproj = dir.GetFiles("*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (csproj != null) return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    // =========================
    // Schema / Migration
    // =========================

    static void EnsureSchemaAndMigrateIfNeeded(SqliteConnection conn)
    {
        // companies:
        // - v1: number + f01..f42 + equipment_text + source_line + updated_at
        // - v2: number + meaningful columns + equipment_text + source_line + updated_at
        if (!TableExists(conn, "companies"))
        {
            CreateCompaniesV2(conn);
        }
        else
        {
            var cols = GetTableColumns(conn, "companies");
            bool isV2 = cols.Contains("cname");
            bool isV1 = cols.Contains("f01");

            if (isV1 && !isV2)
            {
                Console.WriteLine("偵測到 companies 為第一階段欄位（f01..f42），開始 migration 到第二階段命名...");
                MigrateCompaniesV1ToV2(conn);
                Console.WriteLine("companies migration 完成");
            }
            else if (!isV2)
            {
                // 存在但又不像 v1/v2：保守處理
                Console.WriteLine("警告：companies 表已存在，但欄位不符合預期（既不是 v1 也不是 v2）。請檢查 DB。");
            }
        }

        // rcompanies:
        if (!TableExists(conn, "rcompanies"))
        {
            CreateRCompaniesV2(conn);
        }
        else
        {
            var cols = GetTableColumns(conn, "rcompanies");
            bool isV2 = cols.Contains("name") && cols.Contains("zip_code");
            bool isV1 = cols.Contains("f01");

            if (isV1 && !isV2)
            {
                Console.WriteLine("偵測到 rcompanies 為第一階段欄位（f01..f08），開始 migration 到第二階段命名...");
                MigrateRCompaniesV1ToV2(conn);
                Console.WriteLine("rcompanies migration 完成");
            }
            else if (!isV2)
            {
                Console.WriteLine("警告：rcompanies 表已存在，但欄位不符合預期（既不是 v1 也不是 v2）。請檢查 DB。");
            }
        }

        CreateIndexes(conn);
    }

    static void CreateCompaniesV2(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS companies (
  number TEXT PRIMARY KEY,

  cname TEXT,
  ename TEXT,
  c_address TEXT,
  f_address TEXT,
  tax_id TEXT,
  money TEXT,
  area TEXT,

  c1 TEXT,
  c2 TEXT,
  c3 TEXT,

  f1 TEXT,
  f2 TEXT,
  f3 TEXT,

  c_date TEXT,
  join_date TEXT,
  re_date TEXT,

  c_tel TEXT,
  c_fax TEXT,
  f_tel TEXT,
  f_fax TEXT,

  chief TEXT,
  title TEXT,
  pid TEXT,
  sex TEXT,
  p_address TEXT,

  ca TEXT,
  cn TEXT,
  c_area TEXT,

  v_date TEXT,
  v_date2 TEXT,

  chairman TEXT,
  chairman_e TEXT,
  gm TEXT,
  gm_e TEXT,

  main_product TEXT,
  main_product_e TEXT,

  email TEXT,
  http TEXT,
  address_e TEXT,

  classify TEXT,
  area_class TEXT,

  equipment_text TEXT,
  source_line TEXT,
  updated_at TEXT
);
";
        cmd.ExecuteNonQuery();
    }

    static void CreateRCompaniesV2(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS rcompanies (
  code TEXT PRIMARY KEY,
  name TEXT,
  chief TEXT,
  newsletter_copies TEXT,
  address TEXT,
  comment TEXT,
  zip_code TEXT,
  source_line TEXT,
  updated_at TEXT
);
";
        cmd.ExecuteNonQuery();
    }

    static void CreateIndexes(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE INDEX IF NOT EXISTS idx_companies_cname ON companies(cname);
CREATE INDEX IF NOT EXISTS idx_companies_area ON companies(area);
CREATE INDEX IF NOT EXISTS idx_companies_classify ON companies(classify);
CREATE INDEX IF NOT EXISTS idx_companies_area_class ON companies(area_class);

CREATE INDEX IF NOT EXISTS idx_rcompanies_name ON rcompanies(name);
";
        cmd.ExecuteNonQuery();
    }

    static void MigrateCompaniesV1ToV2(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();

        // 1) create new table
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS companies_new (
  number TEXT PRIMARY KEY,

  cname TEXT,
  ename TEXT,
  c_address TEXT,
  f_address TEXT,
  tax_id TEXT,
  money TEXT,
  area TEXT,

  c1 TEXT,
  c2 TEXT,
  c3 TEXT,

  f1 TEXT,
  f2 TEXT,
  f3 TEXT,

  c_date TEXT,
  join_date TEXT,
  re_date TEXT,

  c_tel TEXT,
  c_fax TEXT,
  f_tel TEXT,
  f_fax TEXT,

  chief TEXT,
  title TEXT,
  pid TEXT,
  sex TEXT,
  p_address TEXT,

  ca TEXT,
  cn TEXT,
  c_area TEXT,

  v_date TEXT,
  v_date2 TEXT,

  chairman TEXT,
  chairman_e TEXT,
  gm TEXT,
  gm_e TEXT,

  main_product TEXT,
  main_product_e TEXT,

  email TEXT,
  http TEXT,
  address_e TEXT,

  classify TEXT,
  area_class TEXT,

  equipment_text TEXT,
  source_line TEXT,
  updated_at TEXT
);
";
            cmd.ExecuteNonQuery();
        }

        // 2) copy data (v1: number + f01..f42)
        // 注意：v1 的 f01 其實就是 number 再存一次，所以用 COALESCE(number, f01)
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO companies_new (
  number,
  cname, ename, c_address, f_address, tax_id, money, area,
  c1, c2, c3,
  f1, f2, f3,
  c_date, join_date, re_date,
  c_tel, c_fax, f_tel, f_fax,
  chief, title, pid, sex, p_address,
  ca, cn, c_area,
  v_date, v_date2,
  chairman, chairman_e, gm, gm_e,
  main_product, main_product_e,
  email, http, address_e,
  classify, area_class,
  equipment_text, source_line, updated_at
)
SELECT
  COALESCE(number, f01) AS number,

  f02 AS cname,
  f03 AS ename,
  f04 AS c_address,
  f05 AS f_address,
  f06 AS tax_id,
  f07 AS money,
  f08 AS area,

  f09 AS c1,
  f10 AS c2,
  f11 AS c3,

  f12 AS f1,
  f13 AS f2,
  f14 AS f3,

  f15 AS c_date,
  f16 AS join_date,
  f17 AS re_date,

  f18 AS c_tel,
  f19 AS c_fax,
  f20 AS f_tel,
  f21 AS f_fax,

  f22 AS chief,
  f23 AS title,
  f24 AS pid,
  f25 AS sex,
  f26 AS p_address,

  f27 AS ca,
  f28 AS cn,
  f29 AS c_area,

  f30 AS v_date,
  f31 AS v_date2,

  f32 AS chairman,
  f33 AS chairman_e,
  f34 AS gm,
  f35 AS gm_e,

  f36 AS main_product,
  f37 AS main_product_e,

  f38 AS email,
  f39 AS http,
  f40 AS address_e,

  f41 AS classify,
  f42 AS area_class,

  equipment_text,
  source_line,
  updated_at
FROM companies;
";
            cmd.ExecuteNonQuery();
        }

        // 3) drop old, rename
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
DROP TABLE companies;
ALTER TABLE companies_new RENAME TO companies;
";
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    static void MigrateRCompaniesV1ToV2(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS rcompanies_new (
  code TEXT PRIMARY KEY,
  name TEXT,
  chief TEXT,
  newsletter_copies TEXT,
  address TEXT,
  comment TEXT,
  zip_code TEXT,
  source_line TEXT,
  updated_at TEXT
);
";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            // v1 的 f01..f08：實際上舊 Java 只用到 0..6，最後常是空
            cmd.CommandText = @"
INSERT INTO rcompanies_new (code, name, chief, newsletter_copies, address, comment, zip_code, source_line, updated_at)
SELECT
  code,
  f02 AS name,
  f03 AS chief,
  f04 AS newsletter_copies,
  f05 AS address,
  f06 AS comment,
  f07 AS zip_code,
  source_line,
  updated_at
FROM rcompanies;
";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
DROP TABLE rcompanies;
ALTER TABLE rcompanies_new RENAME TO rcompanies;
";
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    static bool TableExists(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name LIMIT 1;";
        cmd.Parameters.AddWithValue("@name", tableName);
        var result = cmd.ExecuteScalar();
        return result != null && result != DBNull.Value;
    }

    static HashSet<string> GetTableColumns(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            var colName = reader.GetString(reader.GetOrdinal("name"));
            set.Add(colName);
        }
        return set;
    }

    // =========================
    // Import (v2 columns)
    // =========================

    static int ImportCompanies(SqliteConnection conn, SqliteTransaction tx, string companyPath, string comDir, Encoding big5)
    {
        int count = 0;

        using var upsert = conn.CreateCommand();
        upsert.Transaction = tx;

        var columns = new[]
        {
            "number",
            "cname","ename","c_address","f_address","tax_id","money","area",
            "c1","c2","c3",
            "f1","f2","f3",
            "c_date","join_date","re_date",
            "c_tel","c_fax","f_tel","f_fax",
            "chief","title","pid","sex","p_address",
            "ca","cn","c_area",
            "v_date","v_date2",
            "chairman","chairman_e","gm","gm_e",
            "main_product","main_product_e",
            "email","http","address_e",
            "classify","area_class",
            "equipment_text","source_line","updated_at"
        };

        var paramNames = columns.Select(c => $"@{c}").ToArray();

        var updateSet = string.Join(", ", columns
            .Where(c => c != "number")
            .Select(c => $"{c}=excluded.{c}"));

        upsert.CommandText = $@"
INSERT INTO companies ({string.Join(",", columns)})
VALUES ({string.Join(",", paramNames)})
ON CONFLICT(number) DO UPDATE SET {updateSet};
";

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

            var equipment = ReadEquipmentText(comDir, number, big5) ?? "";

            // 依照 Java comPool 的 index 對應
            // 0..41 對應 number..areaClass
            upsert.Parameters["@number"].Value = number;

            upsert.Parameters["@cname"].Value = parts[1];
            upsert.Parameters["@ename"].Value = parts[2];
            upsert.Parameters["@c_address"].Value = parts[3];
            upsert.Parameters["@f_address"].Value = parts[4];
            upsert.Parameters["@tax_id"].Value = parts[5];
            upsert.Parameters["@money"].Value = parts[6];
            upsert.Parameters["@area"].Value = parts[7];

            upsert.Parameters["@c1"].Value = parts[8];
            upsert.Parameters["@c2"].Value = parts[9];
            upsert.Parameters["@c3"].Value = parts[10];

            upsert.Parameters["@f1"].Value = parts[11];
            upsert.Parameters["@f2"].Value = parts[12];
            upsert.Parameters["@f3"].Value = parts[13];

            upsert.Parameters["@c_date"].Value = parts[14];
            upsert.Parameters["@join_date"].Value = parts[15];
            upsert.Parameters["@re_date"].Value = parts[16];

            upsert.Parameters["@c_tel"].Value = parts[17];
            upsert.Parameters["@c_fax"].Value = parts[18];
            upsert.Parameters["@f_tel"].Value = parts[19];
            upsert.Parameters["@f_fax"].Value = parts[20];

            upsert.Parameters["@chief"].Value = parts[21];
            upsert.Parameters["@title"].Value = parts[22];
            upsert.Parameters["@pid"].Value = parts[23];
            upsert.Parameters["@sex"].Value = parts[24];
            upsert.Parameters["@p_address"].Value = parts[25];

            upsert.Parameters["@ca"].Value = parts[26];
            upsert.Parameters["@cn"].Value = parts[27];
            upsert.Parameters["@c_area"].Value = parts[28];

            upsert.Parameters["@v_date"].Value = parts[29];
            upsert.Parameters["@v_date2"].Value = parts[30];

            upsert.Parameters["@chairman"].Value = parts[31];
            upsert.Parameters["@chairman_e"].Value = parts[32];
            upsert.Parameters["@gm"].Value = parts[33];
            upsert.Parameters["@gm_e"].Value = parts[34];

            upsert.Parameters["@main_product"].Value = parts[35];
            upsert.Parameters["@main_product_e"].Value = parts[36];

            upsert.Parameters["@email"].Value = parts[37];
            upsert.Parameters["@http"].Value = parts[38];
            upsert.Parameters["@address_e"].Value = parts[39];

            upsert.Parameters["@classify"].Value = parts[40];
            upsert.Parameters["@area_class"].Value = parts[41];

            upsert.Parameters["@equipment_text"].Value = equipment;
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
INSERT INTO rcompanies (code, name, chief, newsletter_copies, address, comment, zip_code, source_line, updated_at)
VALUES (@code,@name,@chief,@newsletter_copies,@address,@comment,@zip_code,@source_line,@updated_at)
ON CONFLICT(code) DO UPDATE SET
name=excluded.name,
chief=excluded.chief,
newsletter_copies=excluded.newsletter_copies,
address=excluded.address,
comment=excluded.comment,
zip_code=excluded.zip_code,
source_line=excluded.source_line,
updated_at=excluded.updated_at;
";

        upsert.Parameters.Add(new SqliteParameter("@code", ""));
        upsert.Parameters.Add(new SqliteParameter("@name", ""));
        upsert.Parameters.Add(new SqliteParameter("@chief", ""));
        upsert.Parameters.Add(new SqliteParameter("@newsletter_copies", ""));
        upsert.Parameters.Add(new SqliteParameter("@address", ""));
        upsert.Parameters.Add(new SqliteParameter("@comment", ""));
        upsert.Parameters.Add(new SqliteParameter("@zip_code", ""));
        upsert.Parameters.Add(new SqliteParameter("@source_line", ""));
        upsert.Parameters.Add(new SqliteParameter("@updated_at", ""));

        foreach (var line in ReadLines(rcompanyPath, big5))
        {
            var raw = line;
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var parts = raw.Split('|');

            // 舊檔通常是 7 欄 + 最後一個空欄（尾巴多一個 |），所以可能是 7 或 8
            if (parts.Length < 7)
            {
                Console.WriteLine($"[WARN] Rcompany.txt 欄位數不足（至少要 7）：{parts.Length}，line={raw}");
                continue;
            }

            var code = parts[0].Trim();
            if (string.IsNullOrEmpty(code))
                continue;

            upsert.Parameters["@code"].Value = code;
            upsert.Parameters["@name"].Value = parts.ElementAtOrDefault(1) ?? "";
            upsert.Parameters["@chief"].Value = parts.ElementAtOrDefault(2) ?? "";
            upsert.Parameters["@newsletter_copies"].Value = parts.ElementAtOrDefault(3) ?? "";
            upsert.Parameters["@address"].Value = parts.ElementAtOrDefault(4) ?? "";
            upsert.Parameters["@comment"].Value = parts.ElementAtOrDefault(5) ?? "";
            upsert.Parameters["@zip_code"].Value = parts.ElementAtOrDefault(6) ?? "";
            upsert.Parameters["@source_line"].Value = raw;
            upsert.Parameters["@updated_at"].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            upsert.ExecuteNonQuery();
            count++;
        }

        return count;
    }

    // =========================
    // IO helpers
    // =========================

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

        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0) return "";

        int end = bytes.Length;
        while (end > 0 && bytes[end - 1] == 0x00) end--;

        if (end <= 0) return "";

        var trimmed = new byte[end];
        Buffer.BlockCopy(bytes, 0, trimmed, 0, end);

        var s = big5.GetString(trimmed);
        s = s.Replace("\0", "").Trim();
        return s;
    }
}
