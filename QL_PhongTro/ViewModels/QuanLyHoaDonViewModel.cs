using Microsoft.AspNetCore.Mvc.Rendering;
namespace QL_PhongTro.ViewModels;
public sealed class QuanLyHoaDonViewModel {
 public int? ToaNhaId {get;set;} public int? Nam {get;set;} public int? Thang {get;set;}
 public string? TrangThai {get;set;} public string? Q {get;set;}
 public bool SanSang {get;set;} public int SoNhap {get;set;} public int SoCho {get;set;} public int SoQuaHan {get;set;}
 public long DaThuThang {get;set;} public int Total {get;set;} public int Page {get;set;}
 public List<SelectListItem> ToaNhas {get;set;}=[]; public List<DongQuanLyHoaDon> Rows {get;set;}=[];
}
public sealed class DongQuanLyHoaDon {
 public int Id {get;set;} public int PhienBan {get;set;} public int ToaNhaId {get;set;} public string MaHoaDon {get;set;}="";
 public string MaPhong {get;set;}=""; public string TenKhach {get;set;}="";
 public int Nam {get;set;} public int Thang {get;set;} public string TrangThai {get;set;}="";
 public DateOnly? NgayPhatHanh {get;set;} public DateOnly HanThanhToan {get;set;}
 public long TongTien {get;set;} public long DaThu {get;set;} public bool QuaHan {get;set;}
 public string HienThi => TrangThai=="NHAP" ? "Nháp" : TrangThai=="DA_HUY" ? "Đã hủy" : DaThu>=TongTien ? "Đã thanh toán" : QuaHan ? "Quá hạn" : DaThu>0 ? "Trả một phần" : "Chờ thanh toán";
}
