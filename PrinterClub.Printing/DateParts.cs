namespace PrinterClub.Printing;

internal static class DateParts
{
    public static (int y, int m, int d) TryParseRocOrIso(string? s)
    {
        s = (s ?? "").Trim();
        if (string.IsNullOrEmpty(s)) return (0, 0, 0);

        // ROC: 107.12.31
        var dot = s.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (dot.Length == 3 &&
            int.TryParse(dot[0], out var ry) &&
            int.TryParse(dot[1], out var rm) &&
            int.TryParse(dot[2], out var rd))
        {
            return (ry, rm, rd);
        }

        // ISO: yyyy-MM-dd or yyyy/MM/dd
        var normalized = s.Replace('/', '-');
        var dash = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (dash.Length >= 3 &&
            int.TryParse(dash[0], out var y) &&
            int.TryParse(dash[1], out var m) &&
            int.TryParse(dash[2], out var d))
        {
            // 若是西元，轉 ROC（Java 那套是印民國中文數字）
            if (y > 1911) y -= 1911;
            return (y, m, d);
        }

        return (0, 0, 0);
    }
}
