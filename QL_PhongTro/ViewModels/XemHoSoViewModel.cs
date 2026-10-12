namespace QL_PhongTro.ViewModels;

// This DTO contains only the identity number the viewer is allowed to receive.
public class XemHoSoViewModel
{
    public List<HoSoHopDongRow> HopDongsLienQuan { get; set; } = [];
    public bool HoSoDayDu { get; set; }
    public int? TaiKhoanId { get; set; }
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public bool EmailDaXacThuc { get; set; }
    public DateTime? NgayTaoTaiKhoan { get; set; }
    public DateTime? DangNhapGanNhat { get; set; }
    public int SoHopDong { get; set; }
    public string VaiTroThue { get; set; } = "Chưa tham gia hợp đồng";
    public int Id { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string CanCuocHienThi { get; set; } = string.Empty;
    public bool XemDayDu { get; set; }
    public bool LaChuHoSo { get; set; }
    public DateOnly? NgaySinh { get; set; }
    public string? QueQuan { get; set; }
    public string? NgheNghiep { get; set; }
    public bool CoAnhMatTruoc { get; set; }
    public bool CoAnhMatSau { get; set; }
}
