namespace PrintQuote.Domain;

/// <summary>How a paper is bought and priced.</summary>
public enum PaperUnit
{
    /// <summary>500 parent sheets.</summary>
    Rim,
    /// <summary>A single parent sheet (art card).</summary>
    Sheet
}

/// <summary>How a print rate slab charges.</summary>
public enum PrintChargeType
{
    /// <summary>A flat amount per plate (short runs).</summary>
    FlatPerPlate,
    /// <summary>An amount per 1,000 impressions.</summary>
    PerThousand
}

/// <summary>What a finishing operation's rate is multiplied by.</summary>
public enum FinishingBasis
{
    PerSquareInch,
    PerThousand,
    PerPiece,
    Fixed
}

/// <summary>How a component's size is expressed.</summary>
public enum MeasureMode
{
    /// <summary>Pages, in forma (one Demy sheet printed both sides).</summary>
    Forma,
    /// <summary>A flat piece imposed n-up on a press sheet.</summary>
    Ups
}

/// <summary>How a template field appears on the quotation form.</summary>
public enum FieldBehaviour
{
    Ask,
    Default,
    Locked
}

public enum QuotationStatus
{
    Draft,
    Sent,
    Approved,
    Lost
}

/// <summary>Which block of the cost sheet a line belongs to.</summary>
public enum CostCategory
{
    RawMaterial,
    Conversion
}
