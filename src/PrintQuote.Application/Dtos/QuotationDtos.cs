using PrintQuote.Application.Costing;
using PrintQuote.Domain;

namespace PrintQuote.Application.Dtos;

public sealed record CalculateRequest(JobSpec Spec, List<int>? BreakQuantities);
public sealed record CalculateResponse(CostSheet CostSheet, List<QuantityBreak> Breaks);

public sealed record QuotationSummaryDto(
    Guid Id, string Number, string JobName, Guid CustomerId, string CustomerName, Guid CompanyId, string CompanyName,
    Guid? TemplateId, string? TemplateName, int Quantity, decimal TotalBill, decimal RatePerPiece,
    QuotationStatus Status, int CurrentVersion, DateTimeOffset UpdatedAt);

public sealed record QuotationVersionDto(int VersionNumber, string? Note, DateTimeOffset CreatedAt, JobSpec Spec, CostSheet CostSheet);

public sealed record QuotationDetailDto(
    Guid Id, string Number, string JobName, Guid CustomerId, string CustomerName, Guid CompanyId, string CompanyName,
    Guid? TemplateId, string? TemplateName, int Quantity, decimal TotalBill, decimal RatePerPiece,
    QuotationStatus Status, int CurrentVersion, DateTimeOffset UpdatedAt, List<QuotationVersionDto> Versions);

public sealed record CreateQuotationRequest(string JobName, Guid CustomerId, Guid CompanyId, Guid? TemplateId, JobSpec Spec, string? Note);
public sealed record NewVersionRequest(string? JobName, JobSpec Spec, string? Note);
public sealed record StatusRequest(QuotationStatus Status);

public sealed record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
