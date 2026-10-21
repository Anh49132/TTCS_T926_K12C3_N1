namespace QL_PhongTro.ViewModels.Auth;

public class LoginResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public int AccessTokenExpiresIn { get; set; }
    public int RefreshTokenExpiresIn { get; set; }
    public required string UserId { get; set; }
    public required string HoTen { get; set; }
    public required string VaiTro { get; set; }
}

public class RefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}

public class RefreshTokenResponse
{
    public required string AccessToken { get; set; }
    public int AccessTokenExpiresIn { get; set; }
}

public class LogoutResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AuthErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Code { get; set; }
    public string? ErrorDescription { get; set; }
}
