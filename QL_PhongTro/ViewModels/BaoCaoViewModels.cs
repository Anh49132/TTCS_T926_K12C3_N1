using Microsoft.AspNetCore.Mvc.Rendering;

namespace QL_PhongTro.ViewModels;

public sealed class BaoCaoDoanhThuViewModel
{
    // Filter controls
    public int? ToaNhaId { get; set; }
    public int TuThang { get; set; }
    public int TuNam { get; set; }
    public int DenThang { get; set; }
    public int DenNam { get; set; }
    public string TenToaNhaDangChon { get; set; } = "Tất cả toà nhà";
    public List<SelectListItem> DanhSachToaNha { get; set; } = [];
    public string? ErrorMessage { get; set; }

    // KPI / Totals across the filtered range
    public long TongPhatHanh { get; set; }
    public long TongDaThu { get; set; }
    public long TongConPhaiThu { get; set; }
    public decimal TiLeThuTongThe { get; set; }
    public decimal? TiLeLapDayHienTai { get; set; }
    public List<BaoCaoLapDayToaNhaRow> LapDayTheoToaNha { get; set; } = [];

    // Monthly breakdown table
    public List<BaoCaoThangRow> ChiTietTheoThang { get; set; } = [];

    // 6-month bar chart
    public List<BaoCaoChartItem> BieuDo6Thang { get; set; } = [];
}

public sealed class BaoCaoLapDayToaNhaRow
{
    public int ToaNhaId { get; set; }
    public string TenToaNha { get; set; } = "";
    public int SoPhongDangThue { get; set; }
    public int TongPhongChoThue { get; set; }
    public int SoPhongTrong { get; set; }
    public decimal? TiLeLapDay { get; set; }
}

public sealed class BaoCaoThangRow
{
    public int Nam { get; set; }
    public int Thang { get; set; }
    public string NhanThang => $"Tháng {Thang:D2}/{Nam}";
    public long TongPhatHanh { get; set; }
    public long TongDaThu { get; set; }
    public long TongCanTru { get; set; }
    public long SoConPhaiThu { get; set; }
    public decimal TiLeThu { get; set; }
    public int SoPhongDangThue { get; set; }
    public int TongPhongChoThue { get; set; }
    public decimal? TiLeLapDay { get; set; }
}

public sealed class BaoCaoChartItem
{
    public int Nam { get; set; }
    public int Thang { get; set; }
    public string NhanThang => $"T{Thang:D2}/{Nam % 100:D2}";
    public string NhanDayDu => $"Tháng {Thang:D2}/{Nam}";
    public long TongPhatHanh { get; set; }
    public long TongDaThu { get; set; }
    public long SoConPhaiThu { get; set; }
    public decimal TiLeThu { get; set; }
    public decimal? TiLeLapDay { get; set; }
}

