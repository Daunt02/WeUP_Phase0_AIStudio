# WeUP Flyer Ingestion Pipeline (P11)

## Overview
The Flyer Ingestion Pipeline is a specialized deterministic pipeline designed to convert unstructured flyer assets (images/PDFs) into normalized canonical event candidates. It builds upon the core Ingestion Framework (P10) and introduces multi-stage processing with preserved evidence and confidence scoring.

## Pipeline Stages

### 1. Asset Registration
The process begins when a flyer asset is uploaded. The `FlyerOcrAdapter` receives an `assetId` or temporary path.

### 2. OCR Extraction (`IFlyerOcrService`)
- **Input**: Asset Reference.
- **Output**: Raw OCR text + Confidence.
- **Purpose**: Extract all readable text from the visual asset.
- **Seam**: Currently implemented as `StubFlyerOcrService`. Ready for Google Cloud Vision or AWS Textract.

### 3. LLM Normalization (`IFlyerNormalizationService`)
- **Input**: Raw OCR Text.
- **Output**: `CanonicalEventCandidate` + `ConfidenceVector`.
- **Purpose**: Use Natural Language Processing (LLM) to identify entities (Title, Venue, Date, Time, Address) and resolve ambiguities.
- **Seam**: Currently implemented as `StubFlyerNormalizationService`. Ready for Gemini Pro integration.

### 4. Candidate Creation & Gating
- **Logic**: If the `overall` confidence score is below 0.8 or if critical issues are detected (e.g., ambiguous address), the job status is set to `REQUIRES_REVIEW`.
- **Output**: A traceable `IngestionJobRecord` with full provenance.

## Evidence Chain & Provenance
Every flyer ingestion job preserves:
- **`assetId`**: Reference to the original image.
- **`ocrText`**: The raw text as seen by the OCR engine.
- **`confidenceVector`**: Detailed breakdown of certainty across extraction, temporal, venue, and geocode dimensions.
- **`processingMetadata`**: Versions of the OCR engine and LLM used.

## Manual Review Triggers
A job is flagged for manual review if:
- Overall confidence < 80%.
- Temporal data (Date/Time) is missing or conflicting.
- Venue cannot be matched with high certainty.
- The normalization service emits a `WARNING` or `ERROR` issue.

## Failure Modes
- **`PIPELINE_ERROR`**: General failure in OCR or Normalization services.
- **`VALIDATION_FAILED`**: Missing asset identifier in request.
- **`FAILED`**: Terminal failure during processing (e.g., unreadable image).

## Future Production Integration
To move to production:
1. Implement `IFlyerOcrService` using a production OCR provider.
2. Implement `IFlyerNormalizationService` using Gemini Pro with a structured prompt.
3. Replace the in-memory `IngestionJobRepository` with a persistent database.
