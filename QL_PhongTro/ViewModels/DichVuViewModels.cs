using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public class TaoDichVuViewModel : IValidatableObject
{
    [Display(Name = "Tòa nhà")]
    public int? ToaNhaId { get; set; }
    public List<int> ToaNhaIds { get; set; } = [];
    public bool ChonPhongCuThe { get; set; }
    public List<int> PhongIds { get; set; } = [];
    [ValidateNever] public List<PhongTro> Phongs { get; set; } = [];
    [Required(ErrorMessage = "Hãy nhập tên dịch vụ."), StringLength(100, ErrorMessage = "Tên dịch vụ tối đa 100 ký tự."), Display(Name = "Tên dịch vụ")]
    public string? TenDichVu { get; set; }
    [Required(ErrorMessage = "Hãy chọn cách tính tiền."), Display(Name = "Cách tính tiền")]
    public string? CachTinh { get; set; }
    [Required(ErrorMessage = "Hãy nhập đơn vị tính."), StringLength(30, ErrorMessage = "Đơn vị tính tối đa 30 ký tự."), Display(Name = "Đơn vị tính")]
    public string? DonViTinh { get; set; }
    [Required(ErrorMessage = "Hãy nhập đơn giá."), Range(typeof(long), "0", "9223372036854775807", ErrorMessage = "Đơn giá phải là số nguyên đồng, không âm."), Display(Name = "Đơn giá (VND)")]
    public long? DonGia { get; set; }
    [Required, RegularExpression("DANG_AP_DUNG|TAM_NGUNG", ErrorMessage = "Trạng thái không hợp lệ.")]
    public string TrangThai { get; set; } = "DANG_AP_DUNG";
    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? GhiChu { get; set; }
    [Display(Name = "Áp dụng mặc định cho phòng mới")] public bool ApDungMacDinh { get; set; }
    [ValidateNever] public List<SelectListItem> ToaNhas { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ToaNhaIds.Count == 0 && !ToaNhaId.HasValue)
            yield return new ValidationResult("Hãy chọn ít nhất một tòa nhà.", [nameof(ToaNhaIds)]);
        if (ChonPhongCuThe && PhongIds.Count == 0)
            yield return new ValidationResult("Hãy chọn ít nhất một phòng.", [nameof(PhongIds)]);
        if (!CachTinhDichVu.HopLe(CachTinh))
            yield return new ValidationResult("Cách tính tiền không hợp lệ.", [nameof(CachTinh)]);
    }
}

public class DanhSachDichVuViewModel
{
    public int? ToaNhaId { get; set; }
    public List<SelectListItem> ToaNhas { get; set; } = [];
    public List<CauHinhDichVu> DichVus { get; set; } = [];
    public HashSet<int> MacDinhIds { get; set; } = [];
    public bool SanSang { get; set; }
    public bool CanKhoiTao { get; set; }
}
