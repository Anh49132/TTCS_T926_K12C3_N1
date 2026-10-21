namespace QL_PhongTro.ViewModels;

public sealed class OwnerBankInput
{
    public string? TenNganHang { get; set; }
    public string? SoTaiKhoan { get; set; }
    public IFormFile? AnhQr { get; set; }
    public bool XoaQr { get; set; }
    public string? PhienBan { get; set; }
}

public sealed record OwnerBankResponse(string TenNganHang, string SoTaiKhoan, bool CoQr, string PhienBan, DateTime NgayCapNhat);
public sealed class OwnerBankValidationException(Dictionary<string, string[]> errors) : Exception("Thông tin ngân hàng không hợp lệ.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
