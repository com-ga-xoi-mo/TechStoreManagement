using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TechStore.Api.Common.Data;
using TechStore.Api.Warranty.Entities;

namespace TechStore.UnitTests.Warranty;

public sealed class WarrantyEntityTests
{
    [Fact]
    public void WarrantyModel_ConfiguresCaseAndEventStorage()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy_test;Username=postgres")
            .Options;
        using var context = new AppDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var claim = model.FindEntityType(typeof(WarrantyCase))!;
        Assert.Equal("warranty_cases", claim.GetTableName());
        Assert.Equal("case_code", claim.FindProperty(nameof(WarrantyCase.CaseCode))!.GetColumnName());
        Assert.Equal("contact_phone", claim.FindProperty(nameof(WarrantyCase.ContactPhone))!.GetColumnName());
        Assert.True(claim.FindProperty(nameof(WarrantyCase.IssueDescription))!.IsNullable);

        Assert.True(Assert.Single(claim.GetIndexes(), index => index.GetDatabaseName() == "idx_warranty_cases_code").IsUnique);
        var openDevice = Assert.Single(claim.GetIndexes(), index => index.GetDatabaseName() == "uq_warranty_cases_open_device");
        Assert.True(openDevice.IsUnique);
        Assert.Equal("status IN ('Received','Repairing','PendingReplacement','Repaired','Rejected')", openDevice.GetFilter());
        var outcome = Assert.Single(claim.GetCheckConstraints(), check => check.Name == "ck_warranty_cases_outcome");
        Assert.Contains("rejection_reason IS NOT NULL", outcome.Sql);
        Assert.Contains("replacement_unit_cost IS NULL", outcome.Sql);
        Assert.Contains("replacement_device_id <> device_id", outcome.Sql);

        var history = model.FindEntityType(typeof(WarrantyCaseEvent))!;
        Assert.Equal("warranty_case_events", history.GetTableName());
        Assert.True(history.FindProperty(nameof(WarrantyCaseEvent.FromStatus))!.IsNullable);
        Assert.Equal("text", history.FindProperty(nameof(WarrantyCaseEvent.ToStatus))!.GetColumnType());
        Assert.Equal("timestamptz", history.FindProperty(nameof(WarrantyCaseEvent.CreatedAt))!.GetColumnType());
        var chronological = Assert.Single(history.GetIndexes(), index => index.GetDatabaseName() == "idx_warranty_case_events_case_id");
        Assert.Equal(new[] { nameof(WarrantyCaseEvent.CaseId), nameof(WarrantyCaseEvent.CreatedAt) }, chronological.Properties.Select(property => property.Name));
        Assert.Contains(history.GetIndexes(), index => index.GetDatabaseName() == "idx_warranty_case_events_actor_id");
        Assert.Equal(2, history.GetForeignKeys().Count());
    }
}
