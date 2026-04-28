using System.Diagnostics.Metrics;
using System.Threading;
using WeUP.Domain.Flyer;

namespace WeUP.Infrastructure.Flyer;

/// <summary>
/// Metric names for OCR and normalization telemetry (M10-P47).
///
/// Naming convention:
/// - OCR-only metrics start with "ocr_".
/// - Normalization-only metrics start with "normalization_" or "field_".
/// - No shared metric names between OCR and normalization stages.
/// </summary>
public static class OcrNormalizationMetricNames
{
    public const string OcrExtractionSuccessRate = "ocr_extraction_success_rate";
    public const string OcrConfidenceAverage = "ocr_confidence_avg";
    public const string OcrConfidenceDistribution = "ocr_confidence_distribution";
    public const string NormalizationSuccessRate = "normalization_success_rate";
    public const string FieldExtractionCompleteness = "field_extraction_completeness";
    public const string TitleExtracted = "title_extracted";
    public const string TimeExtracted = "time_extracted";
    public const string VenueExtracted = "venue_extracted";
    public const string TitleMissing = "title_missing";
    public const string TimeMissing = "time_missing";
    public const string VenueMissing = "venue_missing";
}

/// <summary>
/// Sample dashboard queries for Prometheus-compatible backends.
///
/// Interpretation guidance:
/// - ocr_extraction_success_rate: sustained drop below 0.95 is usually provider health degradation.
/// - ocr_confidence_avg: sudden drop >0.15 from rolling baseline suggests OCR quality drift.
/// - normalization_success_rate: sustained drop below 0.90 indicates parsing regressions.
/// - field_extraction_completeness: values below 0.80 indicate missing-field pressure and should alert.
/// </summary>
public static class OcrNormalizationDashboardQueries
{
    public const string OcrExtractionSuccessRate5m = "avg_over_time(ocr_extraction_success_rate[5m])";
    public const string OcrConfidenceAverage5m = "avg_over_time(ocr_confidence_avg[5m])";
    public const string OcrConfidenceP50 = "histogram_quantile(0.50, sum(rate(ocr_confidence_distribution_bucket[5m])) by (le))";
    public const string OcrConfidenceP90 = "histogram_quantile(0.90, sum(rate(ocr_confidence_distribution_bucket[5m])) by (le))";
    public const string NormalizationSuccessRate5m = "avg_over_time(normalization_success_rate[5m])";
    public const string FieldCompleteness5m = "avg_over_time(field_extraction_completeness[5m])";
    public const string TitleExtractionRate5m = "rate(title_extracted[5m]) / clamp_min(rate(title_extracted[5m]) + rate(title_missing[5m]), 1e-9)";
    public const string TimeExtractionRate5m = "rate(time_extracted[5m]) / clamp_min(rate(time_extracted[5m]) + rate(time_missing[5m]), 1e-9)";
    public const string VenueExtractionRate5m = "rate(venue_extracted[5m]) / clamp_min(rate(venue_extracted[5m]) + rate(venue_missing[5m]), 1e-9)";
}

public interface IOcrNormalizationTelemetry
{
    void TrackOcrExtraction(string provider, string providerVersion, bool success, double confidence);
    void TrackNormalization(EventCandidate candidate);
}

/// <summary>
/// Tracks OCR and normalization telemetry at full fidelity (no sampling).
///
/// Field completeness uses EventCandidate authority fields:
/// - title: EventCandidate.Title
/// - time: EventCandidate.StartUtc
/// - venue: EventCandidate.Venue
/// </summary>
public sealed class OcrNormalizationMetricsService : IOcrNormalizationTelemetry, IDisposable
{
    private const long Scale = 1_000_000L;

    private readonly Meter _meter;

    private readonly ObservableGauge<double> _ocrExtractionSuccessRate;
    private readonly ObservableGauge<double> _ocrConfidenceAverage;
    private readonly ObservableGauge<double> _normalizationSuccessRate;
    private readonly ObservableGauge<double> _fieldExtractionCompletenessAverage;

    private readonly Histogram<double> _ocrConfidenceDistribution;
    private readonly Histogram<double> _fieldExtractionCompleteness;

    private readonly Counter<long> _titleExtracted;
    private readonly Counter<long> _timeExtracted;
    private readonly Counter<long> _venueExtracted;

    private readonly Counter<long> _titleMissing;
    private readonly Counter<long> _timeMissing;
    private readonly Counter<long> _venueMissing;

    private long _ocrTotal;
    private long _ocrSucceeded;
    private long _ocrConfidenceScaledSum;
    private long _ocrConfidenceCount;

    private long _normalizationTotal;
    private long _normalizationSucceeded;
    private long _fieldCompletenessScaledSum;
    private long _fieldCompletenessCount;

