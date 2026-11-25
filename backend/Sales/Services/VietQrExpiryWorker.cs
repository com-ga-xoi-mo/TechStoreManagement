using Microsoft.Extensions.Options;
using TechStore.Api.Sales.Repositories;

namespace TechStore.Api.Sales.Services;

/// <summary>
/// Expires waiting VietQR requests whose deadline passed, using the same logic as the expire endpoint. Each request is
/// handled in its own scope and transaction, so one failure never stops the rest. Reads and confirmations also enforce
/// the deadline, so a stopped worker only delays the release of held goods.
/// </summary>
public sealed class VietQrExpiryWorker(
    IServiceScopeFactory scopes,
    IOptions<VietQrOptions> options,
    TimeProvider clock,
    ILogger<VietQrExpiryWorker> logger) : BackgroundService
{
    public const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = options.Value.ExpirySweepSeconds;
        if (seconds <= 0) return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>Expires up to <see cref="BatchSize"/> due requests; returns how many were closed.</summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        List<Guid> due;
        try
        {
            using var scope = scopes.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IVietQrHoldRepository>();
            due = (await repository.ListDueIdsAsync(clock.GetUtcNow().UtcDateTime, BatchSize, cancellationToken)).ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not list overdue VietQR requests.");
            return 0;
        }

        var closed = 0;
        foreach (var id in due)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IVietQrHoldService>().ExpireDueAsync(id, cancellationToken);
                if (result.IsSuccess) closed++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Could not expire VietQR request {QrId}.", id);
            }
        }

        return closed;
    }
}
