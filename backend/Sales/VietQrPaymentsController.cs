using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using TechStore.Api.Identity.Services;
using TechStore.Api.Sales.Services;
using TechStore.Shared.Requests;

namespace TechStore.Api.Sales;

/// <summary>
/// Bank-side VietQR endpoints: the signed notification and the review of money that could not complete a sale.
/// The notification is a SANDBOX integration: it is signed with a shared secret from configuration and no real bank
/// provider is connected. Responses say so.
/// </summary>
[ApiController]
[Route("api/payments/vietqr")]
[Authorize(Roles = "Admin,Manager")]
public sealed class VietQrPaymentsController(
    IVietQrConfirmationService confirmation,
    IOptions<VietQrOptions> options,
    TimeProvider clock,
    ILogger<VietQrPaymentsController> logger) : ControllerBase
{
    public const int MaxWebhookBytes = 16 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        var raw = await ReadBodyAsync(cancellationToken);
        if (raw == null)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Payload too large");

        var verdict = VietQrWebhookSignature.Verify(
            options.Value.WebhookSecret,
            Request.Headers["X-Timestamp"].FirstOrDefault(),
            Request.Headers["X-Signature"].FirstOrDefault(),
            raw,
            clock.GetUtcNow());
        if (verdict == VietQrSignatureResult.NotConfigured)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Webhook is not configured", detail: "Sandbox webhook: no secret is configured.");
        if (verdict == VietQrSignatureResult.Invalid)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid signature");

        VietQrWebhookRequest? body;
        try
        {
            body = JsonSerializer.Deserialize<VietQrWebhookRequest>(raw, Json);
        }
        catch (JsonException)
        {
            body = null;
        }

        var errors = Validate(body);
        if (errors.Count > 0) return ValidationProblem(BuildModelState(errors));

        var result = await confirmation.ReceiveSandboxAsync(
            new VietQrBankNotice(body!.TransactionId!.Trim(), body.Amount!.Value, body.TransferContent!.Trim(),
                body.AccountNumber!.Trim(), ToUtc(body.TransferredAt!.Value), raw),
            cancellationToken);

        if (result.Kind != PosResultKind.Success)
        {
            logger.LogWarning("Sandbox VietQR notification {TransactionId} was refused ({Kind}).", body.TransactionId, result.Kind);
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: result.ConflictReason);
        }

        var applied = result.Data!;
        return Ok(new
        {
            mode = "sandbox",
            result = applied.Result,
            reason = applied.Reason.ToString(),
            paymentId = applied.Payment?.Id,
            status = applied.Payment?.Status
        });
    }

    [HttpGet("events")]
    public async Task<IActionResult> Events([FromQuery] VietQrEventSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await confirmation.SearchEventsAsync(request, cancellationToken);
        return result.Kind == PosResultKind.Success ? Ok(result.Data) : ValidationProblem(BuildModelState(result.ValidationErrors));
    }

    [HttpPost("events/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveVietQrEventRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryRead(User, out var actor)) return Unauthorized();
        var result = await confirmation.ResolveEventAsync(id, request, actor, cancellationToken);
        return result.Kind switch
        {
            PosResultKind.Success => Ok(result.Data),
            PosResultKind.NotFound => NotFound(),
            PosResultKind.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: result.ConflictReason),
            _ => ValidationProblem(BuildModelState(result.ValidationErrors))
        };
    }

    // Reads at most MaxWebhookBytes; a longer body is refused before it is parsed or logged.
    private async Task<string?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxWebhookBytes + 1];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await Request.Body.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0) break;
            read += count;
        }

        return read > MaxWebhookBytes ? null : Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static Dictionary<string, string[]> Validate(VietQrWebhookRequest? body)
    {
        var errors = new Dictionary<string, string[]>();
        if (body == null)
        {
            errors["request"] = ["The body must be a JSON object."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(body.TransactionId) || body.TransactionId.Trim().Length > 100)
            errors["transactionId"] = ["The transaction id is required (at most 100 characters)."];
        if (body.Amount is not > 0m || decimal.Round(body.Amount.Value, 2) != body.Amount.Value)
            errors["amount"] = ["The amount must be above zero with at most 2 decimals."];
        if (string.IsNullOrWhiteSpace(body.TransferContent) || body.TransferContent.Trim().Length > 500)
            errors["transferContent"] = ["The transfer content is required (at most 500 characters)."];
        if (string.IsNullOrWhiteSpace(body.AccountNumber) || body.AccountNumber.Trim().Length > 40)
            errors["accountNumber"] = ["The account number is required (at most 40 characters)."];
        if (!body.TransferredAt.HasValue)
            errors["transferredAt"] = ["The transfer time is required."];
        return errors;
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static ModelStateDictionary BuildModelState(Dictionary<string, string[]>? errors)
    {
        var modelState = new ModelStateDictionary();
        if (errors != null)
        {
            foreach (var (key, messages) in errors)
            {
                foreach (var message in messages) modelState.AddModelError(key, message);
            }
        }

        return modelState;
    }
}
