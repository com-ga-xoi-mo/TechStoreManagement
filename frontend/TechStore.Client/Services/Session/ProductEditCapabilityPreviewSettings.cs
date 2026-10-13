using System;
using System.Collections.Generic;

namespace TechStore.Client.Services.Session;

public enum PreviewCapabilityMode
{
    Denied,
    Authorized
}

/// <summary>
/// Explicit local UI-preview capability switches. They are presentation fixtures, not real RBAC.
/// </summary>
public static class ProductEditCapabilityPreviewSettings
{
    public const string EditEnvironmentVariable = "TECHSTORE_PRODUCT_EDIT_PREVIEW_ACCESS";
    public const string CostWriteEnvironmentVariable = "TECHSTORE_COST_WRITE_PREVIEW_ACCESS";

    public static PreviewCapabilityMode EditMode => ReadMode(EditEnvironmentVariable);
    public static PreviewCapabilityMode CostWriteMode => ReadMode(CostWriteEnvironmentVariable);

    public static IReadOnlySet<string> BuildPreviewCapabilities(
        PreviewCapabilityMode editMode,
        PreviewCapabilityMode costWriteMode)
    {
        var capabilities = new HashSet<string>();
        if (editMode == PreviewCapabilityMode.Authorized)
        {
            capabilities.Add("catalog:product-edit");
        }

        if (costWriteMode == PreviewCapabilityMode.Authorized)
        {
            capabilities.Add("catalog:cost-write");
        }

        return capabilities;
    }

    private static PreviewCapabilityMode ReadMode(string environmentVariable)
    {
#if DEBUG
        return string.Equals(Environment.GetEnvironmentVariable(environmentVariable), "authorized", StringComparison.OrdinalIgnoreCase)
            ? PreviewCapabilityMode.Authorized
            : PreviewCapabilityMode.Denied;
#else
        return PreviewCapabilityMode.Denied;
#endif
    }
}
