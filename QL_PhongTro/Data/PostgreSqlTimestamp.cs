using System.Globalization;

namespace QL_PhongTro.Data;

public static class PostgreSqlTimestamp
{
    public static string Write(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);

    public static DateTime Read(string value)
    {
        // Match Microsoft.Data.Sqlite 10's offset handling. The built-in EF
        // DateTimeToStringConverter leaves offset-bearing strings in local time,
        // whereas SqliteDataReader.GetDateTime normalizes them to UTC.
        var parsed = DateTime.Parse(value, CultureInfo.InvariantCulture);
        return parsed.Kind == DateTimeKind.Local ? parsed.ToUniversalTime() : parsed;
    }
}
