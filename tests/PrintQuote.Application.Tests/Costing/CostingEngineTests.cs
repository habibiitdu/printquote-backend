using PrintQuote.Application.Common;
using PrintQuote.Application.Costing;
using PrintQuote.Domain;
using static PrintQuote.Application.Tests.Costing.SampleRates;

namespace PrintQuote.Application.Tests.Costing;

public class CostingEngineTests
{
    private static CostLine Line(CostSheet sheet, string description) =>
        sheet.Lines.Single(l => l.Description == description);

    [Fact]
    public void English2_25000_matches_the_sample_sheet()
    {
        // Worked example in docs/technical-design.md section 4.5.
        var sheet = CostingEngine.Calculate(LectureBook(25000, 5), Book());

        Assert.Equal(2, Line(sheet, "Demy plate").Quantity);
        Assert.Equal(5, Line(sheet, "Double Demy plate").Quantity);
        Assert.Equal(12.55m, Line(sheet, "70 gsm Offset Paper (Cover)").Quantity);
        Assert.Equal(125.2m, Line(sheet, "55 gsm Offset Paper (Partex) (Inner)").Quantity);

        var coverPrint = Line(sheet, "Print Demy (Cover)");
        Assert.Equal(50.2m, coverPrint.Quantity);
        Assert.Equal(150, coverPrint.Rate);
        var innerPrint = Line(sheet, "Print Double Demy (Inner)");
        Assert.Equal(125.2m, innerPrint.Quantity);
        Assert.Equal(180, innerPrint.Rate);
        Assert.Equal(0.9m, Line(sheet, "Pin binding").Rate);

        Assert.Equal(319_677.50m, sheet.RawMaterialsTotal);
        Assert.Equal(52_566.00m, sheet.ConversionTotal);
        Assert.Equal(372_243.50m, sheet.ProductionCost);
        Assert.Equal(10, sheet.ProfitPercent);
        Assert.Equal("rule:Standard", sheet.ProfitSource);
        Assert.Equal(37_224.35m, sheet.Profit);
        Assert.Equal(409_467.85m, sheet.TotalBill);
        Assert.Equal(16.38m, sheet.RatePerPiece);
    }

    [Theory]
    // quantity, inner forma, cover rims, inner rims. Inner rims are the sample sheets' values; the
    // 20,000 cover sample has 10.2 rims because its wastage was added by hand (engine: 10.05).
    [InlineData(25000, 4.5, 12.55, 112.7)]
    [InlineData(25000, 5, 12.55, 125.2)]
    [InlineData(20000, 4.5, 10.05, 90.2)]
    [InlineData(20000, 5, 10.05, 100.2)]
    [InlineData(2500, 2, 1.3, 5.1)]
    [InlineData(2500, 4, 1.3, 10.2)]
    [InlineData(2500, 5, 1.3, 12.7)]
    [InlineData(1000, 4, 0.55, 4.2)]
    public void Lecture_book_paper_matches_the_sample_sheets(int quantity, double innerForma, double coverRims, double innerRims)
    {
        var sheet = CostingEngine.Calculate(LectureBook(quantity, (decimal)innerForma), Book());

        Assert.Equal((decimal)coverRims, Line(sheet, "70 gsm Offset Paper (Cover)").Quantity);
        Assert.Equal((decimal)innerRims, Line(sheet, "55 gsm Offset Paper (Partex) (Inner)").Quantity);
    }

    [Fact]
    public void Short_runs_pay_flat_per_plate_and_20_percent_profit()
    {
        var sheet = CostingEngine.Calculate(LectureBook(2500, 5), Book());

        var coverPrint = Line(sheet, "Print Demy (Cover)");
        Assert.Equal("plate", coverPrint.Unit);
        Assert.Equal(2, coverPrint.Quantity);
        Assert.Equal(350, coverPrint.Rate);
        var innerPrint = Line(sheet, "Print Double Demy (Inner)");
        Assert.Equal("plate", innerPrint.Unit);
        Assert.Equal(5, innerPrint.Quantity);
        Assert.Equal(600, innerPrint.Rate);

        Assert.Equal(20, sheet.ProfitPercent);
        Assert.Equal(48_528.00m, sheet.TotalBill);
        Assert.Equal(19.41m, sheet.RatePerPiece);
    }

