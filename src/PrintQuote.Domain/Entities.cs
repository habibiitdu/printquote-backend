namespace PrintQuote.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A letterhead the press quotes under, e.g. Swift Print.</summary>
public class Company : Entity
{
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Customer : Entity
{
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    /// <summary>Overrides the profit rules for this customer when set.</summary>
    public decimal? ProfitPercent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Paper : Entity
{
    public string Name { get; set; } = "";
    public string PaperType { get; set; } = "";
    public int Gsm { get; set; }
    public decimal SheetWidthIn { get; set; } = 23;
    public decimal SheetHeightIn { get; set; } = 36;
    public PaperUnit Unit { get; set; } = PaperUnit.Rim;
    public decimal PricePerUnit { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Press : Entity
{
    public string Name { get; set; } = "";
    public decimal SheetWidthIn { get; set; }
    public decimal SheetHeightIn { get; set; }
    /// <summary>Press sheets cut from one parent sheet (Demy = 2, Double Demy = 1).</summary>
    public int PiecesPerParentSheet { get; set; } = 1;
    public decimal PlatePrice { get; set; }
    public int MakeReadySheetsPerPlate { get; set; }
    /// <summary>Forma (both sides, work-and-turn) one plate carries: Demy 0.5, Double Demy 1.</summary>
    public decimal FormaPerPlate { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public List<PressRateSlab> RateSlabs { get; set; } = [];
}

public class PressRateSlab : Entity
{
    public Guid PressId { get; set; }
    /// <summary>Upper bound of impressions per plate; null means "and above".</summary>
    public int? UpToImpressionsPerPlate { get; set; }
    public PrintChargeType ChargeType { get; set; }
    public decimal Rate { get; set; }
}

public class FinishingOperation : Entity
{
    public string Name { get; set; } = "";
    public FinishingBasis Basis { get; set; }
    public decimal Rate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BindingType : Entity
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<BindingRateSlab> Slabs { get; set; } = [];
}

public class BindingRateSlab : Entity
{
    public Guid BindingTypeId { get; set; }
    /// <summary>Upper bound of inner forma; null means "and above".</summary>
    public decimal? UpToForma { get; set; }
    public decimal RatePerPiece { get; set; }
}

public class ProfitRule : Entity
{
    public string Name { get; set; } = "";
    public int Priority { get; set; }
    /// <summary>Applies when quantity is at most this; null matches any quantity.</summary>
    public int? MaxQuantity { get; set; }
    public decimal ProfitPercent { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Single-row global settings.</summary>
public class AppSettings : Entity
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    public decimal RimRoundingStep { get; set; } = 0.05m;
    public decimal DefaultCommissionPercent { get; set; }
    public decimal DefaultOverheadPercent { get; set; }
    public decimal DefaultMarketingPercent { get; set; }
}

public class ProductTemplate : Entity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal? DefaultPageWidthIn { get; set; }
    public decimal? DefaultPageHeightIn { get; set; }
    public Guid? DefaultBindingTypeId { get; set; }
    public decimal? ProfitPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public List<TemplateComponent> Components { get; set; } = [];
    public List<TemplateExtra> Extras { get; set; } = [];
}

public class TemplateComponent : Entity
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = "";
    public MeasureMode Measure { get; set; }
    public int SortOrder { get; set; }
    public bool CountsTowardBindingForma { get; set; }
    public List<TemplateFinishing> DefaultFinishing { get; set; } = [];
    public List<TemplateField> Fields { get; set; } = [];
}

/// <summary>A finishing operation pre-selected on a template component (stored as JSON).</summary>
public class TemplateFinishing
{
    public Guid FinishingOperationId { get; set; }
    public int? Sides { get; set; }
}

public class TemplateField : Entity
{
    public Guid ComponentId { get; set; }
    public string FieldKey { get; set; } = "";
    public FieldBehaviour Behaviour { get; set; }
    public string? DefaultValue { get; set; }
    public string? AllowedValues { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public decimal? Step { get; set; }
}

public class TemplateExtra : Entity
{
    public Guid TemplateId { get; set; }
    public Guid FinishingOperationId { get; set; }
    public decimal? DefaultAmount { get; set; }
}

public class Quotation : Entity
{
    public string Number { get; set; } = "";
    public string JobName { get; set; } = "";
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? TemplateId { get; set; }
    public ProductTemplate? Template { get; set; }
    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;
    public int CurrentVersion { get; set; }
    public int Quantity { get; set; }
    public decimal TotalBill { get; set; }
    public decimal RatePerPiece { get; set; }
    public List<QuotationVersion> Versions { get; set; } = [];
}

public class QuotationVersion : Entity
{
    public Guid QuotationId { get; set; }
    public int VersionNumber { get; set; }
    public string? Note { get; set; }
    /// <summary>The job specification, as JSON.</summary>
    public string SpecJson { get; set; } = "{}";
    /// <summary>The calculated cost sheet, as JSON.</summary>
    public string CostSheetJson { get; set; } = "{}";
    /// <summary>Every rate the version was priced with, as JSON.</summary>
    public string RateSnapshotJson { get; set; } = "{}";
}
