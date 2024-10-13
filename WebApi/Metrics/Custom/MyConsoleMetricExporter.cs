using System.Globalization;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Metrics.Custom;

public class MyConsoleMetricExporter : ConsoleExporter<Metric>
{
    private Resource resource;

    public MyConsoleMetricExporter(ConsoleExporterOptions options)
        : base(options)
    {
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (var metric in batch)
        {
            if(metric.Name != "http.server.active_requests")
                continue;
            
            var msg = new StringBuilder($"\n");
            msg.Append($"Metric Name: {metric.Name}");
            if (metric.Description != string.Empty)
            {
                msg.Append(", ");
                msg.Append(metric.Description);
            }

            if (metric.Unit != string.Empty)
            {
                msg.Append($", Unit: {metric.Unit}");
            }

            if (!string.IsNullOrEmpty(metric.MeterName))
            {
                msg.Append($", Meter: {metric.MeterName}");

                if (!string.IsNullOrEmpty(metric.MeterVersion))
                {
                    msg.Append($"/{metric.MeterVersion}");
                }
            }

            this.WriteLine(msg.ToString());

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                string valueDisplay = string.Empty;
                StringBuilder tagsBuilder = new StringBuilder();

                var tags = tagsBuilder.ToString().TrimEnd();

                var metricType = metric.MetricType;

                if (metricType == MetricType.Histogram || metricType == MetricType.ExponentialHistogram)
                {
                    var bucketsBuilder = new StringBuilder();
                    var sum = metricPoint.GetHistogramSum();
                    var count = metricPoint.GetHistogramCount();
                    bucketsBuilder.Append($"Sum: {sum} Count: {count} ");
                    if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                    {
                        bucketsBuilder.Append($"Min: {min} Max: {max} ");
                    }

                    bucketsBuilder.AppendLine();

                    if (metricType == MetricType.Histogram)
                    {
                        bool isFirstIteration = true;
                        double previousExplicitBound = default;
                        foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
                        {
                            if (isFirstIteration)
                            {
                                bucketsBuilder.Append("(-Infinity,");
                                bucketsBuilder.Append(histogramMeasurement.ExplicitBound);
                                bucketsBuilder.Append(']');
                                bucketsBuilder.Append(':');
                                bucketsBuilder.Append(histogramMeasurement.BucketCount);
                                previousExplicitBound = histogramMeasurement.ExplicitBound;
                                isFirstIteration = false;
                            }
                            else
                            {
                                bucketsBuilder.Append('(');
                                bucketsBuilder.Append(previousExplicitBound);
                                bucketsBuilder.Append(',');
                                if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                                {
                                    bucketsBuilder.Append(histogramMeasurement.ExplicitBound);
                                    previousExplicitBound = histogramMeasurement.ExplicitBound;
                                }
                                else
                                {
                                    bucketsBuilder.Append("+Infinity");
                                }

                                bucketsBuilder.Append(']');
                                bucketsBuilder.Append(':');
                                bucketsBuilder.Append(histogramMeasurement.BucketCount);
                            }

                            bucketsBuilder.AppendLine();
                        }
                    }
                    else
                    {
                        var exponentialHistogramData = metricPoint.GetExponentialHistogramData();
                        var scale = exponentialHistogramData.Scale;

                        if (exponentialHistogramData.ZeroCount != 0)
                        {
                            bucketsBuilder.AppendLine($"Zero Bucket:{exponentialHistogramData.ZeroCount}");
                        }

                        var offset = exponentialHistogramData.PositiveBuckets.Offset;
                        foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
                        {
                            var lowerBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(offset, scale)
                                .ToString(CultureInfo.InvariantCulture);
                            var upperBound = Base2ExponentialBucketHistogramHelper
                                .CalculateLowerBoundary(++offset, scale).ToString(CultureInfo.InvariantCulture);
                            bucketsBuilder.AppendLine($"({lowerBound}, {upperBound}]:{bucketCount}");
                        }
                    }

                    valueDisplay = bucketsBuilder.ToString();
                }
                else if (metricType.IsDouble())
                {
                    if (metricType.IsSum())
                    {
                        valueDisplay = metricPoint.GetSumDouble().ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        valueDisplay = metricPoint.GetGaugeLastValueDouble().ToString(CultureInfo.InvariantCulture);
                    }
                }
                else if (metricType.IsLong())
                {
                    if (metricType.IsSum())
                    {
                        valueDisplay = metricPoint.GetSumLong().ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        valueDisplay = metricPoint.GetGaugeLastValueLong().ToString(CultureInfo.InvariantCulture);
                    }
                }

                var exemplarString = new StringBuilder();
                if (metricPoint.TryGetExemplars(out var exemplars))
                {
                    foreach (ref readonly var exemplar in exemplars)
                    {
                        exemplarString.Append("Timestamp: ");
                        exemplarString.Append(exemplar.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ",
                            CultureInfo.InvariantCulture));
                        if (metricType.IsDouble())
                        {
                            exemplarString.Append(" Value: ");
                            exemplarString.Append(exemplar.DoubleValue);
                        }
                        else if (metricType.IsLong())
                        {
                            exemplarString.Append(" Value: ");
                            exemplarString.Append(exemplar.LongValue);
                        }

                        if (exemplar.TraceId != default)
                        {
                            exemplarString.Append(" TraceId: ");
                            exemplarString.Append(exemplar.TraceId.ToHexString());
                            exemplarString.Append(" SpanId: ");
                            exemplarString.Append(exemplar.SpanId.ToHexString());
                        }

                        exemplarString.AppendLine();
                    }
                }

                msg = new StringBuilder();
                msg.Append('(');
                msg.Append(metricPoint.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ",
                    CultureInfo.InvariantCulture));
                msg.Append(", ");
                msg.Append(metricPoint.EndTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                msg.Append("] ");
                msg.Append(tags);
                if (tags != string.Empty)
                {
                    msg.Append(' ');
                }

                msg.Append(metric.MetricType);
                msg.AppendLine();
                msg.Append($"Value: {valueDisplay}");

                if (exemplarString.Length > 0)
                {
                    msg.AppendLine();
                    msg.AppendLine("Exemplars");
                    msg.Append(exemplarString.ToString());
                }

                this.WriteLine(msg.ToString());
            }
        }

        return ExportResult.Success;
    }
}

