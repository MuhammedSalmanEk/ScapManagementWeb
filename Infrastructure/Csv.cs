using System.Globalization;
using System.Text;

namespace ScrapManagement.Web.Infrastructure;

public static class Csv
{
    public static byte[] Build(IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true); // BOM so Excel reads it correctly
        return utf8.GetPreamble().Concat(utf8.GetBytes(sb.ToString())).ToArray();
    }

    public static string Num(decimal value, int decimals = 2) =>
        value.ToString("F" + decimals, CultureInfo.InvariantCulture);

    private static string Escape(string? value)
    {
        value ??= "";
        // Neutralise spreadsheet formula injection from user-entered text.
        if (value.Length > 0 && "=+-@".Contains(value[0]) && !decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            value = "'" + value;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
