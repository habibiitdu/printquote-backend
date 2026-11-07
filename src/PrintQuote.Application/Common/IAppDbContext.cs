using Microsoft.EntityFrameworkCore;
using PrintQuote.Domain;

namespace PrintQuote.Application.Common;

public interface IAppDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Paper> Papers { get; }
    DbSet<Press> Presses { get; }
    DbSet<PressRateSlab> PressRateSlabs { get; }
    DbSet<FinishingOperation> FinishingOperations { get; }
    DbSet<BindingType> BindingTypes { get; }
    DbSet<BindingRateSlab> BindingRateSlabs { get; }
    DbSet<ProfitRule> ProfitRules { get; }
    DbSet<AppSettings> Settings { get; }
    DbSet<ProductTemplate> Templates { get; }
    DbSet<TemplateComponent> TemplateComponents { get; }
    DbSet<TemplateField> TemplateFields { get; }
    DbSet<TemplateExtra> TemplateExtras { get; }
    DbSet<Quotation> Quotations { get; }
    DbSet<QuotationVersion> QuotationVersions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the next quotation number, e.g. "Q-0001".</summary>
    Task<string> NextQuotationNumberAsync(CancellationToken cancellationToken = default);
}
