using Microsoft.Data.Sqlite;
using QL_PhongTro.Data;
using Xunit;

public class PostgreSqlTimestampTests
{
    [Theory]
    [InlineData("2026-10-22 16:12:34.1234567")]
    [InlineData("2026-10-22T16:12:34.1234567Z")]
    [InlineData("2026-10-22T16:12:34.1234567+00:00")]
    [InlineData("2026-10-22T23:12:34.1234567+07:00")]
    [InlineData("2026-10-22 16:12:34")]
    [InlineData("2026-10-22")]
    public void Read_matches_current_Sqlite_including_offset_and_100ns(string original)
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "SELECT $value";
        command.Parameters.AddWithValue("$value", original);
        using var reader = command.ExecuteReader(); reader.Read();
        var expected = reader.GetDateTime(0);
        var actual = PostgreSqlTimestamp.Read(original);
        Assert.Equal(expected.Ticks, actual.Ticks);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Ticks, PostgreSqlTimestamp.Read(PostgreSqlTimestamp.Write(actual)).Ticks);
    }
}
