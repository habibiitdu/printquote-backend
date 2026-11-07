using Microsoft.EntityFrameworkCore;
using PrintQuote.Application.Common;
using PrintQuote.Domain;

namespace PrintQuote.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    private const string QuotationNumberSequence = "quotation_number_seq";

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Paper> Papers => Set<Paper>();
    public DbSet<Press> Presses => Set<Press>();
    public DbSet<PressRateSlab> PressRateSlabs => Set<PressRateSlab>();
    public DbSet<FinishingOperation> FinishingOperations => Set<FinishingOperation>();
    public DbSet<BindingType> BindingTypes => Set<BindingType>();
    public DbSet<BindingRateSlab> BindingRateSlabs => Set<BindingRateSlab>();
    public DbSet<ProfitRule> ProfitRules => Set<ProfitRule>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    public DbSet<ProductTemplate> Templates => Set<ProductTemplate>();
    public DbSet<TemplateComponent> TemplateComponents => Set<TemplateComponent>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<TemplateExtra> TemplateExtras => Set<TemplateExtra>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationVersion> QuotationVersions => Set<QuotationVersion>();

    public async Task<string> NextQuotationNumberAsync(CancellationToken cancellationToken = default)
    {
        var next = await Database
            .SqlQueryRaw<long>($"SELECT nextval('{QuotationNumberSequence}') AS \"Value\"")
            .SingleAsync(cancellationToken);
        return $"Q-{next:D4}";
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 4);
        builder.Properties<decimal?>().HavePrecision(18, 4);
        builder.Properties<PaperUnit>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<PrintChargeType>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<FinishingBasis>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<MeasureMode>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<FieldBehaviour>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<QuotationStatus>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasSequence<long>(QuotationNumberSequence);

        model.Entity<Press>(e =>
        {
            e.HasMany(p => p.RateSlabs).WithOne().HasForeignKey(s => s.PressId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<BindingType>(e =>
        {
            e.HasMany(b => b.Slabs).WithOne().HasForeignKey(s => s.BindingTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ProductTemplate>(e =>
        {
            e.HasMany(t => t.Components).WithOne().HasForeignKey(c => c.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.Extras).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<BindingType>().WithMany().HasForeignKey(t => t.DefaultBindingTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<TemplateComponent>(e =>
        {
            e.HasMany(c => c.Fields).WithOne().HasForeignKey(f => f.ComponentId).OnDelete(DeleteBehavior.Cascade);
            e.OwnsMany(c => c.DefaultFinishing, f => f.ToJson());
        });

        model.Entity<TemplateField>(e =>
        {
            e.HasIndex(f => new { f.ComponentId, f.FieldKey }).IsUnique();
        });

        model.Entity<TemplateExtra>(e =>
        {
            e.HasOne<FinishingOperation>().WithMany().HasForeignKey(x => x.FinishingOperationId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Quotation>(e =>
        {
            e.HasIndex(q => q.Number).IsUnique();
            e.HasIndex(q => q.Status);
            e.HasIndex(q => q.UpdatedAt);
            e.HasOne(q => q.Customer).WithMany().HasForeignKey(q => q.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(q => q.Company).WithMany().HasForeignKey(q => q.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(q => q.Template).WithMany().HasForeignKey(q => q.TemplateId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(q => q.Versions).WithOne().HasForeignKey(v => v.QuotationId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<QuotationVersion>(e =>
        {
            e.HasIndex(v => new { v.QuotationId, v.VersionNumber }).IsUnique();
            e.Property(v => v.SpecJson).HasColumnType("jsonb");
            e.Property(v => v.CostSheetJson).HasColumnType("jsonb");
            e.Property(v => v.RateSnapshotJson).HasColumnType("jsonb");
        });
    }
}
