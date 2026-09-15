namespace Convivium.Application;

using Convivium.Application.Auth;
using Convivium.Application.Billing;
using Convivium.Application.Condominiums;
using Convivium.Application.Dashboard;
using Convivium.Application.Expenses;
using Convivium.Application.Finance;
using Convivium.Application.Notifications;
using Convivium.Application.People;
using Convivium.Application.Platform;
using Convivium.Application.Units;
using Convivium.Application.Utilities;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<BillingService>();
        services.AddScoped<CondominiumService>();
        services.AddScoped<PlatformService>();
        services.AddScoped<PeopleService>();
        services.AddScoped<UnitService>();
        services.AddScoped<CashBookService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<EmailOutboxService>();
        services.AddScoped<UtilityBillImportService>();

        return services;
    }
}
