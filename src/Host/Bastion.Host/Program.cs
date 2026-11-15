using System.Globalization;
using Bastion.Host;
using Bastion.Modules.FlatOffers;
using Bastion.Modules.JobOffers;
using Bastion.Platform.Abstractions;
using Serilog;

IModule[] allModules = [new FlatOffersModule(), new JobOffersModule()];

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.UseTomlConfiguration(builder.Environment);

var logsPath = Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Logs:Path"] ?? "logs");
const string logLine = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3} {Module} {Message:lj}{NewLine}{Exception}";
builder.Services.AddSerilog(logger => logger
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: logLine, formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File(
        Path.Combine(logsPath, "bastion-.log"),
        outputTemplate: logLine,
        formatProvider: CultureInfo.InvariantCulture,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var modules = ModuleLoader.GetEnabled(allModules, builder.Configuration);
foreach (var module in modules)
{
    module.AddServices(builder.Services, builder.Configuration.GetSection($"Modules:{module.Name}"));
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/platform/modules", () => TypedResults.Ok(modules.Select(m => new ModuleInfo(m.Name, m.RoutePrefix))))
    .WithTags("Platform");
app.MapModuleEndpoints(modules);

app.Run();
