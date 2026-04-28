# Ingestion Performance Dashboard v1.0 (M10-P50)

## Purpose

This dashboard is the minimum production dashboard for answering four operator questions fast:

1. Is ingestion keeping up with incoming work?
2. Are failures or quality regressions increasing?
3. Is dedup or merge behavior creating reviewer pressure?
4. Is moderation backlog growing faster than the team can clear it?

The dashboard is intentionally narrow. Every panel below maps to a concrete action and uses only metrics defined in M10-P46 through M10-P49.

## Metric Contract Used

### M10-P46 ingestion pipeline

- `ingestion_requests_total{sourceType,status,market}`
- `ingestion_jobs_completed_total{sourceType,status,market}`
- `ingestion_jobs_failed_total{sourceType,status,market}`
- `ingestion_job_duration_ms{sourceType,status,market}` histogram

### M10-P47 OCR and normalization quality

- `ocr_extraction_success_rate`
- `ocr_confidence_avg`
- `ocr_confidence_distribution{provider,provider_version,status}` histogram
- `normalization_success_rate`
- `field_extraction_completeness` gauge
- `field_extraction_completeness{status}` histogram
- `title_extracted`
- `time_extracted`
- `venue_extracted`
- `title_missing`
- `time_missing`
- `venue_missing`

### M10-P48 dedup and merge outcomes

- `dedup_assessment_total{level,auto_merge_allowed,has_blockers}`
- `merge_plans_created_total`
- `merge_auto_approved_total`
- `merge_requires_review_total`
- `merge_rejected_total`
- `merge_field_conflicts_total{field}`
- `title_conflicts`
- `time_conflicts`
- `venue_conflicts`

### M10-P49 moderation queue health

- `moderation_queue_size{risk_level,moderation_status}`
- `moderation_items_processed_total{risk_level,moderation_status,action,actor_type}`
- `moderation_items_pending_total{risk_level,moderation_status}`
- `moderation_processing_time_ms{risk_level,moderation_status,action,actor_type}` histogram
- `moderation_backlog_age_ms{risk_level,moderation_status}`

## Suggested Dashboard Layout

### Row 1: Pipeline Health

Left to right:

1. Ingestion throughput
2. Success vs failure rate
3. Ingestion bottleneck latency

### Row 2: Extraction and Normalization Quality

Left to right:

1. OCR confidence distribution
2. Normalization completeness
3. Field extraction completeness by field

### Row 3: Dedup and Merge Pressure

Left to right:

1. Dedup classification breakdown
2. Merge conflict frequency

### Row 4: Moderation Load

Left to right:

1. Moderation queue size
2. Moderation backlog age
3. Moderation processing throughput

## Panel Definitions

### 1. Ingestion Throughput

- Panel type: Time series
- Decision supported: Determine whether the pipeline is accepting work and finishing it at an expected rate.
- Primary PromQL:

```promql
sum(rate(ingestion_requests_total[5m]))
```

- Supporting PromQL:

```promql
sum(rate(ingestion_jobs_completed_total[5m]))
sum(rate(ingestion_jobs_failed_total[5m]))
```

- Suggested legend:
  - `accepted rps`
  - `completed rps`
  - `failed rps`
- Interpretation:
  - `accepted rps` rising while `completed rps` stays flat means the pipeline is falling behind.
  - `accepted rps` flat with both terminal rates dropping usually means a stuck worker or stalled terminal transitions.
  - `failed rps` should stay near zero outside controlled failure drills.
- Expected range:
  - Environment-specific, but `completed rps` should roughly track `accepted rps` over sustained windows.
- Immediate action:
  - If accepted work is normal but completions drop, inspect ingestion worker health and recent deploys.

### 2. Success vs Failure Rate

- Panel type: Stat plus small time series
- Decision supported: Decide whether failures are transient noise or an active incident.
- Primary PromQL for success rate:

```promql
sum(rate(ingestion_jobs_completed_total[5m]))
/
clamp_min(sum(rate(ingestion_jobs_completed_total[5m])) + sum(rate(ingestion_jobs_failed_total[5m])), 1e-9)
```

- Primary PromQL for failure rate:

```promql
sum(rate(ingestion_jobs_failed_total[5m]))
/
clamp_min(sum(rate(ingestion_jobs_completed_total[5m])) + sum(rate(ingestion_jobs_failed_total[5m])), 1e-9)
```

