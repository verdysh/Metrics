using Azure.Monitor.OpenTelemetry.AspNetCore;
using Metrics.Custom;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Microsoft.AspNetCore.Hosting");

        metrics.AddMyConsoleExporter((exporterOptions, readerOptions) =>
        {
            readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 60_000;
        });
    })
    .WithLogging()
    .UseAzureMonitor()
;

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => "Metrics TEST");

app.Run();