    [Fact]
    public void Inner_plates_are_one_per_forma_rounded_up()
    {
        var sheet = CostingEngine.Calculate(LectureBook(25000, 5.5m), Book());

        Assert.Equal(6, Line(sheet, "Double Demy plate").Quantity);
    }

    [Fact]
    public void Half_forma_can_be_moved_to_the_demy_press()
    {
        var sheet = CostingEngine.Calculate(LectureBook(2500, 2.5m, halfFormaPress: Demy), Book());

        // 2 cover plates + 1 plate for the half forma; 2 whole formas on Double Demy.
        Assert.Equal(3, Line(sheet, "Demy plate").Quantity);
        Assert.Equal(2, Line(sheet, "Double Demy plate").Quantity);
        Assert.Equal(2, sheet.Lines.Count(l => l.Description == "Print Demy (Cover)" || l.Description == "Print Demy (Inner)"));
    }

    [Fact]
    public void Pin_binding_rate_steps_with_inner_forma()
    {
        Assert.Equal(0.8m, Line(CostingEngine.Calculate(LectureBook(25000, 4.5m), Book()), "Pin binding").Rate);
        Assert.Equal(1.0m, Line(CostingEngine.Calculate(LectureBook(25000, 6), Book()), "Pin binding").Rate);
        Assert.Equal(1.3m, Line(CostingEngine.Calculate(LectureBook(25000, 8), Book()), "Pin binding").Rate);
        Assert.Equal(1.5m, Line(CostingEngine.Calculate(LectureBook(25000, 10), Book()), "Pin binding").Rate);
    }

    [Fact]
    public void Omr_sheet_fits_8_up_on_demy_and_prints_both_sides()
    {
        var spec = new JobSpec
        {
            Quantity = 500_000,
            ProfitPercentOverride = 12,
            Components =
            [
                new()
                {
                    Name = "Sheet", Measure = MeasureMode.Ups, OpenWidthIn = 8.3m, OpenHeightIn = 5.6m,
                    ColorsFront = 1, ColorsBack = 1, PaperId = Offset100, PressId = Demy
                }
            ]
        };

        var sheet = CostingEngine.Calculate(spec, Book(demyMakeReady: 0));

        // 500,000 ÷ 8 = 62,500 Demy sheets = 31,250 parent sheets = 62.5 rims; 125,000 impressions.
        Assert.Equal(2, Line(sheet, "Demy plate").Quantity);
        Assert.Equal(62.5m, Line(sheet, "100 gsm Offset Paper (Partex) (Sheet)").Quantity);
        Assert.Equal(125m, Line(sheet, "Print Demy (Sheet)").Quantity);
        Assert.Equal(12, sheet.ProfitPercent);
        Assert.Equal("override", sheet.ProfitSource);
    }

    [Fact]
    public void Map_book_prices_lamination_crease_glue_and_extras()
    {
        var spec = new JobSpec
        {
            Quantity = 10_000,
            BindingTypeId = Glue,
            ProfitPercentOverride = 10,
            Extras = [new() { FinishingOperationId = Packing }, new() { FinishingOperationId = Delivery }],
            Components =
            [
                new()
                {
                    Name = "Cover", Measure = MeasureMode.Ups, OpenWidthIn = 18, OpenHeightIn = 12, ColorsFront = 4,
                    PaperId = Card300, PressId = Demy,
                    Finishing = [new() { FinishingOperationId = Lamination, Sides = 1 }, new() { FinishingOperationId = Crease }]
                },
                new()
                {
                    Name = "Map", Measure = MeasureMode.Ups, OpenWidthIn = 22, OpenHeightIn = 35, ColorsFront = 4,
                    PaperId = Art120, PressId = DoubleDemy,
                    Finishing = [new() { FinishingOperationId = Lamination, Sides = 1 }]
                }
            ]
        };

        var sheet = CostingEngine.Calculate(spec, Book(demyMakeReady: 0, doubleDemyMakeReady: 0));

        Assert.Equal(5000, Line(sheet, "300 gsm Art Card (Cover)").Quantity);
        Assert.Equal(20, Line(sheet, "120 gsm Art Paper (Map)").Quantity);
        Assert.Equal(6000.00m, Line(sheet, "Print Demy (Cover)").Total);
        Assert.Equal(8000.00m, Line(sheet, "Print Double Demy (Map)").Total);
        Assert.Equal(2_160_000m, Line(sheet, "Glue lamination (Cover)").Quantity);
        Assert.Equal(8640.00m, Line(sheet, "Glue lamination (Cover)").Total);
        Assert.Equal(30_800.00m, Line(sheet, "Glue lamination (Map)").Total);
        Assert.Equal(1500.00m, Line(sheet, "Cover crease (Cover)").Total);
        Assert.Equal(8000.00m, Line(sheet, "Glue binding").Total);
        // The sample sheet buys 10 extra card sheets (150 tk) by hand; everything else matches.
        Assert.Equal(240_290m - 150m, sheet.ProductionCost);
    }

