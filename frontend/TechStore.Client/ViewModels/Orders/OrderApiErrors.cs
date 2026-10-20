using TechStore.Client.Services.Api;

namespace TechStore.Client.ViewModels.Orders;

/// <summary>Turns an <see cref="ApiException"/> from the Orders API into one line the user can act on.</summary>
internal static class OrderApiErrors
{
    /// <param name="exception">The failure from the repository.</param>
    /// <param name="subject">What was being done to which order, e.g. "Đơn ORD-1".</param>
    public static string Describe(ApiException exception, string subject)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            { Kind: ApiErrorKind.Timeout } => "Yêu cầu quá thời gian chờ. Hãy kiểm tra lại trạng thái đơn rồi thử lại.",
            { Kind: ApiErrorKind.Network } => "Không thể kết nối API. Hãy kiểm tra kết nối rồi thử lại.",
            { Kind: ApiErrorKind.InvalidResponse } => "API trả dữ liệu không đúng định dạng. Hãy làm mới danh sách để kiểm tra trạng thái đơn.",
            { StatusCode: 404 } => $"{subject} không còn tồn tại.",
            { StatusCode: 409 } => FirstText(exception) ?? $"{subject} không thể thực hiện lúc này vì trạng thái hoặc thiết bị đã thay đổi.",
            { StatusCode: 400 } => FirstText(exception) ?? "Yêu cầu không hợp lệ.",
            { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.",
            { StatusCode: 403 } => "Bạn không có quyền thực hiện thao tác này.",
            _ => "Không thể hoàn tất thao tác. Hãy thử lại."
        };
    }

    /// <summary>The server's own explanation: its <c>detail</c>, otherwise the first field error.</summary>
    public static string? FirstText(ApiException exception)
    {
        if (!string.IsNullOrWhiteSpace(exception.Detail)) return exception.Detail;
        return exception.FieldErrors.Values.SelectMany(messages => messages).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
    }
}