    public OcrNormalizationMetricsService(string meterName = "WeUP.OcrNormalization")
    {
        _meter = new Meter(meterName, "1.0");

        _ocrExtractionSuccessRate = _meter.CreateObservableGauge(
            name: OcrNormalizationMetricNames.OcrExtractionSuccessRate,
            observeValue: () => ComputeRate(_ocrSucceeded, _ocrTotal),
            unit: "ratio",
            description: "OCR extraction success ratio across all requests.");

        _ocrConfidenceAverage = _meter.CreateObservableGauge(
            name: OcrNormalizationMetricNames.OcrConfidenceAverage,
            observeValue: () => ComputeAverage(_ocrConfidenceScaledSum, _ocrConfidenceCount),
            unit: "ratio",
            description: "Average OCR confidence across all extraction attempts.");

        _normalizationSuccessRate = _meter.CreateObservableGauge(
            name: OcrNormalizationMetricNames.NormalizationSuccessRate,
            observeValue: () => ComputeRate(_normalizationSucceeded, _normalizationTotal),
            unit: "ratio",
            description: "Normalization success ratio derived from EventCandidate core fields.");

        _fieldExtractionCompletenessAverage = _meter.CreateObservableGauge(
            name: OcrNormalizationMetricNames.FieldExtractionCompleteness,
            observeValue: () => ComputeAverage(_fieldCompletenessScaledSum, _fieldCompletenessCount),
            unit: "ratio",
            description: "Average completeness across title/time/venue extraction.");

        _ocrConfidenceDistribution = _meter.CreateHistogram<double>(
            name: OcrNormalizationMetricNames.OcrConfidenceDistribution,
            unit: "ratio",
            description: "Distribution of OCR confidence scores in [0,1].");

        _fieldExtractionCompleteness = _meter.CreateHistogram<double>(
            name: OcrNormalizationMetricNames.FieldExtractionCompleteness,
            unit: "ratio",
            description: "Per-event completeness ratio across title/time/venue extraction.");

        _titleExtracted = _meter.CreateCounter<long>(
            name: OcrNormalizationMetricNames.TitleExtracted,
            unit: "{event}",
            description: "Count of events where title was extracted.");

        _timeExtracted = _meter.CreateCounter<long>(
            name: OcrNormalizationMetricNames.TimeExtracted,
            unit: "{event}",
            description: "Count of events where start time was extracted.");

        _venueExtracted = _meter.CreateCounter<long>(
            name: OcrNormalizationMetricNames.VenueExtracted,
            unit: "{event}",
            description: "Count of events where venue was extracted.");

        _titleMissing = _meter.CreateCounter<long>(
            name: OcrNormalizationMetricNames.TitleMissing,
            unit: "{event}",
            description: "Count of events where title was missing after normalization.");

        _timeMissing = _meter.CreateCounter<long>(
            name: OcrNormalizationMetricNames.TimeMissing,
            unit: "{event}",
            description: "Count of events where start time was missing after normalization.");

        _venueMissing = _meter.CreateCounter<long>(
            name: OcrNormalizationMetricNames.VenueMissing,
            unit: "{event}",
            description: "Count of events where venue was missing after normalization.");
    }

    public void TrackOcrExtraction(string provider, string providerVersion, bool success, double confidence)
    {
        confidence = Math.Clamp(confidence, 0.0, 1.0);

        var tags = new KeyValuePair<string, object?>[]
        {
            new("provider", provider),
            new("provider_version", providerVersion),
            new("status", success ? "success" : "failure"),
        };

        Interlocked.Increment(ref _ocrTotal);
        if (success)
        {
            Interlocked.Increment(ref _ocrSucceeded);
        }

        Interlocked.Increment(ref _ocrConfidenceCount);
        Interlocked.Add(ref _ocrConfidenceScaledSum, ToScaled(confidence));

        _ocrConfidenceDistribution.Record(confidence, tags);
    }

    public void TrackNormalization(EventCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var titleExtracted = !string.IsNullOrWhiteSpace(candidate.Title);
        var timeExtracted = candidate.StartUtc.HasValue;
        var venueExtracted = !string.IsNullOrWhiteSpace(candidate.Venue);

        var extractedCount = (titleExtracted ? 1 : 0)
            + (timeExtracted ? 1 : 0)
            + (venueExtracted ? 1 : 0);

        var completeness = extractedCount / 3.0;

        // Normalization success requires all core fields; partial outputs are tracked separately.
        var success = extractedCount == 3;

        var tags = new KeyValuePair<string, object?>[]
        {
            new("status", success ? "success" : "incomplete"),
        };

        Interlocked.Increment(ref _normalizationTotal);
        if (success)
        {
            Interlocked.Increment(ref _normalizationSucceeded);
        }

        Interlocked.Increment(ref _fieldCompletenessCount);
        Interlocked.Add(ref _fieldCompletenessScaledSum, ToScaled(completeness));
        _fieldExtractionCompleteness.Record(completeness, tags);

        if (titleExtracted)
        {
            _titleExtracted.Add(1);
        }
        else
        {
            _titleMissing.Add(1);
        }

        if (timeExtracted)
        {
            _timeExtracted.Add(1);
        }
        else
        {
            _timeMissing.Add(1);
        }

        if (venueExtracted)
        {
            _venueExtracted.Add(1);
        }
        else
        {
            _venueMissing.Add(1);
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static double ComputeRate(long numerator, long denominator)
    {
        if (denominator <= 0)
        {
            return 0.0;
        }

        return Math.Clamp(numerator / (double)denominator, 0.0, 1.0);
    }

    private static double ComputeAverage(long scaledSum, long count)
    {
        if (count <= 0)
        {
            return 0.0;
        }

        return Math.Clamp((scaledSum / (double)count) / Scale, 0.0, 1.0);
    }

    private static long ToScaled(double value)
        => (long)Math.Round(Math.Clamp(value, 0.0, 1.0) * Scale, MidpointRounding.AwayFromZero);
}
