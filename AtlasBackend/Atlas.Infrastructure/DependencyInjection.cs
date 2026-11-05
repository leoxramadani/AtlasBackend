using Atlas.Application.Abstractions;
using Atlas.Application.Services;
using Atlas.Domain.Repositories;
using Atlas.Infrastructure.Caching;
using Atlas.Infrastructure.Persistence;
using Atlas.Infrastructure.Persistence.Repositories.Application;
using Atlas.Infrastructure.Persistence.Repositories.Audit;
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
                "CRITICAL: MS SQL Server connection string 'DefaultConnection' is missing in configuration. ");
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
        services.AddScoped<IAuditRepository, AuditRepository>();

        // Redis Distributed Cache
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "Atlas_";
        });
        services.AddSingleton<ICacheService, RedisCacheService>();

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
