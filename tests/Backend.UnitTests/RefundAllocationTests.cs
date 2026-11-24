using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Services;

namespace TechStore.UnitTests;

public class RefundAllocationTests
{
    [Fact]
    public void Calculate_SuccessiveReturns_ReconcilesEveryComponentAndKeepsFreeItemOutstanding()
    {
        // Arrange
        var sold = Line(3, 10m, 1m, 2m, 1m);
        var free = Line(2, 5m, 10m, 0m, 0m);
        var order = Sale(sold, free);

        // Act
        var first = Settle(order, sold, 1);
        var second = Settle(order, sold, 1);
        var third = Settle(order, sold, 1);
        var last = Settle(order, free, 2);

        // Assert
        Assert.Equal(new[] { 9m, 9m, 9m }, new[] { first, second, third }.Select(x => x.Lines.Single().NetAmount));
        Assert.Equal(new[] { .33m, .34m, .33m }, new[] { first, second, third }.Select(x => x.Lines.Single().TaxAmount));
        Assert.Equal(0m, third.RemainingNetAmount);
        Assert.False(third.IsFullyReturned);
        Assert.True(last.IsFullyReturned);
        Assert.Equal(0m, last.Lines.Single().RefundAmount);
        Assert.Equal(13m, order.Returns.Sum(x => x.DiscountAmount));
        Assert.Equal(27m, order.Returns.Sum(x => x.NetAmount));
        Assert.Equal(order.TotalAmount, order.Returns.Sum(x => x.RefundAmount));
    }

    [Fact]
    public void Calculate_OriginalM4Sale_UsesRecordedPriceRegardlessOfCurrentTaxAndCost()
    {
        // Arrange
        var item = Line(2, 1_000_000m, 0m, 200_000m, 180_000m);
        var order = Sale(item);
        order.TaxRate = 0;
        item.UnitCost = null;

        // Act
        var first = Settle(order, item, 1);
        var last = Settle(order, item, 1);

        // Assert
        var expected = new RefundLineAllocation(item.Id, 1, 900_000m, 100_000m, 90_000m);
        Assert.Equal(expected, first.Lines.Single());
        Assert.Equal(expected, last.Lines.Single());
        Assert.Equal(990_000m, expected.RefundAmount);
        Assert.Equal(900_000m, first.RemainingNetAmount);
        Assert.Equal(0m, last.RemainingNetAmount);
        Assert.True(last.IsFullyReturned);
    }

    [Fact]
    public void Calculate_LegacyCashReceiptAndLaterModernRefund_UsesOriginalSnapshotAndFinalResidual()
    {
        // Arrange
        var item = Line(4, 1m, 0m, .02m, .02m);
        var order = Sale(item);
        order.Returns.Add(new() { OrderItemId = item.Id, Quantity = 1, RefundAmount = 1.01m });

        // Act
        var second = Settle(order, item, 1);
        var rest = Settle(order, item, 2);

        // Assert
        Assert.Equal(new RefundLineAllocation(item.Id, 1, .99m, 0m, 0m), second.Lines.Single());
        Assert.Equal(new RefundLineAllocation(item.Id, 2, 1.99m, .01m, .01m), rest.Lines.Single());
        Assert.Equal(order.TotalAmount, order.Returns.Sum(x => x.RefundAmount));
        Assert.Null(order.Returns.First().NetAmount);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("unknown")]
    [InlineData("legacy")]
    [InlineData("partialSnapshot")]
    [InlineData("inconsistentReceipt")]
    [InlineData("overdrawnTax")]
    [InlineData("underpaidFullLine")]
    public void Calculate_InvalidSettledHistory_RequiresReviewBeforeAnyFurtherPayout(string problem)
    {
        // Arrange
        var old = Line(2, 10m, 0m, 1m, 2m);
        var other = Line(1, 200m, 0m, 0m, 0m);
        var order = Sale(old, other);
        var receipt = new OrderReturn { OrderItemId = old.Id, Quantity = 1, RefundAmount = 10.5m };
        switch (problem)
        {
            case "quantity": receipt.Quantity = 3; break;
            case "unknown": receipt.OrderItemId = Guid.NewGuid(); break;
            case "legacy": receipt.RefundAmount -= .01m; break;
            case "partialSnapshot": receipt.TaxAmount = 1m; break;
            case "inconsistentReceipt": receipt.NetAmount = 9m; receipt.DiscountAmount = .5m; receipt.TaxAmount = 1m; break;
            case "overdrawnTax": receipt.NetAmount = 7.5m; receipt.DiscountAmount = .5m; receipt.TaxAmount = 3m; break;
            case "underpaidFullLine": receipt.Quantity = 2; receipt.NetAmount = 9.5m; receipt.DiscountAmount = .5m; receipt.TaxAmount = 1m; break;
        }
        order.Returns.Add(receipt);

        // Act
        var proposal = Propose(order, other.Id, 1);

        // Assert
        Assert.NotNull(proposal.Error);
        Assert.Empty(proposal.Lines);
    }

