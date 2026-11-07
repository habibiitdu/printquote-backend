using Microsoft.AspNetCore.Mvc;
using PrintQuote.Application.Dtos;
using PrintQuote.Application.Services;

namespace PrintQuote.Api.Controllers;

// Thin controllers: routing and status codes only. Rules live in the services.

[ApiController]
[Route("api/companies")]
public sealed class CompaniesController(CompanyService service) : ControllerBase
{
    [HttpGet] public Task<List<CompanyDto>> List(CancellationToken ct) => service.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<CompanyDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<CompanyDto>> Create(CompanyInput input, CancellationToken ct) => ApiResults.Created(await service.CreateAsync(input, ct));
    [HttpPut("{id:guid}")] public Task<CompanyDto> Update(Guid id, CompanyInput input, CancellationToken ct) => service.UpdateAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(CustomerService service) : ControllerBase
{
    [HttpGet] public Task<List<CustomerDto>> List(CancellationToken ct) => service.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<CustomerDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<CustomerDto>> Create(CustomerInput input, CancellationToken ct) => ApiResults.Created(await service.CreateAsync(input, ct));
    [HttpPut("{id:guid}")] public Task<CustomerDto> Update(Guid id, CustomerInput input, CancellationToken ct) => service.UpdateAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/papers")]
public sealed class PapersController(PaperService service) : ControllerBase
{
    [HttpGet] public Task<List<PaperDto>> List(CancellationToken ct) => service.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<PaperDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<PaperDto>> Create(PaperInput input, CancellationToken ct) => ApiResults.Created(await service.CreateAsync(input, ct));
    [HttpPut("{id:guid}")] public Task<PaperDto> Update(Guid id, PaperInput input, CancellationToken ct) => service.UpdateAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/presses")]
public sealed class PressesController(PressService service) : ControllerBase
{
    [HttpGet] public Task<List<PressDto>> List(CancellationToken ct) => service.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<PressDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<PressDto>> Create(PressInput input, CancellationToken ct) => ApiResults.Created(await service.CreateAsync(input, ct));
    [HttpPut("{id:guid}")] public Task<PressDto> Update(Guid id, PressInput input, CancellationToken ct) => service.UpdateAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/finishing-operations")]
public sealed class FinishingOperationsController(FinishingOperationService service) : ControllerBase
{
    [HttpGet] public Task<List<FinishingOperationDto>> List(CancellationToken ct) => service.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<FinishingOperationDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<FinishingOperationDto>> Create(FinishingOperationInput input, CancellationToken ct) => ApiResults.Created(await service.CreateAsync(input, ct));
    [HttpPut("{id:guid}")] public Task<FinishingOperationDto> Update(Guid id, FinishingOperationInput input, CancellationToken ct) => service.UpdateAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/binding-types")]
public sealed class BindingTypesController(BindingTypeService service) : ControllerBase
{
    [HttpGet] public Task<List<BindingTypeDto>> List(CancellationToken ct) => service.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<BindingTypeDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<BindingTypeDto>> Create(BindingTypeInput input, CancellationToken ct) => ApiResults.Created(await service.CreateAsync(input, ct));
    [HttpPut("{id:guid}")] public Task<BindingTypeDto> Update(Guid id, BindingTypeInput input, CancellationToken ct) => service.UpdateAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/profit-rules")]
public sealed class ProfitRulesController(ProfitRuleService service) : ControllerBase
{
    [HttpGet] public Task<List<ProfitRuleDto>> List(CancellationToken ct) => service.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<ProfitRuleDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<ProfitRuleDto>> Create(ProfitRuleInput input, CancellationToken ct) => ApiResults.Created(await service.CreateAsync(input, ct));
    [HttpPut("{id:guid}")] public Task<ProfitRuleDto> Update(Guid id, ProfitRuleInput input, CancellationToken ct) => service.UpdateAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/templates")]
public sealed class TemplatesController(TemplateService service) : ControllerBase
{
    [HttpGet] public Task<List<TemplateDto>> List(CancellationToken ct) => service.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<TemplateDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<TemplateDto>> Create(TemplateInput input, CancellationToken ct) => ApiResults.Created(await service.CreateAsync(input, ct));
    [HttpPut("{id:guid}")] public Task<TemplateDto> Update(Guid id, TemplateInput input, CancellationToken ct) => service.UpdateAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(SettingsService service) : ControllerBase
{
    [HttpGet] public Task<SettingsDto> Get(CancellationToken ct) => service.GetAsync(ct);
    [HttpPut] public Task<SettingsDto> Update(SettingsDto input, CancellationToken ct) => service.UpdateAsync(input, ct);
}

internal static class ApiResults
{
    /// <summary>201 Created with the new resource as the body.</summary>
    public static ObjectResult Created(object value) => new(value) { StatusCode = StatusCodes.Status201Created };
}
