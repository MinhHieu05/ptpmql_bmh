using DemoMVC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace DemoMVC.Middleware;

public sealed class DynamicActionAuthorizationMiddleware
{
    private readonly RequestDelegate _next;

    public DynamicActionAuthorizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        DynamicActionPermissionService permissionService)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null ||
            endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>() is not { } action)
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        action.RouteValues.TryGetValue("area", out var area);
        var allowed = await permissionService.IsAllowedAsync(
            context.User,
            area,
            action.ControllerName,
            action.ActionName,
            context.RequestAborted);

        if (!allowed)
        {
            await context.ForbidAsync();
            return;
        }

        await _next(context);
    }
}
