using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Dike.Web.Localization;

public sealed class LocalizedModelBindingMessages(IStringLocalizer<UiText> text) : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions options)
    {
        var messages = options.ModelBindingMessageProvider;
        messages.SetAttemptedValueIsInvalidAccessor((_, field) => text["Enter a valid value for {0}.", field]);
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => text["Enter a valid value."]);
        messages.SetUnknownValueIsInvalidAccessor(field => text["Enter a valid value for {0}.", field]);
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => text["Enter a valid value."]);
        messages.SetValueIsInvalidAccessor(_ => text["Enter a valid value."]);
        messages.SetValueMustBeANumberAccessor(field => text["Enter a number for {0}.", field]);
        messages.SetNonPropertyValueMustBeANumberAccessor(() => text["Enter a valid value."]);
        messages.SetValueMustNotBeNullAccessor(_ => text["Complete the required fields."]);
        messages.SetMissingBindRequiredValueAccessor(field => text["The {0} field is required.", field]);
        messages.SetMissingKeyOrValueAccessor(() => text["Complete the required fields."]);
        messages.SetMissingRequestBodyRequiredValueAccessor(() => text["Complete the required fields."]);
    }
}
