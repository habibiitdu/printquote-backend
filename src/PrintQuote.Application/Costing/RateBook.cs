using PrintQuote.Domain;

namespace PrintQuote.Application.Costing;

/// <summary>
/// Every price in force when a job is priced. Saved with each quotation version,
/// so re-reading an old quotation never depends on today's rates.
/// </summary>
public sealed record RateBook
{
    public decimal RimRoundingStep { get; init; } = 0.05m;
    public List<PaperRate> Papers { get; init; } = [];
    public List<PressRate> Presses { get; init; } = [];
    public List<FinishingRate> Finishing { get; init; } = [];
    public List<BindingRate> Bindings { get; init; } = [];
    public List<ProfitRuleRate> ProfitRules { get; init; } = [];
    public Dictionary<Guid, decimal> CustomerMargins { get; init; } = [];
    public Dictionary<Guid, decimal> TemplateMargins { get; init; } = [];
}

public sealed record PaperRate(Guid Id, string Name, int Gsm, PaperUnit Unit, decimal PricePerUnit);

public sealed record PressRate(
    Guid Id,
    string Name,
    decimal SheetWidthIn,
    decimal SheetHeightIn,
    int PiecesPerParentSheet,
    decimal PlatePrice,
    int MakeReadySheetsPerPlate,
    decimal FormaPerPlate,
    List<PressSlabRate> Slabs);

public sealed record PressSlabRate(int? UpToImpressionsPerPlate, PrintChargeType ChargeType, decimal Rate);

public sealed record FinishingRate(Guid Id, string Name, FinishingBasis Basis, decimal Rate);

public sealed record BindingRate(Guid Id, string Name, List<BindingSlabRate> Slabs);

public sealed record BindingSlabRate(decimal? UpToForma, decimal RatePerPiece);

public sealed record ProfitRuleRate(string Name, int Priority, int? MaxQuantity, decimal ProfitPercent);