internal static class Base2ExponentialBucketHistogramHelper
{
    private const double EpsilonTimes2 = double.Epsilon * 2;
    private static readonly double Ln2 = Math.Log(2);

    /// <summary>
    /// Calculate the lower boundary for a Base2ExponentialBucketHistogram bucket.
    /// </summary>
    /// <param name="index">Index.</param>
    /// <param name="scale">Scale.</param>
    /// <returns>Calculated lower boundary.</returns>
    public static double CalculateLowerBoundary(int index, int scale)
    {
        if (scale > 0)
        {
#if NET6_0_OR_GREATER
            var inverseFactor = Math.ScaleB(Ln2, -scale);
#else
            var inverseFactor = ScaleB(Ln2, -scale);
#endif
            var lowerBound = Math.Exp(index * inverseFactor);
            return lowerBound == 0 ? double.Epsilon : lowerBound;
        }
        else
        {
            if ((scale == -1 && index == -537) || (scale == 0 && index == -1074))
            {
                return EpsilonTimes2;
            }

            var n = index << -scale;

            // LowerBoundary should not return zero.
            // It should return values >= double.Epsilon (2 ^ -1074).
            // n < -1074 occurs at the minimum index of a scale.
            // e.g., At scale -1, minimum index is -538. -538 << 1 = -1075
            if (n < -1074)
            {
                return double.Epsilon;
            }

#if NET6_0_OR_GREATER
            return Math.ScaleB(1, n);
#else
            return ScaleB(1, n);
#endif
        }
    }

#if !NET6_0_OR_GREATER
    // Math.ScaleB was introduced in .NET Core 3.0.
    // This implementation is from:
    // https://github.com/dotnet/runtime/blob/v7.0.0/src/libraries/System.Private.CoreLib/src/System/Math.cs#L1494
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1203 // Constants should appear before fields
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1119 // Statement should not use unnecessary parenthesis
    private const double SCALEB_C1 = 8.98846567431158E+307; // 0x1p1023
    private const double SCALEB_C2 = 2.2250738585072014E-308; // 0x1p-1022
    private const double SCALEB_C3 = 9007199254740992; // 0x1p53

    private static double ScaleB(double x, int n)
    {
        // Implementation based on https://git.musl-libc.org/cgit/musl/tree/src/math/scalbln.c
        //
        // Performs the calculation x * 2^n efficiently. It constructs a double from 2^n by building
        // the correct biased exponent. If n is greater than the maximum exponent (1023) or less than
        // the minimum exponent (-1022), adjust x and n to compute correct result.

        double y = x;
        if (n > 1023)
        {
            y *= SCALEB_C1;
            n -= 1023;
            if (n > 1023)
            {
                y *= SCALEB_C1;
                n -= 1023;
                if (n > 1023)
                {
                    n = 1023;
                }
            }
        }
        else if (n < -1022)
        {
            y *= SCALEB_C2 * SCALEB_C3;
            n += 1022 - 53;
            if (n < -1022)
            {
                y *= SCALEB_C2 * SCALEB_C3;
                n += 1022 - 53;
                if (n < -1022)
                {
                    n = -1022;
                }
            }
        }

        double u = BitConverter.Int64BitsToDouble(((long)(0x3ff + n) << 52));
        return y * u;
    }
#pragma warning restore SA1119 // Statement should not use unnecessary parenthesis
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1203 // Constants should appear before fields
#pragma warning restore SA1201 // Elements should appear in the correct order
#endif
}