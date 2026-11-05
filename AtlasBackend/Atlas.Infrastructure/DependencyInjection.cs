using Atlas.Application.Abstractions;
using Atlas.Application.Services;
using Atlas.Domain.Repositories;
using Atlas.Infrastructure.Caching;
using Atlas.Infrastructure.Messaging;
using Atlas.Infrastructure.Messaging.Consumers;
using Atlas.Infrastructure.Persistence;
using Atlas.Infrastructure.Persistence.Repositories.Application;
using Atlas.Infrastructure.Persistence.Repositories.Audit;
using Atlas.Infrastructure.Providers;
using MassTransit;
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
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "";
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

        // MassTransit & RabbitMQ
        services.AddMassTransit(x =>
        {
            x.AddConsumer<ApplicationVerificationConsumer>();
            x.AddConsumer<AccountProvisioningConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitHost = configuration["RabbitMQ:Host"] ?? "localhost";
                var rabbitPort = ushort.TryParse(configuration["RabbitMQ:Port"], out var port) ? port : (ushort)5672;
                var rabbitVHost = configuration["RabbitMQ:VirtualHost"] ?? "";
                var rabbitUsername = configuration["RabbitMQ:Username"] ?? "";
                var rabbitPassword = configuration["RabbitMQ:Password"] ?? "";

                cfg.Host(rabbitHost, rabbitPort, rabbitVHost, h =>
                {
                    h.Username(rabbitUsername);
                    h.Password(rabbitPassword);
                });

                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IEventBus, MassTransitEventBus>();

        // Application Services
        services.AddScoped<IOnboardingService, OnboardingService>();

        return services;
    }
}
