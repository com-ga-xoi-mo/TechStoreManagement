using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Api.Common.Ai;
using TechStore.Api.Identity.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Assistant;

[ApiController]
[Route("api/assistant")]
[Authorize]
public sealed class AssistantController(IAssistantService assistant) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<ActionResult<AssistantReplyDto>> Chat([FromBody] AssistantChatRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var user)) return Unauthorized();
        if (request?.Messages is not { Count: >= 1 and <= 20 } messages || messages.Any(m => m is null || m.Role is not ("user" or "assistant") || m.Content is null || m.Content.Trim().Length is < 1 or > 2000) || messages[^1].Role != "user")
        {
            ModelState.AddModelError("messages", "Cần 1–20 tin nhắn hợp lệ, kết thúc bằng tin nhắn người dùng.");
            return ValidationProblem(ModelState);
        }
        try { return Ok(await assistant.ChatAsync(request, user.Role, cancellationToken)); }
        catch (AssistantNotConfiguredException) { return Problem(statusCode: 503, title: "Trợ lý AI chưa được cấu hình."); }
        catch (ChatModelUnavailableException) { return Problem(statusCode: 502, title: "Không kết nối được dịch vụ AI."); }
        catch (AssistantTimeoutException) { return Problem(statusCode: 504, title: "Trợ lý AI phản hồi quá lâu. Vui lòng thử lại."); }
    }
}
