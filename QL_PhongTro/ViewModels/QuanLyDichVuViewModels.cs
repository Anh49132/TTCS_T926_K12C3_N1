using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public class QuanLyDichVuViewModel
{
    public int ToaNhaId { get; set; }
    public int DichVuId { get; set; }
    public int PhienBan { get; set; }
    public long GiaCu { get; set; }
    [Required(ErrorMessage = "Hãy nhập đơn giá."), Range(typeof(long), "0", "9223372036854775807", ErrorMessage = "Đơn giá phải là số nguyên đồng không âm.")]
    [Display(Name = "Đơn giá mới (VND)")] public long? DonGia { get; set; }
    [DataType(DataType.Date), Display(Name = "Ngày bắt đầu hiệu lực")]
    public DateOnly? TuNgay { get; set; }
    [ValidateNever] public List<CauHinhDichVu> LichSu { get; set; } = [];
    [ValidateNever] public string TenToaNha { get; set; } = "";
    [ValidateNever] public int SoPhongApDung { get; set; }
    [ValidateNever] public int SoPhongGiaRieng { get; set; }
    [ValidateNever] public bool ApDungMacDinh { get; set; }
}
