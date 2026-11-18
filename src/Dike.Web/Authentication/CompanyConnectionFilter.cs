using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Dike.Web.Authentication;

// Provider selection is authoritative in Identity, never in a query, form or stale cookie.
public sealed class CompanyConnectionFilter(CompanyAdministrator access) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var route = context.ActionDescriptor.ViewEnginePath;
        if (!route.StartsWith("/Accounts/", StringComparison.OrdinalIgnoreCase)) { await next(); return; }
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        AuthorizedCompany company;
        try { company = await access.RequireAsync(context.HttpContext.User, context.HttpContext.RequestAborted); }
        catch (UnauthorizedAccessException) { context.Result = new ForbidResult(); return; }
        if (!company.SetupComplete) { context.Result = new RedirectToPageResult("/Company/Setup"); return; }
        if (!ConnectionProviders.IsSupported(company.ConnectionProvider)) { context.Result = new ForbidResult(); return; }
        var isOdoo = route.StartsWith("/Accounts/Odoo/", StringComparison.OrdinalIgnoreCase);
        if (isOdoo != (company.ConnectionProvider == ConnectionProviders.Odoo))
        {
            // The generic connections entry point follows the company's selected provider.
            context.Result = route.Equals("/Accounts/Index", StringComparison.OrdinalIgnoreCase) &&
                (HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method))
                ? new RedirectToPageResult("/Accounts/Odoo/Index") : new NotFoundResult();
            return;
        }
        await next();
    }
}