- Debug query for breakdown by source type:

```promql
sum(rate(ingestion_jobs_failed_total[15m])) by (sourceType)
```

- Interpretation:
  - This is the incident panel. If failure rate rises, the next move is to use the source-type breakdown to find the failing path.
  - High failure rate with normal throughput means bad work is flowing through fast.
  - High failure rate with low throughput means deeper pipeline impairment.
- Expected range:
  - Healthy steady-state failure rate should stay below 2 percent.
  - Anything above 5 percent for more than one alert window is operator-actionable.
- Immediate action:
  - Slice by `sourceType` first, then correlate with OCR quality and job latency panels.

### 3. Ingestion Bottleneck Latency

- Panel type: Time series
- Decision supported: Identify whether ingestion is slow even when it is not yet failing.
- Primary PromQL:

```promql
histogram_quantile(
  0.95,
  sum(rate(ingestion_job_duration_ms_bucket[5m])) by (le, sourceType, status)
)
```

- Optional aggregate view:

```promql
histogram_quantile(
  0.95,
  sum(rate(ingestion_job_duration_ms_bucket[5m])) by (le)
)
```

- Interpretation:
  - Rising p95 with flat failure rate usually means external dependency slowness, OCR slowness, or queueing.
  - Rising p95 plus rising failure rate means the system is timing out or collapsing under load.
- Expected range:
  - Should stay within the known ingest SLA for the deployment tier.
  - Watch for sustained doubling from the service baseline, even if absolute thresholds are not yet crossed.
- Immediate action:
  - Compare the slowest `sourceType` and `status` series to isolate where time is being spent.

### 4. OCR Confidence Distribution

- Panel type: Heatmap or percentile time series
- Decision supported: Detect OCR quality degradation before it turns into normalization failures.
- Primary PromQL for p50 and p90:

```promql
histogram_quantile(
  0.50,
  sum(rate(ocr_confidence_distribution_bucket[15m])) by (le)
)
```

```promql
histogram_quantile(
  0.90,
  sum(rate(ocr_confidence_distribution_bucket[15m])) by (le)
)
```

- Debug query by provider:

```promql
histogram_quantile(
  0.50,
  sum(rate(ocr_confidence_distribution_bucket{status="success"}[15m])) by (le, provider)
)
```

- Interpretation:
  - Falling median confidence is usually the first signal of degraded OCR inputs, provider drift, or preprocessing regressions.
  - A healthy system can tolerate some low-confidence tail, but the median should not collapse.
- Expected range:
  - p50 should usually stay above 0.80.
  - p90 should usually stay above 0.90 for clean flyer inputs.
- Immediate action:
  - If the aggregate drops, compare providers and recent asset-quality changes before changing thresholds.

### 5. Normalization Completeness

- Panel type: Stat plus time series
- Decision supported: Decide whether extracted OCR text is turning into usable event candidates.
- Primary PromQL:

```promql
avg_over_time(field_extraction_completeness[5m])
```

- Secondary PromQL:

```promql
avg_over_time(normalization_success_rate[5m])
```

- Interpretation:
  - `field_extraction_completeness` shows average coverage of title, time, and venue.
  - `normalization_success_rate` is stricter and only counts events with all core fields present.
  - If completeness is stable but success rate falls, parsing logic is likely stricter or broken.
  - If both fall together, OCR input quality or shared extraction logic is degrading.
- Expected range:
  - Completeness should usually stay above 0.85.
  - Normalization success should usually stay above 0.90.
- Immediate action:
  - Compare this panel with OCR confidence first. If OCR is healthy, investigate normalization/parser changes.

### 6. Field Extraction Completeness by Field

- Panel type: Bar chart
- Decision supported: Identify which core field is driving incomplete candidates.
- Title rate:

```promql
rate(title_extracted[15m])
/
clamp_min(rate(title_extracted[15m]) + rate(title_missing[15m]), 1e-9)
```

- Time rate:

```promql
rate(time_extracted[15m])
/
clamp_min(rate(time_extracted[15m]) + rate(time_missing[15m]), 1e-9)
```

- Venue rate:

