using Microsoft.EntityFrameworkCore;
using PrintQuote.Application.Common;
using PrintQuote.Application.Costing;
using PrintQuote.Application.Dtos;
using PrintQuote.Domain;

namespace PrintQuote.Application.Services;

// Create/read/update services for the rate masters. DELETE deactivates (IsActive = false) so that
// quotations, which keep their own rate snapshot, are never left pointing at a missing row.
// Profit rules are the exception: they are removed, because nothing references them.

public sealed class CompanyService(IAppDbContext db)
{
    public async Task<List<CompanyDto>> ListAsync(CancellationToken ct) =>
        await db.Companies.AsNoTracking().OrderBy(c => c.Name).Select(c => ToDto(c)).ToListAsync(ct);

    public async Task<CompanyDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await Find(id, ct));

    public async Task<CompanyDto> CreateAsync(CompanyInput input, CancellationToken ct)
    {
        var entity = new Company();
        Apply(entity, input);
        db.Companies.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, CompanyInput input, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        Apply(entity, input);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        (await Find(id, ct)).IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Company> Find(Guid id, CancellationToken ct) =>
        await db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Company", id);

    private static void Apply(Company entity, CompanyInput input)
    {
        Guard.Required(input.Name, "name");
        entity.Name = input.Name.Trim();
        entity.Address = input.Address?.Trim();
        entity.Phone = input.Phone?.Trim();
        entity.IsActive = input.IsActive;
    }

    private static CompanyDto ToDto(Company c) => new(c.Id, c.Name, c.Address, c.Phone, c.IsActive);
}

public sealed class CustomerService(IAppDbContext db)
{
    public async Task<List<CustomerDto>> ListAsync(CancellationToken ct) =>
        await db.Customers.AsNoTracking().OrderBy(c => c.Name).Select(c => ToDto(c)).ToListAsync(ct);

    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await Find(id, ct));

    public async Task<CustomerDto> CreateAsync(CustomerInput input, CancellationToken ct)
    {
        var entity = new Customer();
        Apply(entity, input);
        db.Customers.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, CustomerInput input, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        Apply(entity, input);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        (await Find(id, ct)).IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Customer> Find(Guid id, CancellationToken ct) =>
        await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Customer", id);

    private static void Apply(Customer entity, CustomerInput input)
    {
        var errors = new ErrorBag();
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add("name", "Enter a name.");
        }
        if (input.ProfitPercent is < 0)
        {
            errors.Add("profitPercent", "Must not be negative.");
        }
        errors.ThrowIfAny();
        entity.Name = input.Name.Trim();
        entity.Address = input.Address?.Trim();
        entity.Phone = input.Phone?.Trim();
        entity.Email = input.Email?.Trim();
        entity.ProfitPercent = input.ProfitPercent;
        entity.IsActive = input.IsActive;
    }

    private static CustomerDto ToDto(Customer c) => new(c.Id, c.Name, c.Address, c.Phone, c.Email, c.ProfitPercent, c.IsActive);
}

