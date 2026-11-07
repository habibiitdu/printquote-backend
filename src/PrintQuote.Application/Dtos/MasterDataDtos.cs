using PrintQuote.Application.Costing;
using PrintQuote.Domain;

namespace PrintQuote.Application.Dtos;

public sealed record CompanyDto(Guid Id, string Name, string? Address, string? Phone, bool IsActive);
public sealed record CompanyInput(string Name, string? Address, string? Phone, bool IsActive = true);

public sealed record CustomerDto(Guid Id, string Name, string? Address, string? Phone, string? Email, decimal? ProfitPercent, bool IsActive);
public sealed record CustomerInput(string Name, string? Address, string? Phone, string? Email, decimal? ProfitPercent, bool IsActive = true);

public sealed record PaperDto(
    Guid Id, string Name, string PaperType, int Gsm, decimal SheetWidthIn, decimal SheetHeightIn,
    PaperUnit Unit, decimal PricePerUnit, DateOnly EffectiveFrom, bool IsActive,
    decimal RimWeightKg, decimal PricePerKg);
public sealed record PaperInput(
    string Name, string PaperType, int Gsm, decimal SheetWidthIn, decimal SheetHeightIn,
    PaperUnit Unit, decimal PricePerUnit, DateOnly EffectiveFrom, bool IsActive = true);

public sealed record PressSlabDto(Guid? Id, int? UpToImpressionsPerPlate, PrintChargeType ChargeType, decimal Rate);
public sealed record PressDto(
    Guid Id, string Name, decimal SheetWidthIn, decimal SheetHeightIn, int PiecesPerParentSheet,
    decimal PlatePrice, int MakeReadySheetsPerPlate, decimal FormaPerPlate, bool IsActive, List<PressSlabDto> RateSlabs);
public sealed record PressInput(
    string Name, decimal SheetWidthIn, decimal SheetHeightIn, int PiecesPerParentSheet,
    decimal PlatePrice, int MakeReadySheetsPerPlate, decimal FormaPerPlate, bool IsActive, List<PressSlabDto> RateSlabs);

public sealed record FinishingOperationDto(Guid Id, string Name, FinishingBasis Basis, decimal Rate, bool IsActive);
public sealed record FinishingOperationInput(string Name, FinishingBasis Basis, decimal Rate, bool IsActive = true);

public sealed record BindingSlabDto(Guid? Id, decimal? UpToForma, decimal RatePerPiece);
public sealed record BindingTypeDto(Guid Id, string Name, bool IsActive, List<BindingSlabDto> Slabs);
public sealed record BindingTypeInput(string Name, bool IsActive, List<BindingSlabDto> Slabs);

public sealed record ProfitRuleDto(Guid Id, string Name, int Priority, int? MaxQuantity, decimal ProfitPercent, bool IsActive);
public sealed record ProfitRuleInput(string Name, int Priority, int? MaxQuantity, decimal ProfitPercent, bool IsActive = true);

public sealed record SettingsDto(decimal RimRoundingStep, decimal DefaultCommissionPercent, decimal DefaultOverheadPercent, decimal DefaultMarketingPercent);

public sealed record TemplateFieldDto(
    Guid? Id, string FieldKey, FieldBehaviour Behaviour, string? DefaultValue, string? AllowedValues,
    decimal? Min, decimal? Max, decimal? Step);
public sealed record TemplateComponentDto(
    Guid? Id, string Name, MeasureMode Measure, int SortOrder, bool CountsTowardBindingForma,
    List<FinishingSelection> DefaultFinishing, List<TemplateFieldDto> Fields);
public sealed record TemplateExtraDto(Guid? Id, Guid FinishingOperationId, decimal? DefaultAmount);
public sealed record TemplateDto(
    Guid Id, string Name, string? Description, decimal? DefaultPageWidthIn, decimal? DefaultPageHeightIn,
    Guid? DefaultBindingTypeId, decimal? ProfitPercent, bool IsActive,
    List<TemplateComponentDto> Components, List<TemplateExtraDto> Extras);
public sealed record TemplateInput(
    string Name, string? Description, decimal? DefaultPageWidthIn, decimal? DefaultPageHeightIn,
    Guid? DefaultBindingTypeId, decimal? ProfitPercent, bool IsActive,
    List<TemplateComponentDto> Components, List<TemplateExtraDto> Extras);
