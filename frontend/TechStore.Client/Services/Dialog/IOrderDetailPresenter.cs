using TechStore.Client.Models.Orders;

namespace TechStore.Client.Services.Dialog;

/// <summary>
/// UI boundary that shows one order in a read-only dialog. It is separate from <see cref="IDialogService"/>
/// because that service only carries a title and text.
/// </summary>
public interface IOrderDetailPresenter
{
    /// <summary>Returns when the dialog is closed, or immediately when another dialog is already open.</summary>
    Task ShowAsync(OrderDetail order, CancellationToken cancellationToken = default);
}
