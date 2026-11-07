using System.Globalization;
using PrintQuote.Application.Common;
using PrintQuote.Domain;

namespace PrintQuote.Application.Costing;

/// <summary>
/// Prices a <see cref="JobSpec"/> against a <see cref="RateBook"/>. Pure: no I/O, no clock.
/// The rules are documented in docs/technical-design.md, section 4. Keep the two in step.
/// </summary>
public static class CostingEngine
{
    private const decimal Tolerance = 0.000000001m;
    private const int SheetsPerRim = 500;
    private static readonly CultureInfo Numbers = CultureInfo.GetCultureInfo("en-IN");

    public static CostSheet Calculate(JobSpec spec, RateBook rates)
    {
        Validate(spec, rates);

        var qty = spec.Quantity;
        var plateLines = new Dictionary<Guid, (PressRate Press, int Plates)>();
        var paperLines = new List<PendingLine>();
        var conversionLines = new List<PendingLine>();
        var trace = new List<string>();
        var warnings = new List<string>();
        var description = new List<string>();
        decimal bindingForma = 0;

        if (spec.PageWidthIn is { } pw && spec.PageHeightIn is { } ph)
        {
            description.Add($"Size: {N(pw)}\" × {N(ph)}\"");
        }

        foreach (var component in spec.Components)
        {
            var paper = rates.Papers.Single(p => p.Id == component.PaperId);
            decimal parentSheets = 0;
            decimal areaPerCopy;

            if (component.Measure == MeasureMode.Forma)
            {
                var forma = component.Forma!.Value;
                var colors = component.Colors!.Value;
                if (component.CountsTowardBindingForma)
                {
                    bindingForma += forma;
                }

                foreach (var (partForma, press) in SplitForma(component, forma, rates))
                {
                    var platesPerColour = (int)Math.Ceiling(partForma / press.FormaPerPlate - Tolerance);
                    var plates = platesPerColour * colors;
                    var netSheets = partForma * qty / (2 * press.FormaPerPlate);
                    var pressSheets = Math.Ceiling(netSheets + plates * press.MakeReadySheetsPerPlate - Tolerance);
                    var impressions = pressSheets * 2 * colors;
                    parentSheets += pressSheets / press.PiecesPerParentSheet;
                    AddPlates(plateLines, press, plates);

                    var print = PrintLine(press, component.Name, plates, impressions);
                    conversionLines.Add(print.Line);
                    trace.Add($"{component.Name}: {N(partForma)} forma on {press.Name}, work-and-turn. " +
                              $"{N(netSheets)} sheets + {plates} plates × {press.MakeReadySheetsPerPlate} make-ready = {N(pressSheets)} {press.Name} sheets. " +
                              $"{plates} plates ({platesPerColour} per colour × {colors} colour). " +
                              $"{N(impressions)} impressions: {print.Reason}.");
                }

                areaPerCopy = forma * 4 * (spec.PageWidthIn ?? 0) * (spec.PageHeightIn ?? 0);
                description.Add($"{component.Name}: {N(forma)} forma, {paper.Name}, {colors} colour");
            }
            else
            {
                var press = rates.Presses.Single(p => p.Id == component.PressId);
                var width = component.OpenWidthIn!.Value;
                var height = component.OpenHeightIn!.Value;
                var front = component.ColorsFront!.Value;
                var back = component.ColorsBack ?? 0;
                var ups = Ups(width, height, press);
                var plates = front + back;
                var pressSheets = Math.Ceiling((decimal)qty / ups - Tolerance) + plates * press.MakeReadySheetsPerPlate;
                var impressions = pressSheets * plates;
                parentSheets = pressSheets / press.PiecesPerParentSheet;
                AddPlates(plateLines, press, plates);

                var print = PrintLine(press, component.Name, plates, impressions);
                conversionLines.Add(print.Line);
                trace.Add($"{component.Name}: {N(width)}\" × {N(height)}\" fits {ups} up on {press.Name} ({N(press.SheetWidthIn)}\" × {N(press.SheetHeightIn)}\"). " +
                          $"{N(Math.Ceiling((decimal)qty / ups - Tolerance))} sheets + {plates} plates × {press.MakeReadySheetsPerPlate} make-ready = {N(pressSheets)} {press.Name} sheets. " +
                          $"{N(impressions)} impressions: {print.Reason}.");

                areaPerCopy = width * height;
                description.Add($"{component.Name}: {N(width)}\" × {N(height)}\", {paper.Name}, {front}/{back} colour");
            }

            paperLines.Add(PaperLine(paper, component.Name, parentSheets, rates.RimRoundingStep, trace));

            foreach (var selection in component.Finishing)
            {
                var operation = rates.Finishing.Single(f => f.Id == selection.FinishingOperationId);
                var sides = Math.Max(1, selection.Sides ?? 1);
                if (operation.Basis == FinishingBasis.PerSquareInch && areaPerCopy == 0)
                {
                    warnings.Add($"{operation.Name} on {component.Name} needs the finished page size; it was left out.");
                    continue;
                }
                conversionLines.Add(FinishingLine(operation, component.Name, qty, areaPerCopy * sides, null));
                description[^1] += operation.Basis == FinishingBasis.PerSquareInch
                    ? $", {operation.Name} ({sides} side{(sides > 1 ? "s" : "")})"
                    : $", {operation.Name}";
            }
        }

        if (spec.BindingTypeId is { } bindingId)
        {
            var binding = rates.Bindings.Single(b => b.Id == bindingId);
            var slab = binding.Slabs
                .OrderBy(s => s.UpToForma is null)
                .ThenBy(s => s.UpToForma)
                .FirstOrDefault(s => s.UpToForma is null || bindingForma <= s.UpToForma + Tolerance);
            if (slab is null)
            {
                warnings.Add($"{binding.Name} binding has no rate for {N(bindingForma)} forma; it was left out.");
            }
            else
            {
                conversionLines.Add(new PendingLine(CostCategory.Conversion, $"{binding.Name} binding", qty, "pcs", slab.RatePerPiece, null));
                trace.Add($"{binding.Name} binding: {N(bindingForma)} forma of pages falls in the slab priced at {N(slab.RatePerPiece)} per copy.");
            }
            description.Add($"Binding: {binding.Name}");
        }

        foreach (var extra in spec.Extras)
        {
            var operation = rates.Finishing.Single(f => f.Id == extra.FinishingOperationId);
            conversionLines.Add(FinishingLine(operation, null, qty, 0, extra.Amount));
        }

        // Assemble in cost-sheet order: plates, paper, then conversion.
        var pending = plateLines.Values
            .Select(p => new PendingLine(CostCategory.RawMaterial, $"{p.Press.Name} plate", p.Plates, "pcs", p.Press.PlatePrice, null))
            .Concat(paperLines)
            .Concat(conversionLines)
            .ToList();
        var lines = pending
            .Select((l, i) => new CostLine(i + 1, l.Category, l.Description, l.Quantity, l.Unit, l.Rate, Money(l.Quantity * l.Rate), l.Component))
            .ToList();

        var rawMaterials = lines.Where(l => l.Category == CostCategory.RawMaterial).Sum(l => l.Total);
        var conversion = lines.Where(l => l.Category == CostCategory.Conversion).Sum(l => l.Total);
        var production = rawMaterials + conversion;
        var commission = Money(production * spec.CommissionPercent / 100);
        var overhead = Money(production * spec.OverheadPercent / 100);
        var marketing = Money(production * spec.MarketingPercent / 100);
        var totalCost = production + commission + overhead + marketing;

        var (profitPercent, profitSource) = ResolveProfit(spec, rates);
        if (profitSource == "none")
        {
            warnings.Add("No profit rule matched this quantity, so no profit was added.");
        }
        var profit = Money(totalCost * profitPercent / 100);
        var totalBill = totalCost + profit;

        return new CostSheet
        {
            Quantity = qty,
            DescriptionLines = description,
            Lines = lines,
            RawMaterialsTotal = rawMaterials,
            ConversionTotal = conversion,
            ProductionCost = production,
            Commission = commission,
            Overhead = overhead,
            Marketing = marketing,
            TotalCost = totalCost,
            ProfitPercent = profitPercent,
            ProfitSource = profitSource,
            Profit = profit,
            TotalBill = totalBill,
            RatePerPiece = Money(totalBill / qty),
            Trace = trace,
            Warnings = warnings
        };
    }

