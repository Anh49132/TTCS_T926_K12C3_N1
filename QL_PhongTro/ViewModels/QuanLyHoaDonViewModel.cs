using Microsoft.AspNetCore.Mvc.Rendering;
namespace QL_PhongTro.ViewModels;
public sealed class QuanLyHoaDonViewModel {
 public int? ToaNhaId {get;set;} public int? Nam {get;set;} public int? Thang {get;set;}
 public string? TrangThai {get;set;} public string? Q {get;set;}
 public bool SanSang {get;set;} public int SoNhap {get;set;} public int SoCho {get;set;} public int SoQuaHan {get;set;}
 public long DaThuThang {get;set;} public int Total {get;set;} public int Page {get;set;}
 public int SoChoXacNhan {get;set;}
 public List<SelectListItem> ToaNhas {get;set;}=[]; public List<DongQuanLyHoaDon> Rows {get;set;}=[];
}
public sealed class DongQuanLyHoaDon {
 public int Id {get;set;} public int PhienBan {get;set;} public int ToaNhaId {get;set;} public string MaHoaDon {get;set;}="";
 public string MaPhong {get;set;}=""; public string TenKhach {get;set;}="";
 public int Nam {get;set;} public int Thang {get;set;} public string TrangThai {get;set;}="";
 public DateOnly? NgayPhatHanh {get;set;} public DateOnly HanThanhToan {get;set;}
 public long TongTien {get;set;} public long DaThu {get;set;} public bool QuaHan {get;set;} public long DaCanTru {get;set;} public long ConNo=>checked(TongTien-DaThu-DaCanTru);
 public int? BaoCaoId {get;set;} public long? SoTienBaoCao {get;set;} public DateTime? GuiLuc {get;set;}
 public string HienThi => TrangThai=="NHAP" ? "Nháp" : TrangThai=="DA_HUY" ? "Đã hủy" : ConNo<=0 ? "Đã thanh toán" : DaThu>0 || DaCanTru>0 ? "Trả một phần" : QuaHan ? "Quá hạn" : "Chưa thanh toán";
}
