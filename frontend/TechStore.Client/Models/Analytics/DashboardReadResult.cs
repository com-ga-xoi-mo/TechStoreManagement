namespace TechStore.Client.Models.Analytics;

public sealed record DashboardReadResult
{
    private DashboardReadResult(bool isAvailable, DashboardSnapshot? snapshot)
    {
        if (isAvailable != (snapshot is not null))
            throw new ArgumentException("An available result must contain a snapshot, and an unavailable result must not.");

        IsAvailable = isAvailable;
        Snapshot = snapshot;
    }

    public bool IsAvailable { get; }
    public DashboardSnapshot? Snapshot { get; }

    public static DashboardReadResult Unavailable { get; } = new(false, null);

    public static DashboardReadResult Available(DashboardSnapshot snapshot) =>
        new(true, snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
}
