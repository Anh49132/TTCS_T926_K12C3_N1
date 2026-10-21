using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace QL_PhongTro.ViewModels;

public class HopDongGiaHanViewModel
{
    [BindNever] public int HopDongId { get; set; }
    [BindNever] public string MaHopDong { get; set; } = "";
    [BindNever] public DateOnly NgayKetThucHienTai { get; set; }
    [BindNever] public DateOnly? NgayBatDau { get; set; }
    [BindNever] public long GiaThueHienTai { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số tháng gia hạn.")]
    [Range(1, 120, ErrorMessage = "Số tháng gia hạn phải từ 1 đến 120.")]
    public int? SoThang { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Giá thuê mới phải lớn hơn 0.")]
    public long? GiaThueMoi { get; set; }

    public static DateOnly TinhNgayKetThuc(DateOnly ngayBatDau, int soThang) => ngayBatDau.AddMonths(soThang);
}
