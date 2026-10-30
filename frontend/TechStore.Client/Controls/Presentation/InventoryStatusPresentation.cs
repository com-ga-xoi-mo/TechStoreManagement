using TechStore.Client.Controls;
using TechStore.Shared.Enums;

namespace TechStore.Client.Controls.Presentation;

public static class InventoryStatusPresentation
{
    public static StatusTone SerialTone(SerialImeiStatus status) => status switch
    {
        SerialImeiStatus.InStock => StatusTone.Success,
        SerialImeiStatus.Reserved => StatusTone.Warning,
        SerialImeiStatus.Sold => StatusTone.Info,
        SerialImeiStatus.Returned => StatusTone.Neutral,
        SerialImeiStatus.UnderRepair => StatusTone.Warning,
        SerialImeiStatus.Defective => StatusTone.Danger,
        _ => StatusTone.Neutral
    };
}
