using Microsoft.AspNetCore.Mvc;
using PrintQuote.Application.Common;
using PrintQuote.Application.Costing;
using PrintQuote.Application.Dtos;
using PrintQuote.Application.Services;
using PrintQuote.Domain;

namespace PrintQuote.Api.Controllers;

[ApiController]
[Route("api/costing")]
public sealed class CostingController(CostingService service) : ControllerBase
{
    /// <summary>Prices a job without saving it (live preview on the quotation form).</summary>
    [HttpPost("calculate")]
    public Task<CalculateResponse> Calculate(CalculateRequest request, CancellationToken ct) => service.CalculateAsync(request, ct);
}

[ApiController]
[Route("api/quotations")]
public sealed class QuotationsController(QuotationService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<QuotationSummaryDto>> List(
        [FromQuery] QuotationStatus? status, [FromQuery] string? search, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        service.ListAsync(status, search, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<QuotationDetailDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<QuotationDetailDto>> Create(CreateQuotationRequest request, CancellationToken ct) =>
        ApiResults.Created(await service.CreateAsync(request, ct));

    [HttpPost("{id:guid}/versions")]
    public Task<QuotationDetailDto> AddVersion(Guid id, NewVersionRequest request, CancellationToken ct) =>
        service.AddVersionAsync(id, request, ct);

    [HttpPut("{id:guid}/status")]
    public Task<QuotationDetailDto> SetStatus(Guid id, StatusRequest request, CancellationToken ct) =>
        service.SetStatusAsync(id, request.Status, ct);

    /// <summary>Prices the current version at other quantities, e.g. ?quantities=1000,2500,10000.</summary>
    [HttpGet("{id:guid}/breaks")]
    public Task<List<QuantityBreak>> Breaks(Guid id, [FromQuery] string? quantities, CancellationToken ct)
    {
        var parsed = new List<int>();
        foreach (var part in (quantities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var q) || q <= 0)
            {
                throw new ValidationException("quantities", $"'{part}' is not a whole number above 0.");
            }
            parsed.Add(q);
        }
        if (parsed.Count == 0)
        {
            throw new ValidationException("quantities", "Give at least one quantity, e.g. ?quantities=1000,2500.");
        }
        return service.BreaksAsync(id, parsed, ct);
    }
}
