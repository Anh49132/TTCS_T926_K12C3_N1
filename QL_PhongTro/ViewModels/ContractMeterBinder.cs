using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace QL_PhongTro.ViewModels;

// HTML number controls submit a dot decimal separator regardless of Windows regional settings.
public sealed class ContractMeterBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);
        if (string.IsNullOrWhiteSpace(value.FirstValue)) context.Result = ModelBindingResult.Success(null);
        else if (decimal.TryParse(value.FirstValue, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                     CultureInfo.InvariantCulture, out var parsed)) context.Result = ModelBindingResult.Success(parsed);
        else context.ModelState.AddModelError(context.ModelName, "Chỉ số phải là số hợp lệ (tối đa 3 chữ số thập phân).");
        return Task.CompletedTask;
    }
}
