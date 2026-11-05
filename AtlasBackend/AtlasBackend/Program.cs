using Atlas.Api.Middleware;
using Atlas.Infrastructure;
using Atlas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog: Console + Seq sink
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Atlas Digital Onboarding API",
        Version = "v1",
        Description = "Backend onboarding service for Atlas with Microsoft SQL Server persistence."
    });
});

builder.Services.AddAtlasInfrastructure(builder.Configuration);

var app = builder.Build();

// Automatically ensure MS SQL database schema is created
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AtlasDbContext>();

    try
    {
        logger.LogInformation("Connecting to MS SQL Server and ensuring database schema exists...");
        db.Database.EnsureCreated();
        logger.LogInformation("MS SQL Server database initialized successfully.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex,
            "CRITICAL: Failed to connect to Microsoft SQL Server at {ConnectionString}. " +
            "Please ensure the SQL Server Docker container is running: 'docker compose -f platform/docker-compose.yml up -d'",
            db.Database.GetDbConnection().ConnectionString);
        throw new InvalidOperationException(
            "Could not connect to MS SQL Server. Start the database container with 'docker compose -f platform/docker-compose.yml up -d'.", ex);
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas Onboarding API v1");
    c.RoutePrefix = string.Empty;
});

app.UseAuthorization();
app.MapControllers();

try
{
    Log.Information("Starting Atlas Onboarding API with MS SQL Server...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Atlas host terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