```promql
rate(venue_extracted[15m])
/
clamp_min(rate(venue_extracted[15m]) + rate(venue_missing[15m]), 1e-9)
```

- Interpretation:
  - This is the fastest path to root cause after quality drops.
  - Missing title spikes point to OCR legibility or template shifts.
  - Missing time spikes point to temporal parser or format drift.
  - Missing venue spikes point to location parsing problems.
- Expected range:
  - All three should be above 0.90 in healthy steady state.
- Immediate action:
  - Route triage to the owning parser depending on which field collapses first.

### 7. Dedup Classification Breakdown

- Panel type: Stacked bar or stacked time series
- Decision supported: Distinguish healthy idempotency from ambiguous duplicate pressure.
- Primary PromQL:

```promql
sum(rate(dedup_assessment_total[15m])) by (level)
```

- Supporting ratio query:

```promql
sum(rate(dedup_assessment_total{level=~"ExactDuplicate|ProbableDuplicate|PossibleDuplicate"}[15m]))
/
clamp_min(sum(rate(dedup_assessment_total[15m])), 1e-9)
```

- Interpretation:
  - `ExactDuplicate` growth on its own is often healthy replay/idempotency.
  - `ProbableDuplicate` and `PossibleDuplicate` growth creates ambiguity and reviewer pressure.
  - Breakdown should be read together with the merge review and moderation backlog panels.
- Expected range:
  - No fixed universal threshold, but ambiguous levels should remain materially lower than `ExactDuplicate`.
- Immediate action:
  - If ambiguous duplicate levels climb, inspect upstream source replay, looser matching, or entity resolution regressions.

### 8. Merge Conflict Frequency

- Panel type: Time series
- Decision supported: Identify which merge conflicts are driving moderation work.
- Primary PromQL:

```promql
sum(rate(merge_field_conflicts_total[15m])) by (field)
```

- Supporting explicit field queries:

```promql
sum(rate(title_conflicts[15m]))
sum(rate(time_conflicts[15m]))
sum(rate(venue_conflicts[15m]))
```

- Interpretation:
  - A rising `time` conflict series usually means schedule ambiguity or timezone drift.
  - A rising `venue` conflict series usually means entity resolution or venue canonicalization problems.
  - A rising `title` conflict series usually means noisy OCR or inconsistent source naming.
- Expected range:
  - Conflict rates should remain low relative to `merge_plans_created_total`.
- Immediate action:
  - Correlate the dominant field with parser quality and dedup classification changes.

### 9. Moderation Queue Size

- Panel type: Stacked time series
- Decision supported: Show current reviewer load by risk and workflow state.
- Primary PromQL:

```promql
max by (risk_level, moderation_status) (
  max_over_time(moderation_queue_size[5m])
)
```

- Focus query for actionable backlog:

```promql
max by (risk_level, moderation_status) (
  max_over_time(moderation_items_pending_total[5m])
)
```

- Interpretation:
  - This is the operational workload panel.
  - Rising `high` or `restricted` risk queues require staffing or automatic policy adjustments sooner than low-risk growth.
  - Flat queue size with rising backlog age means work is starving, not just arriving.
- Expected range:
  - High-risk pending backlog should usually stay below 25 items for MVP operations.
- Immediate action:
  - Prioritize high-risk lanes first, then investigate whether merge review pressure is the feeder.

### 10. Moderation Backlog Age

- Panel type: Stat plus time series
- Decision supported: Decide whether backlog is merely large or actually becoming stale.
- Primary PromQL:

```promql
max_over_time(moderation_backlog_age_ms{moderation_status=~"pending|in_review|needs_edit"}[10m])
```

- Interpretation:
  - This is the best staleness signal. Size alone can hide starvation.
  - Rising age with flat processed throughput means the queue is not being drained in the oldest-first path.
- Expected range:
  - Oldest actionable item should stay well under 30 minutes in normal operation.
- Immediate action:
  - If age grows while queue size is stable, investigate reviewer workflow bottlenecks and routing policy.

### 11. Moderation Processing Throughput

- Panel type: Time series
- Decision supported: Determine whether the review team or system automation is clearing work.
- Primary PromQL:

```promql
sum(rate(moderation_items_processed_total[5m])) by (risk_level, moderation_status)
```

- Debug query by actor type:

