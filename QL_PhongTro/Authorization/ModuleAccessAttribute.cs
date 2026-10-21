using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace QL_PhongTro.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ModuleAccessAttribute(string? module = null, bool write = false)
    : Attribute, IAsyncAuthorizationFilter, IOrderedFilter
{
    public int Order => -1000;
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }
        var code = module ?? context.RouteData.Values["code"]?.ToString() ?? "";
        var permissions = http.RequestServices.GetRequiredService<PermissionService>();
        if (!await permissions.AllowsAsync(code, write))
            context.Result = new ForbidResult();
    }
}