    /// <summary>Prices the same job at other quantities.</summary>
    public static List<QuantityBreak> Breaks(JobSpec spec, RateBook rates, IEnumerable<int> quantities) =>
        quantities
            .Where(q => q > 0)
            .Distinct()
            .Order()
            .Select(q =>
            {
                var sheet = Calculate(spec with { Quantity = q }, rates);
                return new QuantityBreak(q, sheet.ProfitPercent, sheet.TotalBill, sheet.RatePerPiece);
            })
            .ToList();

    /// <summary>Most pieces of w × h that fit on the press sheet, trying both directions.</summary>
    public static int Ups(decimal width, decimal height, PressRate press)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }
        var straight = Fit(press.SheetWidthIn, width) * Fit(press.SheetHeightIn, height);
        var turned = Fit(press.SheetWidthIn, height) * Fit(press.SheetHeightIn, width);
        return Math.Max(straight, turned);

        static int Fit(decimal sheet, decimal piece) => (int)Math.Floor(sheet / piece + Tolerance);
    }

    /// <summary>Rounds up to a multiple of <paramref name="step"/>.</summary>
    public static decimal CeilTo(decimal value, decimal step) =>
        step <= 0 ? value : Math.Ceiling(value / step - Tolerance) * step;

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string N(decimal value) => value.ToString("#,##0.###", Numbers);

    private static IEnumerable<(decimal Forma, PressRate Press)> SplitForma(ComponentSpec component, decimal forma, RateBook rates)
    {
        var main = rates.Presses.Single(p => p.Id == component.PressId);
        var whole = Math.Floor(forma);
        var fraction = forma - whole;
        if (component.HalfFormaPressId is { } halfId && whole >= 1 && fraction > 0)
        {
            yield return (whole, main);
            yield return (fraction, rates.Presses.Single(p => p.Id == halfId));
        }
        else
        {
            yield return (forma, main);
        }
    }

    private static void AddPlates(Dictionary<Guid, (PressRate Press, int Plates)> plateLines, PressRate press, int plates)
    {
        plateLines[press.Id] = plateLines.TryGetValue(press.Id, out var existing)
            ? (press, existing.Plates + plates)
            : (press, plates);
    }

    private static (PendingLine Line, string Reason) PrintLine(PressRate press, string component, int plates, decimal impressions)
    {
        var perPlate = impressions / plates;
        var slab = press.Slabs
            .OrderBy(s => s.UpToImpressionsPerPlate is null)
            .ThenBy(s => s.UpToImpressionsPerPlate)
            .FirstOrDefault(s => s.UpToImpressionsPerPlate is null || perPlate <= s.UpToImpressionsPerPlate)
            ?? throw new ValidationException("pressId", $"{press.Name} has no print rate for {N(perPlate)} impressions per plate.");

        var description = $"Print {press.Name} ({component})";
        return slab.ChargeType == PrintChargeType.FlatPerPlate
            ? (new PendingLine(CostCategory.Conversion, description, plates, "plate", slab.Rate, component),
               $"{N(perPlate)} per plate is a short run, flat {N(slab.Rate)} per plate")
            : (new PendingLine(CostCategory.Conversion, description, impressions / 1000, "thousand", slab.Rate, component),
               $"{N(perPlate)} per plate, {N(slab.Rate)} per 1,000");
    }

    private static PendingLine PaperLine(PaperRate paper, string component, decimal parentSheets, decimal rimStep, List<string> trace)
    {
        var description = $"{paper.Name} ({component})";
        if (paper.Unit == PaperUnit.Rim)
        {
            var rims = CeilTo(parentSheets / SheetsPerRim, rimStep);
            trace.Add($"{component} paper: {N(parentSheets)} parent sheets ÷ 500 = {N(parentSheets / SheetsPerRim)}, rounded up to {N(rims)} rim.");
            return new PendingLine(CostCategory.RawMaterial, description, rims, "rim", paper.PricePerUnit, component);
        }

        var sheets = Math.Ceiling(parentSheets - Tolerance);
        trace.Add($"{component} paper: {N(sheets)} parent sheets.");
        return new PendingLine(CostCategory.RawMaterial, description, sheets, "sheet", paper.PricePerUnit, component);
    }

    private static PendingLine FinishingLine(FinishingRate operation, string? component, int qty, decimal areaPerCopy, decimal? amount)
    {
        var description = component is null ? operation.Name : $"{operation.Name} ({component})";
        return operation.Basis switch
        {
            FinishingBasis.PerSquareInch => new(CostCategory.Conversion, description, areaPerCopy * qty, "sq. inch", operation.Rate, component),
            FinishingBasis.PerThousand => new(CostCategory.Conversion, description, qty / 1000m, "thousand", operation.Rate, component),
            FinishingBasis.PerPiece => new(CostCategory.Conversion, description, qty, "pcs", operation.Rate, component),
            _ => new(CostCategory.Conversion, description, 1, "job", amount ?? operation.Rate, component)
        };
    }

    private static (decimal Percent, string Source) ResolveProfit(JobSpec spec, RateBook rates)
    {
        if (spec.ProfitPercentOverride is { } overridden)
        {
            return (overridden, "override");
        }
        if (spec.CustomerId is { } customerId && rates.CustomerMargins.TryGetValue(customerId, out var customerMargin))
        {
            return (customerMargin, "customer");
        }
        if (spec.TemplateId is { } templateId && rates.TemplateMargins.TryGetValue(templateId, out var templateMargin))
        {
            return (templateMargin, "template");
        }
        var rule = rates.ProfitRules
            .OrderBy(r => r.Priority)
            .FirstOrDefault(r => r.MaxQuantity is null || spec.Quantity <= r.MaxQuantity);
        return rule is null ? (0, "none") : (rule.ProfitPercent, $"rule:{rule.Name}");
    }

    private static void Validate(JobSpec spec, RateBook rates)
    {
        var errors = new ErrorBag();
        if (spec.Quantity <= 0)
        {
            errors.Add("quantity", "Quantity must be at least 1.");
        }
        if (spec.Components.Count == 0)
        {
            errors.Add("components", "Add at least one component.");
        }
        foreach (var (percent, field) in new[] {
            (spec.CommissionPercent, "commissionPercent"), (spec.OverheadPercent, "overheadPercent"), (spec.MarketingPercent, "marketingPercent") })
        {
            if (percent < 0)
            {
                errors.Add(field, "Must not be negative.");
            }
        }

        for (var i = 0; i < spec.Components.Count; i++)
        {
            var c = spec.Components[i];
            var at = $"components[{i}]";
            if (string.IsNullOrWhiteSpace(c.Name))
            {
                errors.Add($"{at}.name", "Name the component.");
            }
            if (rates.Papers.All(p => p.Id != c.PaperId))
            {
                errors.Add($"{at}.paperId", "Choose a paper.");
            }
            var press = rates.Presses.SingleOrDefault(p => p.Id == c.PressId);
            if (press is null)
            {
                errors.Add($"{at}.pressId", "Choose a press.");
            }

            if (c.Measure == MeasureMode.Forma)
            {
                if (c.Forma is not > 0)
                {
                    errors.Add($"{at}.forma", "Forma must be more than 0.");
                }
                else if (c.Forma.Value * 8 % 1 != 0)
                {
                    errors.Add($"{at}.forma", "Forma must be a multiple of 0.125.");
                }
                if (c.Colors is not (>= 1 and <= 4))
                {
                    errors.Add($"{at}.colors", "Colours must be 1 to 4.");
                }
                if (c.HalfFormaPressId is { } half && rates.Presses.All(p => p.Id != half))
                {
                    errors.Add($"{at}.halfFormaPressId", "Choose a press for the half forma.");
                }
                if (press is not null && press.FormaPerPlate <= 0)
                {
                    errors.Add($"{at}.pressId", $"{press.Name} has no forma-per-plate setting.");
                }
            }
            else
            {
                if (c.OpenWidthIn is not > 0 || c.OpenHeightIn is not > 0)
                {
                    errors.Add($"{at}.openWidthIn", "Enter the open size.");
                }
                else if (press is not null && Ups(c.OpenWidthIn.Value, c.OpenHeightIn.Value, press) == 0)
                {
                    errors.Add($"{at}.pressId", $"{N(c.OpenWidthIn.Value)}\" × {N(c.OpenHeightIn.Value)}\" does not fit on {press.Name}.");
                }
                if (c.ColorsFront is not (>= 1 and <= 4))
                {
                    errors.Add($"{at}.colorsFront", "Front colours must be 1 to 4.");
                }
                if (c.ColorsBack is < 0 or > 4)
                {
                    errors.Add($"{at}.colorsBack", "Back colours must be 0 to 4.");
                }
            }

            for (var f = 0; f < c.Finishing.Count; f++)
            {
                if (rates.Finishing.All(o => o.Id != c.Finishing[f].FinishingOperationId))
                {
                    errors.Add($"{at}.finishing[{f}]", "Unknown finishing operation.");
                }
            }
        }

        if (spec.BindingTypeId is { } bindingId && rates.Bindings.All(b => b.Id != bindingId))
        {
            errors.Add("bindingTypeId", "Unknown binding type.");
        }
        for (var e = 0; e < spec.Extras.Count; e++)
        {
            if (rates.Finishing.All(o => o.Id != spec.Extras[e].FinishingOperationId))
            {
                errors.Add($"extras[{e}]", "Unknown operation.");
            }
        }

        errors.ThrowIfAny();
    }

    private sealed record PendingLine(CostCategory Category, string Description, decimal Quantity, string Unit, decimal Rate, string? Component);
}
