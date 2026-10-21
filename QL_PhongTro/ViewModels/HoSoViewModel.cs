using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.ViewModels;

public class HoSoViewModel : IValidatableObject
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool CoHoSo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NgaySinh > DateOnly.FromDateTime(DateTime.Today))
            yield return new ValidationResult("Ngày sinh không được sau ngày hiện tại.", [nameof(NgaySinh)]);
    }

    [Display(Name = "Ảnh mặt trước")]
    public IFormFile? AnhMatTruoc { get; set; }
    [Display(Name = "Ảnh mặt sau")]
    public IFormFile? AnhMatSau { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool CoAnhMatTruoc { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool CoAnhMatSau { get; set; }
    [Display(Name = "Họ tên"), Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, ErrorMessage = "Họ tên không quá 100 ký tự.")]
    public string HoTen { get; set; } = string.Empty;

    [Display(Name = "Ngày sinh"), Required(ErrorMessage = "Vui lòng nhập ngày sinh.")]
    [DataType(DataType.Date)]
    public DateOnly? NgaySinh { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string CanCuocDaLuu { get; set; } = string.Empty;

    [Display(Name = "Số căn cước")]
    [RegularExpression(@"\A(?:[0-9]{9}|[0-9]{12})\z", ErrorMessage = "Số căn cước chỉ gồm chữ số và phải có đúng 9 hoặc 12 chữ số.")]
    public string? SoCanCuoc { get; set; }

    [Display(Name = "Quê quán"), Required(ErrorMessage = "Vui lòng nhập quê quán.")]
    public string QueQuan { get; set; } = string.Empty;

    [Display(Name = "Nghề nghiệp"), Required(ErrorMessage = "Vui lòng nhập nghề nghiệp.")]
    [StringLength(150, ErrorMessage = "Nghề nghiệp không quá 150 ký tự.")]
    public string NgheNghiep { get; set; } = string.Empty;
}
