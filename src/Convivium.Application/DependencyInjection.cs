namespace Convivium.Application;

using Convivium.Application.Auth;
using Convivium.Application.Billing;
using Convivium.Application.Expenses;
using Convivium.Application.Finance;
using Convivium.Application.Notifications;
using Convivium.Application.Utilities;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<BillingService>();
        services.AddScoped<CashBookService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<EmailOutboxService>();
        services.AddScoped<UtilityBillImportService>();

        return services;
    }
}
