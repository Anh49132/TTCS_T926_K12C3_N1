using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace QL_PhongTro.Data;

public partial class AppDbContext
{
    private void ConfigurePostgreSqlModel(ModelBuilder model)
    {
        if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;
        foreach (var entity in model.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (type == typeof(bool))
            {
                property.SetValueConverter(new BoolToZeroOneConverter<int>());
                property.SetColumnType("integer");
            }
            else if (type == typeof(DateTime))
            {
                // Same representation as SQLite: preserve all seven fractional
                // digits, including immutable audit/financial timestamps. Native
                // PostgreSQL timestamp would truncate 100 ns to microseconds.
                property.SetValueConverter(new ValueConverter<DateTime, string>(
                    value => PostgreSqlTimestamp.Write(value), value => PostgreSqlTimestamp.Read(value)));
                property.SetColumnType("text");
            }
            else if (type == typeof(decimal)) property.SetColumnType("numeric");
        }
    }
}
