using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.Models;

public class TaiKhoan
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string HoTen { get; set; } = string.Empty;

    [Required, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(15)]
    public string SoDienThoai { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string MatKhau { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? VaiTro { get; set; }

    public bool DangHoatDong { get; set; }
    public bool MustChangePassword { get; set; }
    public bool EmailConfirmed { get; set; } = true;
    public bool IsDeleted { get; set; }
    public bool IsStaff { get; set; }
    public bool IsSuperuser { get; set; }
    public int? FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime NgayTao { get; set; }
    public DateTime NgayCapNhat { get; set; }
}
