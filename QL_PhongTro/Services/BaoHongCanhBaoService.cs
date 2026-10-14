using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public static class BaoHongCanhBaoService
{
    // Both values represent UTC, including SQLite DateTimes with Kind=Unspecified.
    // Compare elapsed ticks, not rounded hours or Vietnam calendar dates.
    public static BaoHongCanhBao? DanhGia(BaoHong report, DateTime utcNow)
    {
        if (report.MucDo != "GAP" || report.TrangThai != "MOI") return null;
        var waiting = utcNow - report.NgayTao;
        return waiting > TimeSpan.FromHours(24) ? new BaoHongCanhBao(waiting) : null;
    }
}
