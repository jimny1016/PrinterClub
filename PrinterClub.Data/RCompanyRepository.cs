using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace PrinterClub.Data
{
    public class RCompanyRepository
    {
        private readonly string _dbPath;

        public RCompanyRepository(string dbPath)
        {
            _dbPath = dbPath;
        }

        private SqliteConnection Open()
        {
            if (string.IsNullOrWhiteSpace(_dbPath))
                throw new InvalidOperationException("DB path is empty.");

            if (!File.Exists(_dbPath))
                throw new FileNotFoundException("找不到資料庫檔案", _dbPath);

            var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            return conn;
        }

        public List<RCompanyLite> Search(string codeExact, string nameLike, int limit = 200)
        {
            codeExact ??= "";
            nameLike ??= "";

            using var conn = Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
  code,
  IFNULL(name,'') AS name,
  IFNULL(chief,'') AS chief
FROM rcompanies
WHERE
  (@code='' OR code=@code)
  AND (@name='' OR name LIKE '%' || @name || '%')
ORDER BY code
LIMIT @limit;
";
            cmd.Parameters.AddWithValue("@code", codeExact.Trim());
            cmd.Parameters.AddWithValue("@name", nameLike.Trim());
            cmd.Parameters.AddWithValue("@limit", limit);

            var list = new List<RCompanyLite>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new RCompanyLite
                {
                    Code = r.GetString(0),
                    Name = r.GetString(1),
                    Chief = r.GetString(2),
                });
            }
            return list;
        }

        public RCompanyLite? GetByCode(string code)
        {
            code ??= "";

            using var conn = Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
  code,
  IFNULL(name,'') AS name,
  IFNULL(chief,'') AS chief,
  IFNULL(newsletter_copies,'') AS newsletter_copies,
  IFNULL(address,'') AS address,
  IFNULL(comment,'') AS comment,
  IFNULL(zip_code,'') AS zip_code,
  IFNULL(source_line,'') AS source_line,
  IFNULL(updated_at,'') AS updated_at
FROM rcompanies
WHERE code=@code
LIMIT 1;
";
            cmd.Parameters.AddWithValue("@code", code.Trim());

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new RCompanyLite
            {
                Code = r.GetString(0),
                Name = r.GetString(1),
                Chief = r.GetString(2),
                NewsletterCopies = r.GetString(3),
                Address = r.GetString(4),
                Comment = r.GetString(5),
                ZipCode = r.GetString(6),
                SourceLine = r.GetString(7),
                UpdatedAt = r.GetString(8),
            };
        }

        public void Insert(RCompanyLite m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (string.IsNullOrWhiteSpace(m.Code)) throw new InvalidOperationException("代碼（code）必填。");

            using var conn = Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
INSERT INTO rcompanies
(code, name, chief, newsletter_copies, address, comment, zip_code, source_line, updated_at)
VALUES
(@code,@name,@chief,@newsletter,@address,@comment,@zip,@source_line,@updated_at);
";

            BindParams(cmd, m, isInsert: true);
            cmd.ExecuteNonQuery();
        }

        public void Update(RCompanyLite m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (string.IsNullOrWhiteSpace(m.Code)) throw new InvalidOperationException("代碼（code）必填。");

            using var conn = Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
UPDATE rcompanies
SET
  name=@name,
  chief=@chief,
  newsletter_copies=@newsletter,
  address=@address,
  comment=@comment,
  zip_code=@zip,
  source_line=@source_line,
  updated_at=@updated_at
WHERE code=@code;
";

            BindParams(cmd, m, isInsert: false);
            cmd.ExecuteNonQuery();
        }

        public void Delete(string code)
        {
            code ??= "";

            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM rcompanies WHERE code=@code;";
            cmd.Parameters.AddWithValue("@code", code.Trim());
            cmd.ExecuteNonQuery();
        }

        private static void BindParams(SqliteCommand cmd, RCompanyLite m, bool isInsert)
        {
            cmd.Parameters.AddWithValue("@code", (m.Code ?? "").Trim());
            cmd.Parameters.AddWithValue("@name", (m.Name ?? "").Trim());
            cmd.Parameters.AddWithValue("@chief", (m.Chief ?? "").Trim());
            cmd.Parameters.AddWithValue("@newsletter", (m.NewsletterCopies ?? "").Trim());
            cmd.Parameters.AddWithValue("@address", (m.Address ?? "").Trim());
            cmd.Parameters.AddWithValue("@comment", (m.Comment ?? "").Trim());
            cmd.Parameters.AddWithValue("@zip", (m.ZipCode ?? "").Trim());
            cmd.Parameters.AddWithValue("@source_line", (m.SourceLine ?? "").Trim());

            // updated_at：這裡統一由 repo 填（避免 UI 忘記）
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var updatedAt = string.IsNullOrWhiteSpace(m.UpdatedAt) ? now : m.UpdatedAt.Trim();
            cmd.Parameters.AddWithValue("@updated_at", isInsert ? now : updatedAt);
        }
    }
}