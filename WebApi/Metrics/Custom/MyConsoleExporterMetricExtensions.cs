using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

namespace Metrics.Custom;

public static class MyConsoleExporterMetricExtensions
{
    private const int DefaultExportIntervalMilliseconds = 10000;
    private const int DefaultExportTimeoutMilliseconds = Timeout.Infinite;
    
    public static MeterProviderBuilder AddMyConsoleExporter(
        this MeterProviderBuilder builder,
        Action<ConsoleExporterOptions, MetricReaderOptions> configureExporterAndMetricReader)
        => AddMyConsoleExporter(builder, name: null, configureExporterAndMetricReader);
    
    public static MeterProviderBuilder AddMyConsoleExporter(
        this MeterProviderBuilder builder,
        string name,
        Action<ConsoleExporterOptions, MetricReaderOptions> configureExporterAndMetricReader)
    {
        name ??= Options.DefaultName;

        return builder.AddReader(sp =>
        {
            var exporterOptions = sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name);
            var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name);

            configureExporterAndMetricReader?.Invoke(exporterOptions, metricReaderOptions);

            return BuildConsoleExporterMetricReader(exporterOptions, metricReaderOptions);
        });
    }

    private static MetricReader BuildConsoleExporterMetricReader(
        ConsoleExporterOptions exporterOptions,
        MetricReaderOptions metricReaderOptions)
    {
        var metricExporter = new MyConsoleMetricExporter(exporterOptions);

        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions,
            DefaultExportIntervalMilliseconds,
            DefaultExportTimeoutMilliseconds);
    }
}

internal static class PeriodicExportingMetricReaderHelper
{
    internal const int DefaultExportIntervalMilliseconds = 60000;
    internal const int DefaultExportTimeoutMilliseconds = 30000;

    internal static PeriodicExportingMetricReader CreatePeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        MetricReaderOptions options,
        int defaultExportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
        int defaultExportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
    {
        var exportInterval =
            options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds ?? defaultExportIntervalMilliseconds;

        var exportTimeout =
            options.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds ?? defaultExportTimeoutMilliseconds;

        var metricReader = new PeriodicExportingMetricReader(exporter, exportInterval, exportTimeout)
        {
            TemporalityPreference = options.TemporalityPreference,
        };

        return metricReader;
    }
}