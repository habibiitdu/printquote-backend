using Microsoft.EntityFrameworkCore;
using PrintQuote.Application.Common;
using PrintQuote.Application.Costing;
using PrintQuote.Application.Dtos;
using PrintQuote.Domain;

namespace PrintQuote.Application.Services;

public sealed class QuotationService(IAppDbContext db, CostingService costing)
{
    public async Task<PagedResult<QuotationSummaryDto>> ListAsync(
        QuotationStatus? status, string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Quotations.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(q => q.Status == s);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(q =>
                q.JobName.ToLower().Contains(term) ||
                q.Number.ToLower().Contains(term) ||
                q.Customer!.Name.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(q => q.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new QuotationSummaryDto(
                q.Id, q.Number, q.JobName, q.CustomerId, q.Customer!.Name, q.CompanyId, q.Company!.Name,
                q.TemplateId, q.Template != null ? q.Template.Name : null, q.Quantity, q.TotalBill, q.RatePerPiece,
                q.Status, q.CurrentVersion, q.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<QuotationSummaryDto>(items, total, page, pageSize);
    }

    public async Task<QuotationDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var quotation = await db.Quotations.AsNoTracking()
            .Include(q => q.Customer)
            .Include(q => q.Company)
            .Include(q => q.Template)
            .Include(q => q.Versions)
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Quotation", id);
        return ToDetail(quotation);
    }

    public async Task<QuotationDetailDto> CreateAsync(CreateQuotationRequest request, CancellationToken ct)
    {
        var errors = new ErrorBag();
        if (string.IsNullOrWhiteSpace(request.JobName))
        {
            errors.Add("jobName", "Enter a job name.");
        }
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
        {
            errors.Add("customerId", "Choose a customer.");
        }
        if (!await db.Companies.AnyAsync(c => c.Id == request.CompanyId, ct))
        {
            errors.Add("companyId", "Choose a letterhead.");
        }
        if (request.TemplateId is { } templateId && !await db.Templates.AnyAsync(t => t.Id == templateId, ct))
        {
            errors.Add("templateId", "Unknown template.");
        }
        if (request.Spec is null)
        {
            errors.Add("spec", "The job specification is required.");
        }
        errors.ThrowIfAny();

        var quotation = new Quotation
        {
            Number = await db.NextQuotationNumberAsync(ct),
            JobName = request.JobName.Trim(),
            CustomerId = request.CustomerId,
            CompanyId = request.CompanyId,
            TemplateId = request.TemplateId
        };
        await AddVersionAsync(quotation, request.Spec!, request.Note, ct);
        db.Quotations.Add(quotation);
        await db.SaveChangesAsync(ct);
        return await GetAsync(quotation.Id, ct);
    }

    public async Task<QuotationDetailDto> AddVersionAsync(Guid id, NewVersionRequest request, CancellationToken ct)
    {
        if (request.Spec is null)
        {
            throw new ValidationException("spec", "The job specification is required.");
        }
        var quotation = await db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Quotation", id);
        if (!string.IsNullOrWhiteSpace(request.JobName))
        {
            quotation.JobName = request.JobName.Trim();
        }
        var version = await AddVersionAsync(quotation, request.Spec, request.Note, ct);
        db.QuotationVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<QuotationDetailDto> SetStatusAsync(Guid id, QuotationStatus status, CancellationToken ct)
    {
        var quotation = await db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Quotation", id);
        quotation.Status = status;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Prices the current version at other quantities using the rates it was saved with.</summary>
    public async Task<List<QuantityBreak>> BreaksAsync(Guid id, IEnumerable<int> quantities, CancellationToken ct)
    {
        var quotation = await db.Quotations.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Quotation", id);
        var version = await db.QuotationVersions.AsNoTracking()
            .FirstAsync(v => v.QuotationId == id && v.VersionNumber == quotation.CurrentVersion, ct);
        var spec = AppJson.Deserialize<JobSpec>(version.SpecJson);
        var rates = AppJson.Deserialize<RateBook>(version.RateSnapshotJson);
        return CostingEngine.Breaks(spec, rates, quantities);
    }

    /// <summary>Prices the spec, attaches a new version to the quotation and updates its summary columns.</summary>
    private async Task<QuotationVersion> AddVersionAsync(Quotation quotation, JobSpec spec, string? note, CancellationToken ct)
    {
        // The template and customer on the quotation drive the profit rules, whatever the client sent.
        spec = spec with { CustomerId = quotation.CustomerId, TemplateId = quotation.TemplateId };
        var rates = await costing.LoadRateBookAsync(ct);
        var sheet = CostingEngine.Calculate(spec, rates);

        quotation.CurrentVersion += 1;
        quotation.Quantity = sheet.Quantity;
        quotation.TotalBill = sheet.TotalBill;
        quotation.RatePerPiece = sheet.RatePerPiece;

        var version = new QuotationVersion
        {
            QuotationId = quotation.Id,
            VersionNumber = quotation.CurrentVersion,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            SpecJson = AppJson.Serialize(spec),
            CostSheetJson = AppJson.Serialize(sheet),
            RateSnapshotJson = AppJson.Serialize(rates)
        };
        quotation.Versions.Add(version);
        return version;
    }

    private static QuotationDetailDto ToDetail(Quotation q) => new(
        q.Id, q.Number, q.JobName, q.CustomerId, q.Customer?.Name ?? "", q.CompanyId, q.Company?.Name ?? "",
        q.TemplateId, q.Template?.Name, q.Quantity, q.TotalBill, q.RatePerPiece, q.Status, q.CurrentVersion, q.UpdatedAt,
        q.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new QuotationVersionDto(
                v.VersionNumber, v.Note, v.CreatedAt,
                AppJson.Deserialize<JobSpec>(v.SpecJson),
                AppJson.Deserialize<CostSheet>(v.CostSheetJson)))
            .ToList());
}
