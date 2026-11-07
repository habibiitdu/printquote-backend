using PrintQuote.Domain;

namespace PrintQuote.Application.Costing;

/// <summary>Everything the engine needs to know about one job, independent of prices.</summary>
public sealed record JobSpec
{
    public int Quantity { get; init; }
    /// <summary>Finished page size; needed for the lamination area of forma components.</summary>
    public decimal? PageWidthIn { get; init; }
    public decimal? PageHeightIn { get; init; }
    public List<ComponentSpec> Components { get; init; } = [];
    public Guid? BindingTypeId { get; init; }
    public List<ExtraSpec> Extras { get; init; } = [];
    public decimal CommissionPercent { get; init; }
    public decimal OverheadPercent { get; init; }
    public decimal MarketingPercent { get; init; }
    public decimal? ProfitPercentOverride { get; init; }
    /// <summary>Used only to resolve the profit percent.</summary>
    public Guid? CustomerId { get; init; }
    /// <summary>Used only to resolve the profit percent.</summary>
    public Guid? TemplateId { get; init; }
}

public sealed record ComponentSpec
{
    public string Name { get; init; } = "";
    public MeasureMode Measure { get; init; }
    public Guid PaperId { get; init; }
    public Guid PressId { get; init; }

    // Forma measure
    public decimal? Forma { get; init; }
    /// <summary>Colours on both sides (work-and-turn).</summary>
    public int? Colors { get; init; }
    /// <summary>When set, the fractional part of the forma prints on this press.</summary>
    public Guid? HalfFormaPressId { get; init; }
    public bool CountsTowardBindingForma { get; init; }

    // Ups measure
    public decimal? OpenWidthIn { get; init; }
    public decimal? OpenHeightIn { get; init; }
    public int? ColorsFront { get; init; }
    public int? ColorsBack { get; init; }

    public List<FinishingSelection> Finishing { get; init; } = [];
}

public sealed record FinishingSelection
{
    public Guid FinishingOperationId { get; init; }
    /// <summary>Sides to treat; used by per-square-inch operations (default 1).</summary>
    public int? Sides { get; init; }
}

/// <summary>A job-level line such as cutting &amp; packing or delivery.</summary>
public sealed record ExtraSpec
{
    public Guid FinishingOperationId { get; init; }
    /// <summary>For fixed-basis operations, replaces the configured amount.</summary>
    public decimal? Amount { get; init; }
}
