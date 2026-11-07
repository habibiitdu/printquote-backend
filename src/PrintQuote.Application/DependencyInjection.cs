using Microsoft.Extensions.DependencyInjection;
using PrintQuote.Application.Services;

namespace PrintQuote.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CostingService>();
        services.AddScoped<QuotationService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<PaperService>();
        services.AddScoped<PressService>();
        services.AddScoped<FinishingOperationService>();
        services.AddScoped<BindingTypeService>();
        services.AddScoped<ProfitRuleService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<TemplateService>();
        return services;
    }
}
