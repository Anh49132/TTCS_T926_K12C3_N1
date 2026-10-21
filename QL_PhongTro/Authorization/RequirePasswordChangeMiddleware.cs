namespace QL_PhongTro.Authorization;

public sealed class RequirePasswordChangeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.FindFirst("must_change_password")?.Value == "true")
        {
            var path = context.Request.Path.Value?.TrimEnd('/');
            var allowed = new[] { "/Account/ChangePassword", "/Account/Logout", "/Account/SessionStatus" };
            if (!allowed.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.Headers.CacheControl = "no-store";
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { code = "PASSWORD_CHANGE_REQUIRED", changePasswordUrl = "/Account/ChangePassword" });
                }
                else context.Response.Redirect("/Account/ChangePassword");
                return;
            }
        }
        await next(context);
    }
}
