using Microsoft.EntityFrameworkCore;
using PrintQuote.Application.Common;
using PrintQuote.Application.Costing;
using PrintQuote.Application.Dtos;
using PrintQuote.Domain;

namespace PrintQuote.Application.Services;

/// <summary>Builds the rate book from the database and runs the engine.</summary>
public sealed class CostingService(IAppDbContext db)
{
    /// <summary>Loads every active rate. Small tables, so one read per calculation is fine.</summary>
    public async Task<RateBook> LoadRateBookAsync(CancellationToken ct)
    {
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new AppSettings();
        var papers = await db.Papers.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);
        var presses = await db.Presses.AsNoTracking().Include(p => p.RateSlabs).Where(p => p.IsActive).ToListAsync(ct);
        var finishing = await db.FinishingOperations.AsNoTracking().Where(f => f.IsActive).ToListAsync(ct);
        var bindings = await db.BindingTypes.AsNoTracking().Include(b => b.Slabs).Where(b => b.IsActive).ToListAsync(ct);
        var rules = await db.ProfitRules.AsNoTracking().Where(r => r.IsActive).ToListAsync(ct);
        var customerMargins = await db.Customers.AsNoTracking()
            .Where(c => c.ProfitPercent != null)
            .ToDictionaryAsync(c => c.Id, c => c.ProfitPercent!.Value, ct);
        var templateMargins = await db.Templates.AsNoTracking()
            .Where(t => t.ProfitPercent != null)
            .ToDictionaryAsync(t => t.Id, t => t.ProfitPercent!.Value, ct);

        return new RateBook
        {
            RimRoundingStep = settings.RimRoundingStep,
            Papers = papers.Select(p => new PaperRate(p.Id, p.Name, p.Gsm, p.Unit, p.PricePerUnit)).ToList(),
            Presses = presses.Select(p => new PressRate(
                p.Id, p.Name, p.SheetWidthIn, p.SheetHeightIn, p.PiecesPerParentSheet, p.PlatePrice,
                p.MakeReadySheetsPerPlate, p.FormaPerPlate,
                p.RateSlabs.Select(s => new PressSlabRate(s.UpToImpressionsPerPlate, s.ChargeType, s.Rate)).ToList())).ToList(),
            Finishing = finishing.Select(f => new FinishingRate(f.Id, f.Name, f.Basis, f.Rate)).ToList(),
            Bindings = bindings.Select(b => new BindingRate(
                b.Id, b.Name, b.Slabs.Select(s => new BindingSlabRate(s.UpToForma, s.RatePerPiece)).ToList())).ToList(),
            ProfitRules = rules.Select(r => new ProfitRuleRate(r.Name, r.Priority, r.MaxQuantity, r.ProfitPercent)).ToList(),
            CustomerMargins = customerMargins,
            TemplateMargins = templateMargins
        };
    }

    public async Task<CalculateResponse> CalculateAsync(CalculateRequest request, CancellationToken ct)
    {
        if (request.Spec is null)
        {
            throw new ValidationException("spec", "The job specification is required.");
        }
        var rates = await LoadRateBookAsync(ct);
        var sheet = CostingEngine.Calculate(request.Spec, rates);
        var breaks = request.BreakQuantities is { Count: > 0 } quantities
            ? CostingEngine.Breaks(request.Spec, rates, quantities)
            : [];
        return new CalculateResponse(sheet, breaks);
    }
}
