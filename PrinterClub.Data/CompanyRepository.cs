using Microsoft.Data.Sqlite;

namespace PrinterClub.Data
{
    public class CompanyRepository
    {
        private readonly string _dbPath;

        public CompanyRepository(string dbPath)
        {
            _dbPath = dbPath;
        }

        private SqliteConnection Open()
        {
            var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            return conn;
        }

        // ========================
        // Query
        // ========================

        public List<CompanyLite> Search(string? number, string? cname, int limit = 200)
        {
            number = (number ?? "").Trim();
            cname = (cname ?? "").Trim();

            using var conn = Open();
            using var cmd = conn.CreateCommand();

            // 只抓 UI/新申請表會用到的欄位 + equipment_text
            // 欄位名稱全部對齊你 V3 schema
            if (!string.IsNullOrEmpty(number))
            {
                cmd.CommandText = @"
SELECT
  number,
  cname,
  ename,
  c_address,
  f_address,
  tax_id,
  money,
  area,
  company_reg_date,
  company_reg_prefix,
  company_reg_no,
  factory_reg_date,
  factory_reg_prefix,
  factory_reg_no,
  join_date,
  c_tel,
  c_fax,
  f_tel,
  f_fax,
  chief,
  title,
  sex,
  contact_person,
  extension,
  main_product,
  email,
  http,
  classify,
  area_class,
  equipment_text,
  v_date,
  v_date2
FROM companies
WHERE number = @number
LIMIT @limit;";
                cmd.Parameters.AddWithValue("@number", number);
                cmd.Parameters.AddWithValue("@limit", limit);
            }
            else if (!string.IsNullOrEmpty(cname))
            {
                cmd.CommandText = @"
SELECT
  number,
  cname,
  ename,
  c_address,
  f_address,
  tax_id,
  money,
  area,
  company_reg_date,
  company_reg_prefix,
  company_reg_no,
  factory_reg_date,
  factory_reg_prefix,
  factory_reg_no,
  join_date,
  c_tel,
  c_fax,
  f_tel,
  f_fax,
  chief,
  title,
  sex,
  contact_person,
  extension,
  main_product,
  email,
  http,
  classify,
  area_class,
  equipment_text,
  v_date,
  v_date2
FROM companies
WHERE cname LIKE @cname
ORDER BY number
LIMIT @limit;";
                cmd.Parameters.AddWithValue("@cname", $"%{cname}%");
                cmd.Parameters.AddWithValue("@limit", limit);
            }
            else
            {
                cmd.CommandText = @"
SELECT
  number,
  cname,
  ename,
  c_address,
  f_address,
  tax_id,
  money,
  area,
  company_reg_date,
  company_reg_prefix,
  company_reg_no,
  factory_reg_date,
  factory_reg_prefix,
  factory_reg_no,
  join_date,
  c_tel,
  c_fax,
  f_tel,
  f_fax,
  chief,
  title,
  sex,
  contact_person,
  extension,
  main_product,
  email,
  http,
  classify,
  area_class,
  equipment_text,
  v_date,
  v_date2
FROM companies
ORDER BY number
LIMIT @limit;";
                cmd.Parameters.AddWithValue("@limit", limit);
            }

            var list = new List<CompanyLite>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadCompany(reader));
            }
            return list;
        }

        public CompanyLite? GetByNumber(string number)
        {
            number = (number ?? "").Trim();
            if (string.IsNullOrEmpty(number)) return null;

            using var conn = Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
  number,
  cname,
  ename,
  c_address,
  f_address,
  tax_id,
  money,
  area,
  company_reg_date,
  company_reg_prefix,
  company_reg_no,
  factory_reg_date,
  factory_reg_prefix,
  factory_reg_no,
  join_date,
  c_tel,
  c_fax,
  f_tel,
  f_fax,
  chief,
  title,
  sex,
  contact_person,
  extension,
  main_product,
  email,
  http,
  classify,
  area_class,
  equipment_text,
  v_date,
  v_date2
FROM companies
WHERE number = @number
LIMIT 1;";
            cmd.Parameters.AddWithValue("@number", number);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return ReadCompany(reader);
        }
        public List<CompanyLite> SearchByNumberRange(string fromNumber, string toNumber, int limit = 2000)
        {
            fromNumber = (fromNumber ?? "").Trim();
            toNumber = (toNumber ?? "").Trim();
            if (string.IsNullOrEmpty(fromNumber) || string.IsNullOrEmpty(toNumber))
                throw new InvalidOperationException("範圍查詢需要起訖會籍編號。");

            using var conn = Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
  number,
  cname,
  ename,
  c_address,
  f_address,
  tax_id,
  money,
  area,
  company_reg_date,
  company_reg_prefix,
  company_reg_no,
  factory_reg_date,
  factory_reg_prefix,
  factory_reg_no,
  join_date,
  c_tel,
  c_fax,
  f_tel,
  f_fax,
  chief,
  title,
  sex,
  contact_person,
  extension,
  main_product,
  email,
  http,
  classify,
  area_class,
  v_date,
  v_date2,
  equipment_text
FROM companies
WHERE number >= @from AND number <= @to
ORDER BY number
LIMIT @limit;";

            cmd.Parameters.AddWithValue("@from", fromNumber);
            cmd.Parameters.AddWithValue("@to", toNumber);
            cmd.Parameters.AddWithValue("@limit", limit);

            var list = new List<CompanyLite>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadCompany(reader));

            return list;
        }

        // ========================
        // Create / Update / Delete
        // ========================

        public void Insert(CompanyLite model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var number = (model.Number ?? "").Trim();
            if (string.IsNullOrEmpty(number)) throw new InvalidOperationException("number 必填");

            using var conn = Open();

            // unique check
            using (var chk = conn.CreateCommand())
            {
                chk.CommandText = "SELECT 1 FROM companies WHERE number=@number LIMIT 1;";
                chk.Parameters.AddWithValue("@number", number);
                if (chk.ExecuteScalar() != null)
                    throw new InvalidOperationException($"number 已存在：{number}");
            }

            // 只寫入新申請表會用到的欄位；其他欄位不寫（維持 NULL/預設）
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO companies (
  number,
  cname,
  ename,
  c_address,
  f_address,
  tax_id,
  money,
  area,
  company_reg_date,
  company_reg_prefix,
  company_reg_no,
  factory_reg_date,
  factory_reg_prefix,
  factory_reg_no,
  join_date,
  c_tel,
  c_fax,
  f_tel,
  f_fax,
  chief,
  contact_person,
  extension,
  main_product,
  email,
  http,
  classify,
  area_class,
  equipment_text,
  updated_at
) VALUES (
  @number,
  @cname,
  @ename,
  @c_address,
  @f_address,
  @tax_id,
  @money,
  @area,
  @company_reg_date,
  @company_reg_prefix,
  @company_reg_no,
  @factory_reg_date,
  @factory_reg_prefix,
  @factory_reg_no,
  @join_date,
  @c_tel,
  @c_fax,
  @f_tel,
  @f_fax,
  @chief,
  @contact_person,
  @extension,
  @main_product,
  @email,
  @http,
  @classify,
  @area_class,
  @equipment_text,
  @updated_at
);";

            BindParams(cmd, model);
            cmd.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        public void Update(CompanyLite model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var number = (model.Number ?? "").Trim();
            if (string.IsNullOrEmpty(number)) throw new InvalidOperationException("number 必填");

            using var conn = Open();
            using var cmd = conn.CreateCommand();

            // 只更新你 UI 會管理的欄位，避免覆蓋舊欄位（title/pid/sex...等）
            cmd.CommandText = @"
UPDATE companies SET
  cname=@cname,
  ename=@ename,
  c_address=@c_address,
  f_address=@f_address,
  tax_id=@tax_id,
  money=@money,
  area=@area,
  company_reg_date=@company_reg_date,
  company_reg_prefix=@company_reg_prefix,
  company_reg_no=@company_reg_no,
  factory_reg_date=@factory_reg_date,
  factory_reg_prefix=@factory_reg_prefix,
  factory_reg_no=@factory_reg_no,
  join_date=@join_date,
  c_tel=@c_tel,
  c_fax=@c_fax,
  f_tel=@f_tel,
  f_fax=@f_fax,
  chief=@chief,
  contact_person=@contact_person,
  extension=@extension,
  main_product=@main_product,
  email=@email,
  http=@http,
  classify=@classify,
  area_class=@area_class,
  equipment_text=@equipment_text,
  updated_at=@updated_at
WHERE number=@number;";

            BindParams(cmd, model);
            cmd.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            var affected = cmd.ExecuteNonQuery();
            if (affected == 0)
                throw new InvalidOperationException($"找不到要更新的 number：{number}");
        }

        public void Delete(string number)
        {
            number = (number ?? "").Trim();
            if (string.IsNullOrEmpty(number)) return;

            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM companies WHERE number=@number;";
            cmd.Parameters.AddWithValue("@number", number);
            cmd.ExecuteNonQuery();
        }

        // ========================
        // Helpers
        // ========================

        private static void BindParams(SqliteCommand cmd, CompanyLite m)
        {
            cmd.Parameters.AddWithValue("@number", (m.Number ?? "").Trim());
            cmd.Parameters.AddWithValue("@cname", (m.CName ?? "").Trim());
            cmd.Parameters.AddWithValue("@ename", (m.EName ?? "").Trim());

            cmd.Parameters.AddWithValue("@c_address", (m.CAddress ?? "").Trim());
            cmd.Parameters.AddWithValue("@f_address", (m.FAddress ?? "").Trim());

            cmd.Parameters.AddWithValue("@tax_id", (m.TaxId ?? "").Trim());
            cmd.Parameters.AddWithValue("@money", (m.Money ?? "").Trim());
            cmd.Parameters.AddWithValue("@area", (m.Area ?? "").Trim());

            cmd.Parameters.AddWithValue("@company_reg_date", (m.CompanyRegDate ?? "").Trim());
            cmd.Parameters.AddWithValue("@company_reg_prefix", (m.CompanyRegPrefix ?? "").Trim());
            cmd.Parameters.AddWithValue("@company_reg_no", (m.CompanyRegNo ?? "").Trim());

            cmd.Parameters.AddWithValue("@factory_reg_date", (m.FactoryRegDate ?? "").Trim());
            cmd.Parameters.AddWithValue("@factory_reg_prefix", (m.FactoryRegPrefix ?? "").Trim());
            cmd.Parameters.AddWithValue("@factory_reg_no", (m.FactoryRegNo ?? "").Trim());

            // 你希望 CRUD 統一用 join_date
            cmd.Parameters.AddWithValue("@join_date", (m.ApplyDate ?? "").Trim());

            cmd.Parameters.AddWithValue("@c_tel", (m.CTel ?? "").Trim());
            cmd.Parameters.AddWithValue("@c_fax", (m.CFax ?? "").Trim());
            cmd.Parameters.AddWithValue("@f_tel", (m.FTel ?? "").Trim());
            cmd.Parameters.AddWithValue("@f_fax", (m.FFax ?? "").Trim());

            cmd.Parameters.AddWithValue("@chief", (m.Chief ?? "").Trim());
            cmd.Parameters.AddWithValue("@contact_person", (m.ContactPerson ?? "").Trim());
            cmd.Parameters.AddWithValue("@extension", (m.Extension ?? "").Trim());

            cmd.Parameters.AddWithValue("@main_product", (m.MainProduct ?? "").Trim());
            cmd.Parameters.AddWithValue("@email", (m.Email ?? "").Trim());
            cmd.Parameters.AddWithValue("@http", (m.Http ?? "").Trim());

            cmd.Parameters.AddWithValue("@classify", (m.Classify ?? "").Trim());
            cmd.Parameters.AddWithValue("@area_class", (m.AreaClass ?? "").Trim());

            cmd.Parameters.AddWithValue("@equipment_text", (m.EquipmentText ?? "").Trim());
        }

        private static CompanyLite ReadCompany(SqliteDataReader r)
        {
            string S(string col) => r[col] == DBNull.Value ? "" : Convert.ToString(r[col]) ?? "";

            return new CompanyLite
            {
                Number = S("number"),
                CName = S("cname"),
                EName = S("ename"),

                CAddress = S("c_address"),
                FAddress = S("f_address"),

                TaxId = S("tax_id"),
                Money = S("money"),
                Area = S("area"),

                CompanyRegDate = S("company_reg_date"),
                CompanyRegPrefix = S("company_reg_prefix"),
                CompanyRegNo = S("company_reg_no"),

                FactoryRegDate = S("factory_reg_date"),
                FactoryRegPrefix = S("factory_reg_prefix"),
                FactoryRegNo = S("factory_reg_no"),

                ApplyDate = S("join_date"),

                CTel = S("c_tel"),
                CFax = S("c_fax"),
                FTel = S("f_tel"),
                FFax = S("f_fax"),

                Chief = S("chief"),
                ContactPerson = S("contact_person"),
                Extension = S("extension"),

                MainProduct = S("main_product"),
                Email = S("email"),
                Http = S("http"),

                Classify = S("classify"),
                AreaClass = S("area_class"),

                EquipmentText = S("equipment_text"),
                VDate = S("v_date"),
                VDate2 = S("v_date2"),
                Title = S("title"),
                Sex = S("sex"),
            };
        }

        public static string ResolveDefaultDbPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var path1 = Path.Combine(baseDir, "printerClub.db");
            if (File.Exists(path1)) return path1;

            var dir = new DirectoryInfo(baseDir);
            for (int i = 0; i < 4 && dir != null; i++)
            {
                var cand = Path.Combine(dir.FullName, "printerClub.db");
                if (File.Exists(cand)) return cand;
                dir = dir.Parent;
            }

            return path1;
        }
    }
}
