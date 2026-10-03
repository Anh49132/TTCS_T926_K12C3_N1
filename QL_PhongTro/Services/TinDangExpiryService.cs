using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;

namespace QL_PhongTro.Services;

public sealed class TinDangExpiryService(AppDbContext db)
{
    public Task<int> ScanExpiredAsync(DateTime now, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE tin_dang
            SET trang_thai = 'TAM_AN'
            WHERE trang_thai = 'DANG_HIEN_THI'
                AND ngay_het_han IS NOT NULL
                AND datetime(ngay_het_han) < datetime({now})
            """, cancellationToken);
}
