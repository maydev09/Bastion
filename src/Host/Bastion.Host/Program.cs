using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using Bastion.Host;
using Bastion.Modules.Housing;
using Bastion.Modules.JobOffers;
using Bastion.Platform.Abstractions;
using Dapper;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Serilog;

IModule[] allModules = [new HousingModule(), new JobOffersModule()];

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.UseConfigurationSources(builder.Environment, args);

var connectionString = builder.Configuration.GetConnectionString(DatabaseStatusEndpoint.ConnectionStringName);

// The build-time OpenAPI generator starts the app without secrets; nothing connects to this placeholder.
var generatingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (generatingOpenApi)
{
    connectionString = "Host=localhost;Database=openapi";
}

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException(
        $"ConnectionStrings:{DatabaseStatusEndpoint.ConnectionStringName} is not set. Run scripts/dev-up.sh, which stores it in the .NET user secrets.");
}

builder.Services.AddNpgsqlDataSource(connectionString);

DefaultTypeMap.MatchNamesWithUnderscores = true;
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<LogsOptions>()
    .BindConfiguration(LogsOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
const string logLine = $"{{Timestamp:yyyy-MM-dd HH:mm:ss.fff}} {{Level:u3}} {{{ModuleLogScope.Property}}} {{Message:lj}}{{NewLine}}{{Exception}}";
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: logLine, formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File(
        Path.Combine(
            builder.Environment.ContentRootPath,
            services.GetRequiredService<IOptions<LogsOptions>>().Value.Path,
            "bastion-.log"),
        outputTemplate: logLine,
        formatProvider: CultureInfo.InvariantCulture,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();

var modules = ModuleLoader.GetEnabled(allModules, builder.Configuration);
foreach (var module in modules)
{
    module.AddServices(builder.Services, ModuleOptions.SectionOf(builder.Configuration, module.Name));
}

// The OpenAPI generator starts the app; background work (e.g. module jobs) must not run then.
if (generatingOpenApi)
{
    builder.Services.RemoveAll<IHostedService>();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Bastion"));
}

app.MapGet("/api/platform/modules", () => TypedResults.Ok(modules.Select(m => new ModuleInfo(m.Name, m.RoutePrefix))))
    .WithTags("Platform");
app.MapDatabaseStatus();
app.MapModuleEndpoints(modules);
app.MapFallback("/api/{**path}", () => TypedResults.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
