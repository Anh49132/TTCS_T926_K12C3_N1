using Microsoft.EntityFrameworkCore;
using Npgsql;
using QL_PhongTro.Data;
using Xunit;

public class DatabaseErrorsTests
{
    [Theory]
    [InlineData("23505", true, true, false, false)]
    [InlineData("23503", true, false, true, false)]
    [InlineData("23514", true, false, false, false)]
    [InlineData("55P03", false, false, false, true)]
    [InlineData("40001", false, false, false, true)]
    [InlineData("42601", false, false, false, false)]
    public void PostgreSql_errors_are_classified_through_Ef_wrapper(string state, bool constraint, bool unique, bool fk, bool busy)
    {
        var error = new DbUpdateException("test", new PostgresException("test", "ERROR", "ERROR", state));
        Assert.Equal(constraint, DatabaseErrors.IsConstraint(error));
        Assert.Equal(unique, DatabaseErrors.IsUnique(error));
        Assert.Equal(fk, DatabaseErrors.IsForeignKey(error));
        Assert.Equal(busy, DatabaseErrors.IsBusy(error));
        Assert.False(DatabaseErrors.IsMissingSchema(error));
    }
    [Theory]
    [InlineData("42P01")]
    [InlineData("42703")]
    public void Missing_table_or_column_is_explicit(string state) =>
        Assert.True(DatabaseErrors.IsMissingSchema(new PostgresException("test", "ERROR", "ERROR", state)));
}
