using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.Models;

public class ToaNha
{
    [Key]
    public int Id { get; set; }

    public int ChuNhaId { get; set; }
    public int? QuanLyId { get; set; }

    [Required, MaxLength(150)]
    public string TenToaNha { get; set; } = string.Empty;

    [Required]
    public string DiaChi { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PhuongXa { get; set; }

    [MaxLength(100)]
    public string? QuanHuyen { get; set; }

    [MaxLength(100)]
    public string? TinhThanh { get; set; }

    public int? SoTang { get; set; }
    public int NgayChotHangThang { get; set; } = 1;
    public bool DangHoatDong { get; set; } = true;
    public string? GhiChu { get; set; }
    public decimal? DienTichDat { get; set; }
    public bool ThangMay { get; set; }
    public bool BaiDoXe { get; set; }
    public bool CameraAnNinh { get; set; }
    public bool BaoVe24h { get; set; }
    public bool KhuGiatSay { get; set; }
    public bool SanThuong { get; set; }
}