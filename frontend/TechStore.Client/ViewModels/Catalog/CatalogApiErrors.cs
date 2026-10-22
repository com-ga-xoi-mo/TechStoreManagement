using TechStore.Client.Services.Api;

namespace TechStore.Client.ViewModels.Catalog;

internal static class CatalogApiErrors
{
    public static string Describe(ApiException exception, string subject)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            { Kind: ApiErrorKind.Timeout } => "Yêu cầu quá thời gian chờ. Hãy tải lại sản phẩm để kiểm tra trước khi thử lại.",
            { Kind: ApiErrorKind.Network } => "Không thể kết nối API. Hãy kiểm tra kết nối rồi thử lại.",
            { Kind: ApiErrorKind.InvalidResponse } => "API trả dữ liệu không đúng định dạng.",
            { StatusCode: 404 } => $"{subject} không còn tồn tại.",
            { StatusCode: 409 } => FirstText(exception) ?? $"{subject} không thể thực hiện lúc này.",
            { StatusCode: 400 } => FirstText(exception) ?? "Dữ liệu chưa hợp lệ.",
            { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.",
            { StatusCode: 403 } => "Bạn không có quyền thực hiện thao tác này.",
            { StatusCode: 503 } => "Nơi lưu ảnh đang không truy cập được. Hãy thử lại sau.",
            _ => "Không thể hoàn tất thao tác. Hãy thử lại."
        };
    }

    public static string? FirstText(ApiException exception)
    {
        if (!string.IsNullOrWhiteSpace(exception.Detail)) return exception.Detail;
        return exception.FieldErrors.Values.SelectMany(messages => messages).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
    }
}
