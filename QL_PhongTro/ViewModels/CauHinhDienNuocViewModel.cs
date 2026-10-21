using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public class CauHinhDienNuocViewModel
{
    public int ToaNhaId { get; set; }
    [ValidateNever] public string TenToaNha { get; set; } = "";
    public CauHinhTienDichVuViewModel Dien { get; set; } = new();
    public CauHinhTienDichVuViewModel Nuoc { get; set; } = new();
    [BindNever, ValidateNever] public CauHinhTienDichVuViewModel DienDaLuu { get; set; } = new();
    [BindNever, ValidateNever] public CauHinhTienDichVuViewModel NuocDaLuu { get; set; } = new();
    [BindNever, ValidateNever] public DateOnly KyHienTai { get; set; }
    [BindNever, ValidateNever] public DateOnly KyKeTiep { get; set; }
    public DateOnly KyDaXem { get; set; }
    public string TrangThaiDaXem { get; set; } = "";
    [BindNever, ValidateNever] public CauHinhTienDichVuViewModel? DienHienTai { get; set; }
    [BindNever, ValidateNever] public CauHinhTienDichVuViewModel? NuocHienTai { get; set; }
    [BindNever, ValidateNever] public CauHinhTienDichVuViewModel? DienCho { get; set; }
    [BindNever, ValidateNever] public CauHinhTienDichVuViewModel? NuocCho { get; set; }
    public bool CoThayDoi => Dien.ThayDoiSoVoi(DienDaLuu) || Nuoc.ThayDoiSoVoi(NuocDaLuu);
}

public class CauHinhTienDichVuViewModel : IValidatableObject
{
    public string CachTinh { get; set; } = CachTinhDichVu.TheoChiSo;
    public long? DonGiaChiSo { get; set; }
    public long? TienMotNguoi { get; set; }
    public string TruongGiaApDung => CachTinh == CachTinhDichVu.TheoChiSo ? nameof(DonGiaChiSo) : nameof(TienMotNguoi);
    public long? GiaApDung => CachTinh == CachTinhDichVu.TheoChiSo ? DonGiaChiSo : TienMotNguoi;

    public bool ThayDoiSoVoi(CauHinhTienDichVuViewModel saved) =>
        CachTinh is CachTinhDichVu.TheoChiSo or CachTinhDichVu.TheoNguoi && GiaApDung > 0 &&
        (CachTinh != saved.CachTinh || GiaApDung != saved.GiaApDung);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CachTinh is not (CachTinhDichVu.TheoChiSo or CachTinhDichVu.TheoNguoi))
            yield return new ValidationResult("Hãy chọn cách tính hợp lệ.", [nameof(CachTinh)]);
        else if (!GiaApDung.HasValue)
            yield return new ValidationResult("Hãy nhập mức giá cho cách tính đã chọn.",
                [TruongGiaApDung]);
        else if (GiaApDung <= 0)
            yield return new ValidationResult("Mức giá phải là số nguyên đồng lớn hơn 0.",
                [TruongGiaApDung]);
    }
}