    [Theory]
    [InlineData("discount")]
    [InlineData("tax")]
    [InlineData("offsetting")]
    [InlineData("paid")]
    [InlineData("subtotal")]
    [InlineData("line")]
    public void Calculate_SaleComponentsDoNotMatchOrder_RequiresReceiptReview(string problem)
    {
        // Arrange
        var item = Line(2, 10m, 1m, 2m, 2m);
        var order = Sale(item);
        switch (problem)
        {
            case "discount": item.OrderDiscountAmount = 0m; break;
            case "tax": item.TaxAmount = 0m; break;
            case "offsetting": item.OrderDiscountAmount = 0m; item.TaxAmount = 0m; break;
            case "paid": order.TotalAmount += .01m; break;
            case "subtotal": order.Subtotal += .01m; break;
            case "line": item.DiscountAmount += .01m; break;
        }

        // Act
        var proposal = Propose(order, item.Id, 1);

        // Assert
        Assert.NotNull(proposal.Error);
        Assert.Empty(proposal.Lines);
    }

    [Fact]
    public void Calculate_RecordedComponentsDifferByACent_LastUnitReceivesExactlyTheOutstandingAmounts()
    {
        // Arrange
        var item = Line(2, 4m, 1m, 1m, 1m);
        var order = Sale(item);
        order.Returns.Add(new()
        {
            OrderItemId = item.Id, Quantity = 1, NetAmount = 3.01m,
            TaxAmount = .49m, DiscountAmount = .99m, RefundAmount = 3.50m
        });

        // Act
        var last = Settle(order, item, 1);

        // Assert
        Assert.Equal(new RefundLineAllocation(item.Id, 1, 2.99m, 1.01m, .51m), last.Lines.Single());
        Assert.True(last.IsFullyReturned);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void Calculate_InvalidRequestQuantity_AfterPriorSettlementReturnsNoLines(int requested)
    {
        // Arrange
        var item = Line(2, 1m, 0m, 0m, 0m);
        var order = Sale(item);
        Settle(order, item, 1);

        // Act
        var proposal = Propose(order, item.Id, requested);

        // Assert
        Assert.NotNull(proposal.Error);
        Assert.Empty(proposal.Lines);
        Assert.NotNull(Propose(order, Guid.NewGuid(), 1).Error);
    }

    [Fact]
    public void Calculate_OffsettingLegacyPayoutErrors_RequiresReviewOfTheIndividualReceipts()
    {
        // Arrange
        var item = Line(3, 10m, 0m, 0m, 0m);
        var order = Sale(item);
        order.Returns.Add(new() { OrderItemId = item.Id, Quantity = 1, RefundAmount = 8m });
        order.Returns.Add(new() { OrderItemId = item.Id, Quantity = 1, RefundAmount = 12m });

        // Act
        var result = Propose(order, item.Id, 1);

        // Assert
        Assert.NotNull(result.Error);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Calculate_MultiLineRequest_ReturnsOnlyRequestedRowsAndDeductsPriorReceiptsFromRetainedNet()
    {
        // Arrange
        var paid = Line(2, 100m, 0m, 20m, 18m);
        var free = Line(1, 15m, 15m, 0m, 0m);
        var kept = Line(3, 11m, 0m, 0m, 0m);
        var order = Sale(paid, free, kept);
        Settle(order, kept, 1);

        // Act
        var history = RefundAllocation.Calculate(order, new Dictionary<Guid, int>());
        var proposal = RefundAllocation.Calculate(order, new Dictionary<Guid, int> { [free.Id] = 1, [paid.Id] = 2 });

        // Assert
        Assert.Null(history.Error);
        Assert.Empty(history.Lines);
        Assert.Equal(202m, history.RemainingNetAmount);
        Assert.Null(proposal.Error);
        Assert.Equal(22m, proposal.RemainingNetAmount);
        Assert.False(proposal.IsFullyReturned);
        Assert.Equal(2, proposal.Lines.Count);
        Assert.Equal(198m, proposal.Lines.Single(x => x.OrderItemId == paid.Id).RefundAmount);
        Assert.Equal(15m, proposal.Lines.Single(x => x.OrderItemId == free.Id).DiscountAmount);
    }

    private static OrderItem Line(int quantity, decimal unitPrice, decimal lineDiscount, decimal orderDiscount, decimal tax) =>
        new() { Quantity = quantity, UnitPrice = unitPrice, TotalPrice = quantity * unitPrice - lineDiscount,
            DiscountAmount = lineDiscount, OrderDiscountAmount = orderDiscount, TaxAmount = tax };

    private static Order Sale(params OrderItem[] items)
    {
        var order = new Order { Items = items, Subtotal = items.Sum(x => x.TotalPrice),
            DiscountAmount = items.Sum(x => x.OrderDiscountAmount), TaxAmount = items.Sum(x => x.TaxAmount) };
        order.TotalAmount = order.Subtotal - order.DiscountAmount + order.TaxAmount;
        return order;
    }

    private static RefundAllocationResult Propose(Order order, Guid lineId, int quantity) =>
        RefundAllocation.Calculate(order, new Dictionary<Guid, int> { [lineId] = quantity });

    private static RefundAllocationResult Settle(Order order, OrderItem item, int quantity)
    {
        var result = Propose(order, item.Id, quantity);
        Assert.Null(result.Error);
        var line = Assert.Single(result.Lines);
        order.Returns.Add(new() { OrderItemId = line.OrderItemId, Quantity = line.Quantity, NetAmount = line.NetAmount,
            DiscountAmount = line.DiscountAmount, TaxAmount = line.TaxAmount, RefundAmount = line.RefundAmount });
        return result;
    }
}