public sealed class PaperService(IAppDbContext db)
{
    public async Task<List<PaperDto>> ListAsync(CancellationToken ct) =>
        (await db.Papers.AsNoTracking().OrderBy(p => p.Gsm).ThenBy(p => p.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<PaperDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await Find(id, ct));

    public async Task<PaperDto> CreateAsync(PaperInput input, CancellationToken ct)
    {
        var entity = new Paper();
        Apply(entity, input);
        db.Papers.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<PaperDto> UpdateAsync(Guid id, PaperInput input, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        Apply(entity, input);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        (await Find(id, ct)).IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Weight of 500 sheets: width × height (in²) → m², × gsm × 500 / 1000.</summary>
    public static decimal RimWeightKg(decimal widthIn, decimal heightIn, int gsm) =>
        Math.Round(widthIn * heightIn * 0.00064516m * gsm * 500 / 1000, 3);

    private async Task<Paper> Find(Guid id, CancellationToken ct) =>
        await db.Papers.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Paper", id);

    private static void Apply(Paper entity, PaperInput input)
    {
        var errors = new ErrorBag();
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add("name", "Enter a name.");
        }
        if (input.Gsm <= 0)
        {
            errors.Add("gsm", "GSM must be more than 0.");
        }
        if (input.SheetWidthIn <= 0 || input.SheetHeightIn <= 0)
        {
            errors.Add("sheetWidthIn", "Enter the sheet size.");
        }
        if (input.PricePerUnit < 0)
        {
            errors.Add("pricePerUnit", "Price must not be negative.");
        }
        errors.ThrowIfAny();
        entity.Name = input.Name.Trim();
        entity.PaperType = input.PaperType?.Trim() ?? "";
        entity.Gsm = input.Gsm;
        entity.SheetWidthIn = input.SheetWidthIn;
        entity.SheetHeightIn = input.SheetHeightIn;
        entity.Unit = input.Unit;
        entity.PricePerUnit = input.PricePerUnit;
        entity.EffectiveFrom = input.EffectiveFrom;
        entity.IsActive = input.IsActive;
    }

    private static PaperDto ToDto(Paper p)
    {
        var rimKg = RimWeightKg(p.SheetWidthIn, p.SheetHeightIn, p.Gsm);
        var kgPerUnit = p.Unit == PaperUnit.Rim ? rimKg : rimKg / 500;
        var perKg = kgPerUnit > 0 ? Math.Round(p.PricePerUnit / kgPerUnit, 2) : 0;
        return new PaperDto(p.Id, p.Name, p.PaperType, p.Gsm, p.SheetWidthIn, p.SheetHeightIn, p.Unit, p.PricePerUnit,
            p.EffectiveFrom, p.IsActive, rimKg, perKg);
    }
}

public sealed class PressService(IAppDbContext db)
{
    public async Task<List<PressDto>> ListAsync(CancellationToken ct) =>
        (await db.Presses.AsNoTracking().Include(p => p.RateSlabs).OrderBy(p => p.SheetWidthIn * p.SheetHeightIn).ToListAsync(ct))
            .Select(ToDto).ToList();

    public async Task<PressDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await Find(id, ct));

    public async Task<PressDto> CreateAsync(PressInput input, CancellationToken ct)
    {
        var entity = new Press();
        Apply(entity, input);
        db.Presses.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<PressDto> UpdateAsync(Guid id, PressInput input, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        db.PressRateSlabs.RemoveRange(entity.RateSlabs);
        Apply(entity, input);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        (await Find(id, ct)).IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Press> Find(Guid id, CancellationToken ct) =>
        await db.Presses.Include(p => p.RateSlabs).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Press", id);

    private void Apply(Press entity, PressInput input)
    {
        var errors = new ErrorBag();
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add("name", "Enter a name.");
        }
        if (input.SheetWidthIn <= 0 || input.SheetHeightIn <= 0)
        {
            errors.Add("sheetWidthIn", "Enter the sheet size.");
        }
        if (input.PiecesPerParentSheet <= 0)
        {
            errors.Add("piecesPerParentSheet", "Must be at least 1.");
        }
        if (input.FormaPerPlate <= 0)
        {
            errors.Add("formaPerPlate", "Must be more than 0.");
        }
        if (input.PlatePrice < 0 || input.MakeReadySheetsPerPlate < 0)
        {
            errors.Add("platePrice", "Plate price and make-ready must not be negative.");
        }
        Guard.Slabs(errors, "rateSlabs", input.RateSlabs?.Select(s => (decimal?)s.UpToImpressionsPerPlate).ToList(),
            input.RateSlabs?.Select(s => s.Rate).ToList());
        errors.ThrowIfAny();

        entity.Name = input.Name.Trim();
        entity.SheetWidthIn = input.SheetWidthIn;
        entity.SheetHeightIn = input.SheetHeightIn;
        entity.PiecesPerParentSheet = input.PiecesPerParentSheet;
        entity.PlatePrice = input.PlatePrice;
        entity.MakeReadySheetsPerPlate = input.MakeReadySheetsPerPlate;
        entity.FormaPerPlate = input.FormaPerPlate;
        entity.IsActive = input.IsActive;
        entity.RateSlabs = input.RateSlabs!
            .Select(s => new PressRateSlab { PressId = entity.Id, UpToImpressionsPerPlate = s.UpToImpressionsPerPlate, ChargeType = s.ChargeType, Rate = s.Rate })
            .ToList();
        foreach (var slab in entity.RateSlabs)
        {
            db.PressRateSlabs.Add(slab);
        }
    }

    private static PressDto ToDto(Press p) => new(
        p.Id, p.Name, p.SheetWidthIn, p.SheetHeightIn, p.PiecesPerParentSheet, p.PlatePrice, p.MakeReadySheetsPerPlate,
        p.FormaPerPlate, p.IsActive,
        p.RateSlabs
            .OrderBy(s => s.UpToImpressionsPerPlate is null).ThenBy(s => s.UpToImpressionsPerPlate)
            .Select(s => new PressSlabDto(s.Id, s.UpToImpressionsPerPlate, s.ChargeType, s.Rate)).ToList());
}

public sealed class FinishingOperationService(IAppDbContext db)
{
    public async Task<List<FinishingOperationDto>> ListAsync(CancellationToken ct) =>
        await db.FinishingOperations.AsNoTracking().OrderBy(f => f.Name).Select(f => ToDto(f)).ToListAsync(ct);

    public async Task<FinishingOperationDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await Find(id, ct));

    public async Task<FinishingOperationDto> CreateAsync(FinishingOperationInput input, CancellationToken ct)
    {
        var entity = new FinishingOperation();
        Apply(entity, input);
        db.FinishingOperations.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<FinishingOperationDto> UpdateAsync(Guid id, FinishingOperationInput input, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        Apply(entity, input);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        (await Find(id, ct)).IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private async Task<FinishingOperation> Find(Guid id, CancellationToken ct) =>
        await db.FinishingOperations.FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw new NotFoundException("Finishing operation", id);

    private static void Apply(FinishingOperation entity, FinishingOperationInput input)
    {
        Guard.Required(input.Name, "name");
        if (input.Rate < 0)
        {
            throw new ValidationException("rate", "Rate must not be negative.");
        }
        entity.Name = input.Name.Trim();
        entity.Basis = input.Basis;
        entity.Rate = input.Rate;
        entity.IsActive = input.IsActive;
    }

    private static FinishingOperationDto ToDto(FinishingOperation f) => new(f.Id, f.Name, f.Basis, f.Rate, f.IsActive);
}

public sealed class BindingTypeService(IAppDbContext db)
{
    public async Task<List<BindingTypeDto>> ListAsync(CancellationToken ct) =>
        (await db.BindingTypes.AsNoTracking().Include(b => b.Slabs).OrderBy(b => b.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<BindingTypeDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await Find(id, ct));

    public async Task<BindingTypeDto> CreateAsync(BindingTypeInput input, CancellationToken ct)
    {
        var entity = new BindingType();
        Apply(entity, input);
        db.BindingTypes.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<BindingTypeDto> UpdateAsync(Guid id, BindingTypeInput input, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        db.BindingRateSlabs.RemoveRange(entity.Slabs);
        Apply(entity, input);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        (await Find(id, ct)).IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private async Task<BindingType> Find(Guid id, CancellationToken ct) =>
        await db.BindingTypes.Include(b => b.Slabs).FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException("Binding type", id);

    private void Apply(BindingType entity, BindingTypeInput input)
    {
        var errors = new ErrorBag();
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add("name", "Enter a name.");
        }
        Guard.Slabs(errors, "slabs", input.Slabs?.Select(s => s.UpToForma).ToList(), input.Slabs?.Select(s => s.RatePerPiece).ToList());
        errors.ThrowIfAny();

        entity.Name = input.Name.Trim();
        entity.IsActive = input.IsActive;
        entity.Slabs = input.Slabs!
            .Select(s => new BindingRateSlab { BindingTypeId = entity.Id, UpToForma = s.UpToForma, RatePerPiece = s.RatePerPiece })
            .ToList();
        foreach (var slab in entity.Slabs)
        {
            db.BindingRateSlabs.Add(slab);
        }
    }

    private static BindingTypeDto ToDto(BindingType b) => new(
        b.Id, b.Name, b.IsActive,
        b.Slabs.OrderBy(s => s.UpToForma is null).ThenBy(s => s.UpToForma)
            .Select(s => new BindingSlabDto(s.Id, s.UpToForma, s.RatePerPiece)).ToList());
}

public sealed class ProfitRuleService(IAppDbContext db)
{
    public async Task<List<ProfitRuleDto>> ListAsync(CancellationToken ct) =>
        await db.ProfitRules.AsNoTracking().OrderBy(r => r.Priority).Select(r => ToDto(r)).ToListAsync(ct);

    public async Task<ProfitRuleDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await Find(id, ct));

    public async Task<ProfitRuleDto> CreateAsync(ProfitRuleInput input, CancellationToken ct)
    {
        var entity = new ProfitRule();
        Apply(entity, input);
        db.ProfitRules.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<ProfitRuleDto> UpdateAsync(Guid id, ProfitRuleInput input, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        Apply(entity, input);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        db.ProfitRules.Remove(await Find(id, ct));
        await db.SaveChangesAsync(ct);
    }

    private async Task<ProfitRule> Find(Guid id, CancellationToken ct) =>
        await db.ProfitRules.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Profit rule", id);

    private static void Apply(ProfitRule entity, ProfitRuleInput input)
    {
        var errors = new ErrorBag();
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add("name", "Enter a name.");
        }
        if (input.ProfitPercent < 0)
        {
            errors.Add("profitPercent", "Must not be negative.");
        }
        if (input.MaxQuantity is <= 0)
        {
            errors.Add("maxQuantity", "Leave blank for any quantity, or enter at least 1.");
        }
        errors.ThrowIfAny();
        entity.Name = input.Name.Trim();
        entity.Priority = input.Priority;
        entity.MaxQuantity = input.MaxQuantity;
        entity.ProfitPercent = input.ProfitPercent;
        entity.IsActive = input.IsActive;
    }

    private static ProfitRuleDto ToDto(ProfitRule r) => new(r.Id, r.Name, r.Priority, r.MaxQuantity, r.ProfitPercent, r.IsActive);
}

public sealed class SettingsService(IAppDbContext db)
{
    public async Task<SettingsDto> GetAsync(CancellationToken ct) => ToDto(await Load(ct));

    public async Task<SettingsDto> UpdateAsync(SettingsDto input, CancellationToken ct)
    {
        var errors = new ErrorBag();
        if (input.RimRoundingStep <= 0 || input.RimRoundingStep > 1)
        {
            errors.Add("rimRoundingStep", "Use a step between 0.01 and 1.");
        }
        if (input.DefaultCommissionPercent < 0 || input.DefaultOverheadPercent < 0 || input.DefaultMarketingPercent < 0)
        {
            errors.Add("defaultCommissionPercent", "Percentages must not be negative.");
        }
        errors.ThrowIfAny();

        var settings = await Load(ct);
        settings.RimRoundingStep = input.RimRoundingStep;
        settings.DefaultCommissionPercent = input.DefaultCommissionPercent;
        settings.DefaultOverheadPercent = input.DefaultOverheadPercent;
        settings.DefaultMarketingPercent = input.DefaultMarketingPercent;
        await db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    private async Task<AppSettings> Load(CancellationToken ct)
    {
        var settings = await db.Settings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new AppSettings { Id = AppSettings.SingletonId };
            db.Settings.Add(settings);
        }
        return settings;
    }

    private static SettingsDto ToDto(AppSettings s) =>
        new(s.RimRoundingStep, s.DefaultCommissionPercent, s.DefaultOverheadPercent, s.DefaultMarketingPercent);
}

public sealed class TemplateService(IAppDbContext db)
{
    public static readonly string[] FieldKeys =
        ["forma", "colors", "paperId", "pressId", "halfFormaPressId", "openWidthIn", "openHeightIn", "colorsFront", "colorsBack"];

    public async Task<List<TemplateDto>> ListAsync(CancellationToken ct) =>
        (await Query().AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<TemplateDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await Find(id, ct));

    public async Task<TemplateDto> CreateAsync(TemplateInput input, CancellationToken ct)
    {
        var entity = new ProductTemplate();
        await ApplyAsync(entity, input, ct);
        db.Templates.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<TemplateDto> UpdateAsync(Guid id, TemplateInput input, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        db.TemplateFields.RemoveRange(entity.Components.SelectMany(c => c.Fields));
        db.TemplateComponents.RemoveRange(entity.Components);
        db.TemplateExtras.RemoveRange(entity.Extras);
        await ApplyAsync(entity, input, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        (await Find(id, ct)).IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<ProductTemplate> Query() =>
        db.Templates.Include(t => t.Components).ThenInclude(c => c.Fields).Include(t => t.Extras);

    private async Task<ProductTemplate> Find(Guid id, CancellationToken ct) =>
        await Query().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Template", id);

    private async Task ApplyAsync(ProductTemplate entity, TemplateInput input, CancellationToken ct)
    {
        var errors = new ErrorBag();
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add("name", "Enter a name.");
        }
        if (input.Components is not { Count: > 0 })
        {
            errors.Add("components", "Add at least one component.");
        }
        if (input.DefaultBindingTypeId is { } bindingId && !await db.BindingTypes.AnyAsync(b => b.Id == bindingId, ct))
        {
            errors.Add("defaultBindingTypeId", "Unknown binding type.");
        }
        if (input.ProfitPercent is < 0)
        {
            errors.Add("profitPercent", "Must not be negative.");
        }
        var components = input.Components ?? [];
        for (var i = 0; i < components.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(components[i].Name))
            {
                errors.Add($"components[{i}].name", "Name the component.");
            }
            var fields = components[i].Fields ?? [];
            for (var f = 0; f < fields.Count; f++)
            {
                if (!FieldKeys.Contains(fields[f].FieldKey))
                {
                    errors.Add($"components[{i}].fields[{f}].fieldKey", $"Use one of: {string.Join(", ", FieldKeys)}.");
                }
                else if (fields[f].Behaviour == FieldBehaviour.Locked && string.IsNullOrWhiteSpace(fields[f].DefaultValue))
                {
                    errors.Add($"components[{i}].fields[{f}].defaultValue", "A locked field needs a value.");
                }
            }
            if (fields.GroupBy(f => f.FieldKey).Any(g => g.Count() > 1))
            {
                errors.Add($"components[{i}].fields", "Each field can appear only once.");
            }
        }
        errors.ThrowIfAny();

        entity.Name = input.Name.Trim();
        entity.Description = input.Description?.Trim();
        entity.DefaultPageWidthIn = input.DefaultPageWidthIn;
        entity.DefaultPageHeightIn = input.DefaultPageHeightIn;
        entity.DefaultBindingTypeId = input.DefaultBindingTypeId;
        entity.ProfitPercent = input.ProfitPercent;
        entity.IsActive = input.IsActive;
        entity.Components = components.Select((c, i) =>
        {
            var component = new TemplateComponent
            {
                TemplateId = entity.Id,
                Name = c.Name.Trim(),
                Measure = c.Measure,
                SortOrder = c.SortOrder == 0 ? i + 1 : c.SortOrder,
                CountsTowardBindingForma = c.CountsTowardBindingForma,
                DefaultFinishing = (c.DefaultFinishing ?? [])
                    .Select(f => new TemplateFinishing { FinishingOperationId = f.FinishingOperationId, Sides = f.Sides }).ToList()
            };
            component.Fields = (c.Fields ?? []).Select(f => new TemplateField
            {
                ComponentId = component.Id,
                FieldKey = f.FieldKey,
                Behaviour = f.Behaviour,
                DefaultValue = f.DefaultValue,
                AllowedValues = f.AllowedValues,
                Min = f.Min,
                Max = f.Max,
                Step = f.Step
            }).ToList();
            return component;
        }).ToList();
        entity.Extras = (input.Extras ?? [])
            .Select(e => new TemplateExtra { TemplateId = entity.Id, FinishingOperationId = e.FinishingOperationId, DefaultAmount = e.DefaultAmount })
            .ToList();

        foreach (var component in entity.Components)
        {
            db.TemplateComponents.Add(component);
            db.TemplateFields.AddRange(component.Fields);
        }
        db.TemplateExtras.AddRange(entity.Extras);
    }

    private static TemplateDto ToDto(ProductTemplate t) => new(
        t.Id, t.Name, t.Description, t.DefaultPageWidthIn, t.DefaultPageHeightIn, t.DefaultBindingTypeId, t.ProfitPercent, t.IsActive,
        t.Components.OrderBy(c => c.SortOrder).Select(c => new TemplateComponentDto(
            c.Id, c.Name, c.Measure, c.SortOrder, c.CountsTowardBindingForma,
            c.DefaultFinishing.Select(f => new FinishingSelection { FinishingOperationId = f.FinishingOperationId, Sides = f.Sides }).ToList(),
            c.Fields.OrderBy(f => Array.IndexOf(FieldKeys, f.FieldKey)).Select(f => new TemplateFieldDto(
                f.Id, f.FieldKey, f.Behaviour, f.DefaultValue, f.AllowedValues, f.Min, f.Max, f.Step)).ToList())).ToList(),
        t.Extras.Select(e => new TemplateExtraDto(e.Id, e.FinishingOperationId, e.DefaultAmount)).ToList());
}

internal static class Guard
{
    public static void Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(field, "This field is required.");
        }
    }

    /// <summary>Slabs need one open-ended slab, distinct bounds and non-negative rates.</summary>
    public static void Slabs(ErrorBag errors, string field, List<decimal?>? bounds, List<decimal>? rates)
    {
        if (bounds is not { Count: > 0 } || rates is null)
        {
            errors.Add(field, "Add at least one slab.");
            return;
        }
        if (bounds.Count(b => b is null) != 1)
        {
            errors.Add(field, "Exactly one slab must be open-ended (blank upper limit).");
        }
        if (bounds.Where(b => b is not null).GroupBy(b => b).Any(g => g.Count() > 1))
        {
            errors.Add(field, "Two slabs have the same upper limit.");
        }
        if (bounds.Any(b => b is <= 0) || rates.Any(r => r < 0))
        {
            errors.Add(field, "Limits must be more than 0 and rates must not be negative.");
        }
    }
}
