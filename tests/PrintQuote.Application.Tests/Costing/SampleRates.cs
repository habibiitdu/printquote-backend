using PrintQuote.Application.Costing;
using PrintQuote.Domain;

namespace PrintQuote.Application.Tests.Costing;

/// <summary>The rates read off the press's sample sheets (same values as the database seed).</summary>
internal static class SampleRates
{
    public static readonly Guid Offset55 = Guid.Parse("10000000-0000-0000-0000-000000000055");
    public static readonly Guid Offset70 = Guid.Parse("10000000-0000-0000-0000-000000000070");
    public static readonly Guid Offset100 = Guid.Parse("10000000-0000-0000-0000-000000000100");
    public static readonly Guid Art120 = Guid.Parse("10000000-0000-0000-0000-000000000120");
    public static readonly Guid Card300 = Guid.Parse("10000000-0000-0000-0000-000000000300");
    public static readonly Guid Demy = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid DoubleDemy = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid Lamination = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid Crease = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid Packing = Guid.Parse("30000000-0000-0000-0000-000000000004");
    public static readonly Guid Delivery = Guid.Parse("30000000-0000-0000-0000-000000000005");
    public static readonly Guid Pin = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid Glue = Guid.Parse("40000000-0000-0000-0000-000000000002");

    public static RateBook Book(int demyMakeReady = 25, int doubleDemyMakeReady = 20) => new()
    {
        RimRoundingStep = 0.05m,
        Papers =
        [
            new(Offset55, "55 gsm Offset Paper (Partex)", 55, PaperUnit.Rim, 2250),
            new(Offset70, "70 gsm Offset Paper", 70, PaperUnit.Rim, 2850),
            new(Offset100, "100 gsm Offset Paper (Partex)", 100, PaperUnit.Rim, 4000),
            new(Art120, "120 gsm Art Paper", 120, PaperUnit.Rim, 4700),
            new(Card300, "300 gsm Art Card", 300, PaperUnit.Sheet, 15)
        ],
        Presses =
        [
            new(Demy, "Demy", 18, 23, 2, 180, demyMakeReady, 0.5m,
            [
                new(2000, PrintChargeType.FlatPerPlate, 300),
                new(3000, PrintChargeType.FlatPerPlate, 350),
                new(6000, PrintChargeType.FlatPerPlate, 450),
                new(null, PrintChargeType.PerThousand, 150)
            ]),
            new(DoubleDemy, "Double Demy", 23, 36, 1, 370, doubleDemyMakeReady, 1m,
            [
                new(5000, PrintChargeType.FlatPerPlate, 600),
                new(20000, PrintChargeType.PerThousand, 200),
                new(null, PrintChargeType.PerThousand, 180)
            ])
        ],
        Finishing =
        [
            new(Lamination, "Glue lamination", FinishingBasis.PerSquareInch, 0.004m),
            new(Crease, "Cover crease", FinishingBasis.PerThousand, 150),
            new(Packing, "Packing", FinishingBasis.Fixed, 1000),
            new(Delivery, "Delivery & others", FinishingBasis.Fixed, 5000)
        ],
        Bindings =
        [
            new(Pin, "Pin",
            [
                new(2, 0.7m), new(4.5m, 0.8m), new(5, 0.9m), new(6.5m, 1.0m), new(7, 1.2m), new(9, 1.3m), new(null, 1.5m)
            ]),
            new(Glue, "Glue", [new(null, 0.8m)])
        ],
        ProfitRules =
        [
            new("Short run", 1, 5000, 20),
            new("Standard", 2, null, 10)
        ]
    };

    /// <summary>The lecture-sheet book: 0.5 forma 2-colour cover on 70 gsm, N forma 1-colour inner on 55 gsm, pin bound.</summary>
    public static JobSpec LectureBook(int quantity, decimal innerForma, Guid? halfFormaPress = null) => new()
    {
        Quantity = quantity,
        PageWidthIn = 8.5m,
        PageHeightIn = 11,
        BindingTypeId = Pin,
        Components =
        [
            new() { Name = "Cover", Measure = MeasureMode.Forma, Forma = 0.5m, Colors = 2, PaperId = Offset70, PressId = Demy },
            new()
            {
                Name = "Inner", Measure = MeasureMode.Forma, Forma = innerForma, Colors = 1, PaperId = Offset55,
                PressId = DoubleDemy, HalfFormaPressId = halfFormaPress, CountsTowardBindingForma = true
            }
        ]
    };
}
