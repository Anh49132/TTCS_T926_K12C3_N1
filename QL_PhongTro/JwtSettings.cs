namespace QL_PhongTro;

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public int AccessTokenMinutes { get; set; } = 30; // Đã khôi phục gốc
    public int RefreshTokenDays { get; set; } = 7;
}
