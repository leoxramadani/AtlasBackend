using Atlas.Application.Abstractions;
using Atlas.Application.Services;
using Atlas.Domain.Repositories;
using Atlas.Infrastructure.Persistence;
using Atlas.Infrastructure.Persistence.Repositories;
using Atlas.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAtlasInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var sqlConnectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(sqlConnectionString))
        {
            throw new InvalidOperationException(
                "CRITICAL: MS SQL Server connection string 'DefaultConnection' is missing in configuration. " +
                "Atlas requires Microsoft SQL Server (e.g. 'Server=localhost,1433;Database=AtlasDb;User Id=sa;Password=Local_Dev_Password_1;TrustServerCertificate=True;').");
        }

        services.AddDbContext<AtlasDbContext>(options =>
        {
            options.UseSqlServer(sqlConnectionString, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        });

        // Repositories
        services.AddScoped<IApplicationRepository, ApplicationRepository>();

        // Providers & Stubs
        services.AddSingleton<IIdentityVerificationProvider, MockIdNowProvider>();
        services.AddSingleton<ISanctionsScreeningProvider, MockWorldCheckProvider>();
        services.AddSingleton<ICoreBankingService, MockCoreBankingService>();
        services.AddSingleton<ICardIssuanceService, MockCardIssuanceService>();

        // Application Services
        services.AddScoped<IOnboardingService, OnboardingService>();

        return services;
    }
}
