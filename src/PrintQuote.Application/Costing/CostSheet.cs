using PrintQuote.Domain;

namespace PrintQuote.Application.Costing;

/// <summary>The priced job, in the same shape as the press's Excel cost sheet.</summary>
public sealed record CostSheet
{
    public int Quantity { get; init; }
    public List<string> DescriptionLines { get; init; } = [];
    public List<CostLine> Lines { get; init; } = [];
    public decimal RawMaterialsTotal { get; init; }
    public decimal ConversionTotal { get; init; }
    public decimal ProductionCost { get; init; }
    public decimal Commission { get; init; }
    public decimal Overhead { get; init; }
    public decimal Marketing { get; init; }
    public decimal TotalCost { get; init; }
    public decimal ProfitPercent { get; init; }
    /// <summary>override, customer, template, rule:&lt;name&gt; or none.</summary>
    public string ProfitSource { get; init; } = "";
    public decimal Profit { get; init; }
    public decimal TotalBill { get; init; }
    public decimal RatePerPiece { get; init; }
    public List<string> Trace { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
}

public sealed record CostLine(
    int Sl,
    CostCategory Category,
    string Description,
    decimal Quantity,
    string Unit,
    decimal Rate,
    decimal Total,
    string? Component);

public sealed record QuantityBreak(int Quantity, decimal ProfitPercent, decimal TotalBill, decimal RatePerPiece);
