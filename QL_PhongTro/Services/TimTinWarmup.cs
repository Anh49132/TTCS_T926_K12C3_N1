using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

// Compile search queries and Razor before accepting requests. No results are cached.
public static class TimTinWarmup
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/TimTin";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Search warmup"));
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var previous = accessor.HttpContext;
        accessor.HttpContext = context;
        try
        {
            var route = new RouteData();
            route.Values["controller"] = "TimTin";
            route.Values["action"] = "Index";
            context.Request.RouteValues = route.Values;
            var action = new ActionContext(context, route, new ControllerActionDescriptor
            { ControllerName = "TimTin", ActionName = "Index" });
            var controller = new TimTinController(
                provider.GetRequiredService<AppDbContext>(),
                provider.GetRequiredService<TinDangExpirationService>())
            { ControllerContext = new ControllerContext(action) };
            foreach (var order in new[] { "moi-nhat", "gia-tang", "gia-giam" })
            {
                var result = (ViewResult)await controller.Index(new TimTinViewModel { SapXep = order }, CancellationToken.None);
                if (order != "moi-nhat") continue;
                var engine = provider.GetRequiredService<IRazorViewEngine>();
                var view = engine.FindView(action, "Index", isMainPage: true);
                if (!view.Success) throw new InvalidOperationException("Search warmup could not find its Razor view.");
                using var writer = new StringWriter();
                var data = new ViewDataDictionary(provider.GetRequiredService<IModelMetadataProvider>(), action.ModelState)
                { Model = result.Model };
                var temp = new TempDataDictionary(context, provider.GetRequiredService<ITempDataProvider>());
                await view.View.RenderAsync(new ViewContext(action, view.View, data, temp, writer, new HtmlHelperOptions()));
            }
        }
        finally { accessor.HttpContext = previous; }
    }
}