```promql
sum(rate(moderation_items_processed_total[5m])) by (actor_type)
```

- Interpretation:
  - If queue size rises but processed throughput is flat, staffing or automation is insufficient.
  - If system throughput falls but reviewer throughput is unchanged, automated moderation paths likely regressed.
- Expected range:
  - Should at least match incoming review pressure over sustained windows.
- Immediate action:
  - Compare actor type split to determine whether the slowdown is in human review or automation.

## Alert Rules

These are intentionally simple MVP alerts. They are meant to page on real operational decisions, not dashboard trivia.

### 1. Ingestion Failure Spike

```yaml
groups:
  - name: ingestion-dashboard-v1
    rules:
      - alert: IngestionFailureSpike
        expr: |
          (
            sum(rate(ingestion_jobs_failed_total[10m]))
            /
            clamp_min(
              sum(rate(ingestion_jobs_completed_total[10m])) + sum(rate(ingestion_jobs_failed_total[10m])),
              1e-9
            )
          ) > 0.05
        for: 10m
        labels:
          severity: page
          area: ingestion
        annotations:
          summary: Ingestion failure rate above 5%
          description: Failure rate has stayed above 5% for 10 minutes. Break down by sourceType and compare with OCR and latency panels.
```

- Why this threshold:
  - Above 5 percent sustained is no longer noise for a production ingestion pipeline.
- First debugging move:
  - Group failed jobs by `sourceType`, then check OCR confidence and p95 duration.

### 2. OCR Degradation

```yaml
groups:
  - name: ingestion-dashboard-v1
    rules:
      - alert: OcrConfidenceDegradation
        expr: |
          avg_over_time(ocr_confidence_avg[15m]) < 0.75
        for: 15m
        labels:
          severity: warning
          area: ocr
        annotations:
          summary: OCR confidence degraded
          description: Average OCR confidence has been below 0.75 for 15 minutes. Inspect provider-level confidence and normalization completeness next.
```

- Why this threshold:
  - It is below the healthy median band and early enough to catch quality drift before full normalization collapse.
- First debugging move:
  - Compare provider-level confidence percentiles and recent input-quality changes.

### 3. Moderation Backlog Growth

```yaml
groups:
  - name: ingestion-dashboard-v1
    rules:
      - alert: ModerationBacklogGrowth
        expr: |
          max(max_over_time(moderation_items_pending_total{risk_level=~"high|restricted"}[10m])) > 25
          and
          max(max_over_time(moderation_backlog_age_ms{risk_level=~"high|restricted",moderation_status=~"pending|in_review|needs_edit"}[10m])) > 1800000
        for: 10m
        labels:
          severity: page
          area: moderation
        annotations:
          summary: High-risk moderation backlog is growing and aging
          description: High-risk pending backlog is above 25 items and the oldest item is older than 30 minutes. Check merge review pressure and reviewer throughput.
```

- Why this threshold:
  - This catches real queue stress, not a temporary burst that is still being drained.
- First debugging move:
  - Compare merge review pressure, moderation throughput, and actor type split.

## Dashboard Variables

Use a small variable set only when it helps isolate failures quickly.

- `$market` from `label_values(ingestion_requests_total, market)`
- `$sourceType` from `label_values(ingestion_requests_total, sourceType)`
- `$provider` from `label_values(ocr_confidence_distribution_bucket, provider)`
- `$risk_level` from `label_values(moderation_queue_size, risk_level)`

Default behavior should remain global. Operators should not need filters just to see a problem.

## Operator Runbook Notes

- Failure spike + normal OCR + rising latency: likely pipeline or dependency bottleneck, not input quality.
- Failure spike + OCR degradation + normalization drop: likely OCR/provider/input-quality problem.
- Ambiguous dedup growth + merge conflict growth + moderation backlog growth: likely dedup threshold or merge-planning pressure.
- High queue size + low backlog age: burst traffic, watch but do not page unless age rises.
- Stable queue size + rising backlog age: starvation or routing failure, treat as operational issue.

## Non-Goals

- No vanity counters with no action attached.
- No full-screen drilldown dashboard for every source or provider.
- No per-job or per-user cardinality.
- No alert on every single quality wobble.

This is an MVP operations dashboard. It is deliberately optimized for fast incident triage, not for exhaustive analytics.