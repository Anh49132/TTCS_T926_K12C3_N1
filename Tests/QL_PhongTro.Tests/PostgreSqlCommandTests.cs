using Npgsql;
using NpgsqlTypes;
using QL_PhongTro.Data;
using Xunit;

public class PostgreSqlCommandTests
{
    [Fact]
    public void Dollar_quoted_function_bodies_and_positional_parameters_are_preserved()
    {
        const string sql = "CREATE FUNCTION f() RETURNS text AS $body$ SELECT '$id AS Value' $body$ LANGUAGE sql; SELECT $1, $id, $$ text $other $$";
        Assert.Equal("CREATE FUNCTION f() RETURNS text AS $body$ SELECT '$id AS Value' $body$ LANGUAGE sql; SELECT $1, @id, $$ text $other $$", PostgreSqlCommands.Normalize(sql));
    }
    [Fact]
    public void Sql_normalization_preserves_literals_comments_and_quoted_identifiers()
    {
        const string sql = "SELECT '$id AS Value' AS Value, \"$id\", @old FROM x WHERE id=$id -- $id AS Value\n/* $other */";
        const string expected = "SELECT '$id AS Value' AS \"Value\", \"$id\", @old FROM x WHERE id=@id -- $id AS Value\n/* $other */";
        Assert.Equal(expected, PostgreSqlCommands.Normalize(sql));
        Assert.Equal(expected, PostgreSqlCommands.Normalize(expected));
    }
    [Fact]
    public void Raw_parameters_keep_100ns_and_boolean_integer_storage()
    {
        using var command = new NpgsqlCommand("SELECT $stamp, $flag");
        var time = new DateTime(2026, 10, 22, 16, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        command.Parameters.AddWithValue("$stamp", time); command.Parameters.AddWithValue("$flag", true);
        PostgreSqlCommands.Prepare(command, rawAdo: true);
        Assert.Equal("SELECT @stamp, @flag", command.CommandText);
        Assert.Equal(NpgsqlDbType.Text, command.Parameters[0].NpgsqlDbType);
        Assert.Equal(time.Ticks, PostgreSqlTimestamp.Read((string)command.Parameters[0].Value!).Ticks);
        Assert.Equal(NpgsqlDbType.Integer, command.Parameters[1].NpgsqlDbType);
        Assert.Equal(1, command.Parameters[1].Value);
    }
    [Fact]
    public void Ef_predicate_boolean_parameter_keeps_its_native_type()
    {
        using var command = new NpgsqlCommand("SELECT 1 WHERE @allowed OR 1=0");
        command.Parameters.Add("allowed", NpgsqlDbType.Boolean).Value = true;
        PostgreSqlCommands.Prepare(command);
        Assert.Equal(NpgsqlDbType.Boolean, command.Parameters[0].NpgsqlDbType);
        Assert.Equal(true, command.Parameters[0].Value);
    }
}
