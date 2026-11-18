using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Dike.Web.Localization;

/// <summary>Localizes application-owned page errors without changing domain diagnostics.</summary>
public sealed class LocalizedPageFilter(IStringLocalizer<UiText> text) : IPageFilter
{
    public void OnPageHandlerSelected(PageHandlerSelectedContext context) { }
    public void OnPageHandlerExecuting(PageHandlerExecutingContext context) { }

    public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
    {
        if (context.Result is not PageResult) return;
        foreach (var entry in context.ModelState.Values)
        {
            for (var index = 0; index < entry.Errors.Count; index++)
            {
                var original = entry.Errors[index];
                if (string.IsNullOrEmpty(original.ErrorMessage)) continue;
                var translated = UiMessages.Localize(text, original.ErrorMessage);
                if (translated.ResourceNotFound) continue;
                entry.Errors[index] = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelError(translated.Value);
            }
        }
    }
}