    [Fact]
    public void Profit_comes_from_customer_before_template_before_rules()
    {
        var customer = Guid.NewGuid();
        var template = Guid.NewGuid();
        var book = Book() with
        {
            CustomerMargins = new() { [customer] = 15 },
            TemplateMargins = new() { [template] = 12 }
        };
        var spec = LectureBook(25000, 5);

        Assert.Equal("customer", CostingEngine.Calculate(spec with { CustomerId = customer, TemplateId = template }, book).ProfitSource);
        Assert.Equal("template", CostingEngine.Calculate(spec with { TemplateId = template }, book).ProfitSource);
        Assert.Equal(12, CostingEngine.Calculate(spec with { TemplateId = template }, book).ProfitPercent);
    }

    [Fact]
    public void Overheads_are_percentages_of_production_cost()
    {
        var sheet = CostingEngine.Calculate(LectureBook(25000, 5) with { CommissionPercent = 2, OverheadPercent = 1 }, Book());

        Assert.Equal(7444.87m, sheet.Commission);
        Assert.Equal(3722.44m, sheet.Overhead);
        Assert.Equal(sheet.ProductionCost + 7444.87m + 3722.44m, sheet.TotalCost);
    }

    [Fact]
    public void Quantity_breaks_reprice_the_same_job()
    {
        var breaks = CostingEngine.Breaks(LectureBook(25000, 5), Book(), [25000, 1000, 2500]);

        Assert.Equal([1000, 2500, 25000], breaks.Select(b => b.Quantity));
        Assert.Equal(23.97m, breaks[0].RatePerPiece);
        Assert.Equal(19.41m, breaks[1].RatePerPiece);
        Assert.Equal(16.38m, breaks[2].RatePerPiece);
    }

    [Theory]
    [InlineData(8.3, 5.6, 8)]
    [InlineData(18, 12, 1)]
    [InlineData(17, 11, 2)]
    [InlineData(19, 24, 0)]
    public void Ups_tries_both_directions(double width, double height, int expected)
    {
        var demy = Book().Presses.Single(p => p.Id == Demy);

        Assert.Equal(expected, CostingEngine.Ups((decimal)width, (decimal)height, demy));
    }

    [Fact]
    public void Invalid_specs_report_every_field()
    {
        var spec = new JobSpec
        {
            Quantity = 0,
            Components =
            [
                new() { Name = "Poster", Measure = MeasureMode.Ups, OpenWidthIn = 30, OpenHeightIn = 40, ColorsFront = 4, PaperId = Guid.NewGuid(), PressId = DoubleDemy }
            ]
        };

        var error = Assert.Throws<ValidationException>(() => CostingEngine.Calculate(spec, Book()));

        Assert.Contains("quantity", error.Errors.Keys);
        Assert.Contains("components[0].paperId", error.Errors.Keys);
        Assert.Contains("components[0].pressId", error.Errors.Keys);
    }

    [Fact]
    public void Lamination_on_a_forma_component_needs_the_page_size()
    {
        var spec = LectureBook(1000, 2) with { PageWidthIn = null, PageHeightIn = null };
        spec.Components[0].Finishing.Add(new() { FinishingOperationId = Lamination, Sides = 1 });

        var sheet = CostingEngine.Calculate(spec, Book());

        Assert.DoesNotContain(sheet.Lines, l => l.Description.StartsWith("Glue lamination"));
        Assert.Single(sheet.Warnings);
    }
}
