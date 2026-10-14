using System.ComponentModel.DataAnnotations;
using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;
public sealed class TraPhongInput
{
    public int HopDongId { get; set; }
    [Required(ErrorMessage="Chọn ngày trả phòng thực tế."),DataType(DataType.Date)] public DateOnly? NgayTra { get; set; }
    public decimal? ChiSoDien { get; set; }
    public decimal? ChiSoNuoc { get; set; }
    public string? DauVanTay { get; set; }
}
public sealed class TraPhongViewModel
{
    public TraPhongInput Input { get; set; }=new();
    public string MaHopDong { get; set; }="";
    public string Phong { get; set; }="";
    public string ToaNha { get; set; }="";
    public string Khach { get; set; }="";
    public DateOnly NgayKetThuc { get; set; }
    public int PhienBanPhong { get; set; }
    public List<ChiSoTraPhong> CongTo { get; set; }=[];
    public HoaDon? HoaDon { get; set; }
    public int? HoaDonDaCo { get; set; }
}
public sealed record ChiSoTraPhong(string Ma,string Ten,string DonVi,decimal ChiSo,DateOnly NgayChot,long DonGia,int? ChiSoId,int PhienBan);
public sealed record HopDongTraPhongChon(int Id,string Ma,string Phong,string ToaNha,string Khach);
