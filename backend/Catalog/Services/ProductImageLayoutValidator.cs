using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

public static class ProductImageLayoutValidator
{
    public static Dictionary<string, string[]> Validate(UpdateProductImagesLayoutRequest? request)
    {
        Dictionary<string, string[]> errors = [];
        if (request?.Items == null)
        {
            errors["items"] = ["items is required."];
            return errors;
        }
        HashSet<Guid> ids = [];
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            if (item?.Id is not { } id || id == Guid.Empty)
                errors[$"items[{i}].id"] = ["Image id is required."];
            else if (!ids.Add(id))
                errors[$"items[{i}].id"] = [$"Image '{id}' is listed more than once."];
            if (item?.VariantId == Guid.Empty)
                errors[$"items[{i}].variantId"] = ["Variant id cannot be empty."];
        }
        return errors;
    }
}
