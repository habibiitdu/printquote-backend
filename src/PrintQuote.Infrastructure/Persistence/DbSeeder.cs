using Microsoft.EntityFrameworkCore;
using PrintQuote.Domain;

namespace PrintQuote.Infrastructure.Persistence;

/// <summary>
/// Fills an empty database with the rates read off the press's sample sheets
/// (docs/technical-design.md, section 8). Runs only when no press exists yet.
/// </summary>
public static class DbSeeder
{
    // Fixed ids so seeded rows are recognisable and templates can reference them.
    public static readonly Guid Offset55 = Guid.Parse("10000000-0000-0000-0000-000000000055");
    public static readonly Guid Offset70 = Guid.Parse("10000000-0000-0000-0000-000000000070");
    public static readonly Guid Offset100 = Guid.Parse("10000000-0000-0000-0000-000000000100");
    public static readonly Guid Art120 = Guid.Parse("10000000-0000-0000-0000-000000000120");
    public static readonly Guid Card300 = Guid.Parse("10000000-0000-0000-0000-000000000300");
    public static readonly Guid Demy = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid DoubleDemy = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid Lamination = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid Crease = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid CuttingPacking = Guid.Parse("30000000-0000-0000-0000-000000000003");
    public static readonly Guid Packing = Guid.Parse("30000000-0000-0000-0000-000000000004");
    public static readonly Guid Delivery = Guid.Parse("30000000-0000-0000-0000-000000000005");
    public static readonly Guid Pin = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid Glue = Guid.Parse("40000000-0000-0000-0000-000000000002");

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Presses.AnyAsync(ct))
        {
            return;
        }

        var from = new DateOnly(2026, 1, 1);

        db.Settings.Add(new AppSettings { Id = AppSettings.SingletonId, RimRoundingStep = 0.05m });

        db.Companies.AddRange(
            new Company { Name = "Swift Print" },
            new Company { Name = "Quality Park Limited" });
        db.Customers.Add(new Customer { Name = "Panacea Publications", Address = "Kawran Bazar" });

        db.Papers.AddRange(
            Paper(Offset55, "55 gsm Offset Paper (Partex)", "Offset", 55, PaperUnit.Rim, 2250, from),
            Paper(Offset70, "70 gsm Offset Paper", "Offset", 70, PaperUnit.Rim, 2850, from),
            Paper(Offset100, "100 gsm Offset Paper (Partex)", "Offset", 100, PaperUnit.Rim, 4000, from),
            Paper(Art120, "120 gsm Art Paper", "Art paper", 120, PaperUnit.Rim, 4700, from),
            Paper(Card300, "300 gsm Art Card", "Art card", 300, PaperUnit.Sheet, 15, from));

        db.Presses.AddRange(
            new Press
            {
                Id = Demy, Name = "Demy", SheetWidthIn = 18, SheetHeightIn = 23, PiecesPerParentSheet = 2,
                PlatePrice = 180, MakeReadySheetsPerPlate = 25, FormaPerPlate = 0.5m,
                RateSlabs =
                [
                    Slab(2000, PrintChargeType.FlatPerPlate, 300),
                    Slab(3000, PrintChargeType.FlatPerPlate, 350),
                    Slab(6000, PrintChargeType.FlatPerPlate, 450),
                    Slab(null, PrintChargeType.PerThousand, 150)
                ]
            },
            new Press
            {
                Id = DoubleDemy, Name = "Double Demy", SheetWidthIn = 23, SheetHeightIn = 36, PiecesPerParentSheet = 1,
                PlatePrice = 370, MakeReadySheetsPerPlate = 20, FormaPerPlate = 1,
                RateSlabs =
                [
                    Slab(5000, PrintChargeType.FlatPerPlate, 600),
                    Slab(20000, PrintChargeType.PerThousand, 200),
                    Slab(null, PrintChargeType.PerThousand, 180)
                ]
            });

        db.FinishingOperations.AddRange(
            new FinishingOperation { Id = Lamination, Name = "Glue lamination", Basis = FinishingBasis.PerSquareInch, Rate = 0.004m },
            new FinishingOperation { Id = Crease, Name = "Cover crease", Basis = FinishingBasis.PerThousand, Rate = 150 },
            new FinishingOperation { Id = CuttingPacking, Name = "Cutting & packing", Basis = FinishingBasis.Fixed, Rate = 0 },
            new FinishingOperation { Id = Packing, Name = "Packing", Basis = FinishingBasis.Fixed, Rate = 1000 },
            new FinishingOperation { Id = Delivery, Name = "Delivery & others", Basis = FinishingBasis.Fixed, Rate = 5000 });

        db.BindingTypes.AddRange(
            new BindingType
            {
                Id = Pin, Name = "Pin",
                Slabs =
                [
                    BindingSlab(2, 0.7m), BindingSlab(4.5m, 0.8m), BindingSlab(5, 0.9m), BindingSlab(6.5m, 1.0m),
                    BindingSlab(7, 1.2m), BindingSlab(9, 1.3m), BindingSlab(null, 1.5m)
                ]
            },
            new BindingType { Id = Glue, Name = "Glue", Slabs = [BindingSlab(null, 0.8m)] });

        db.ProfitRules.AddRange(
            new ProfitRule { Name = "Short run", Priority = 1, MaxQuantity = 5000, ProfitPercent = 20 },
            new ProfitRule { Name = "Standard", Priority = 2, MaxQuantity = null, ProfitPercent = 10 });

        db.Templates.AddRange(LectureBook(), SingleSheet(), MapBook(), MemoryTest());

        await db.SaveChangesAsync(ct);
    }

    private static ProductTemplate LectureBook() => new()
    {
        Name = "Lecture sheet / book",
        Description = "0.5 forma cover and N forma inner pages, pin or glue bound.",
        DefaultPageWidthIn = 8.5m,
        DefaultPageHeightIn = 11,
        DefaultBindingTypeId = Pin,
        Components =
        [
            Component("Cover", MeasureMode.Forma, 1, false, [],
                Field("forma", FieldBehaviour.Locked, "0.5"),
                Field("colors", FieldBehaviour.Default, "2", "1, 2, 4"),
                Field("paperId", FieldBehaviour.Default, Offset70.ToString()),
                Field("pressId", FieldBehaviour.Locked, Demy.ToString())),
            Component("Inner", MeasureMode.Forma, 2, true, [],
                Field("forma", FieldBehaviour.Ask, null, null, 0.5m, 20, 0.5m),
                Field("colors", FieldBehaviour.Default, "1", "1, 2, 4"),
                Field("paperId", FieldBehaviour.Default, Offset55.ToString()),
                Field("pressId", FieldBehaviour.Default, DoubleDemy.ToString()),
                Field("halfFormaPressId", FieldBehaviour.Default, null))
        ],
        Extras = [Extra(Packing, 0), Extra(Delivery, 0)]
    };

    private static ProductTemplate SingleSheet() => new()
    {
        Name = "Single sheet (OMR, flyer)",
        Description = "One flat piece, imposed n-up on the press sheet.",
        ProfitPercent = 12,
        Components =
        [
            Component("Sheet", MeasureMode.Ups, 1, false, [],
                Field("openWidthIn", FieldBehaviour.Ask, null, null, 1, 36),
                Field("openHeightIn", FieldBehaviour.Ask, null, null, 1, 36),
                Field("colorsFront", FieldBehaviour.Default, "1", "1, 2, 4"),
                Field("colorsBack", FieldBehaviour.Default, "1", "0, 1, 2, 4"),
                Field("paperId", FieldBehaviour.Default, Offset100.ToString()),
                Field("pressId", FieldBehaviour.Default, Demy.ToString()))
        ],
        Extras = [Extra(CuttingPacking, null), Extra(Delivery, null)]
    };

    private static ProductTemplate MapBook() => new()
    {
        Name = "Map book",
        Description = "Laminated card cover and a Double Demy map, glue bound.",
        DefaultPageWidthIn = 9,
        DefaultPageHeightIn = 12,
        DefaultBindingTypeId = Glue,
        ProfitPercent = 10,
        Components =
        [
            Component("Cover", MeasureMode.Ups, 1, false,
                [new TemplateFinishing { FinishingOperationId = Lamination, Sides = 1 }, new TemplateFinishing { FinishingOperationId = Crease }],
                Field("openWidthIn", FieldBehaviour.Default, "18"),
                Field("openHeightIn", FieldBehaviour.Default, "12"),
                Field("colorsFront", FieldBehaviour.Locked, "4"),
                Field("colorsBack", FieldBehaviour.Locked, "0"),
                Field("paperId", FieldBehaviour.Default, Card300.ToString()),
                Field("pressId", FieldBehaviour.Locked, Demy.ToString())),
            Component("Map", MeasureMode.Ups, 2, false,
                [new TemplateFinishing { FinishingOperationId = Lamination, Sides = 1 }],
                Field("openWidthIn", FieldBehaviour.Default, "22"),
                Field("openHeightIn", FieldBehaviour.Default, "35"),
                Field("colorsFront", FieldBehaviour.Locked, "4"),
                Field("colorsBack", FieldBehaviour.Locked, "0"),
                Field("paperId", FieldBehaviour.Default, Art120.ToString()),
                Field("pressId", FieldBehaviour.Locked, DoubleDemy.ToString()))
        ],
        Extras = [Extra(Packing, null), Extra(Delivery, null)]
    };

    private static ProductTemplate MemoryTest() => new()
    {
        Name = "Memory test / class test",
        Description = "Loose leaves from 0.125 to 1 forma.",
        DefaultPageWidthIn = 8.5m,
        DefaultPageHeightIn = 11,
        Components =
        [
            Component("Paper", MeasureMode.Forma, 1, false, [],
                Field("forma", FieldBehaviour.Ask, null, "0.125, 0.25, 0.5, 1", 0.125m, 1, 0.125m),
                Field("colors", FieldBehaviour.Default, "1", "1, 2"),
                Field("paperId", FieldBehaviour.Default, Offset55.ToString()),
                Field("pressId", FieldBehaviour.Locked, Demy.ToString()))
        ],
        Extras = [Extra(CuttingPacking, null)]
    };

    private static Paper Paper(Guid id, string name, string type, int gsm, PaperUnit unit, decimal price, DateOnly from) => new()
    {
        Id = id, Name = name, PaperType = type, Gsm = gsm, SheetWidthIn = 23, SheetHeightIn = 36,
        Unit = unit, PricePerUnit = price, EffectiveFrom = from
    };

    private static PressRateSlab Slab(int? upTo, PrintChargeType type, decimal rate) =>
        new() { UpToImpressionsPerPlate = upTo, ChargeType = type, Rate = rate };

    private static BindingRateSlab BindingSlab(decimal? upTo, decimal rate) => new() { UpToForma = upTo, RatePerPiece = rate };

    private static TemplateComponent Component(
        string name, MeasureMode measure, int order, bool countsTowardBinding, List<TemplateFinishing> finishing, params TemplateField[] fields) => new()
    {
        Name = name, Measure = measure, SortOrder = order, CountsTowardBindingForma = countsTowardBinding,
        DefaultFinishing = finishing, Fields = [.. fields]
    };

    private static TemplateField Field(
        string key, FieldBehaviour behaviour, string? value, string? allowed = null, decimal? min = null, decimal? max = null, decimal? step = null) => new()
    {
        FieldKey = key, Behaviour = behaviour, DefaultValue = value, AllowedValues = allowed, Min = min, Max = max, Step = step
    };

    private static TemplateExtra Extra(Guid operation, decimal? amount) => new() { FinishingOperationId = operation, DefaultAmount = amount };
}
