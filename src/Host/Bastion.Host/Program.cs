using System.Globalization;
using System.Text.Json.Serialization;
using Bastion.Host;
using Bastion.Modules.FlatOffers;
using Bastion.Modules.JobOffers;
using Bastion.Platform.Abstractions;
using Serilog;

IModule[] allModules = [new FlatOffersModule(), new JobOffersModule()];

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.UseTomlConfiguration(builder.Environment);

var logs = builder.Configuration.GetSection(LogsOptions.Section).Get<LogsOptions>() ?? new LogsOptions();
var logsPath = Path.Combine(builder.Environment.ContentRootPath, logs.Path);
const string logLine = $"{{Timestamp:yyyy-MM-dd HH:mm:ss.fff}} {{Level:u3}} {{{ModuleLogScope.Property}}} {{Message:lj}}{{NewLine}}{{Exception}}";
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

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var modules = ModuleLoader.GetEnabled(allModules, builder.Configuration);
foreach (var module in modules)
{
    module.AddServices(builder.Services, ModuleOptions.SectionOf(builder.Configuration, module.Name));
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Bastion"));
}

app.MapGet("/api/platform/modules", () => TypedResults.Ok(modules.Select(m => new ModuleInfo(m.Name, m.RoutePrefix))))
    .WithTags("Platform");
app.MapModuleEndpoints(modules);
app.MapFallback("/api/{**path}", () => TypedResults.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
