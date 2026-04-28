using System;

namespace WeUP.Contracts.Ingestion;

/// <summary>
/// One piece of evidence attached to a candidate during the ingestion pipeline.
/// Immutable and append-only.
/// </summary>
public sealed record EvidenceRecord(
    Guid CandidateId,
    string Source,
    string RawMatchedText,
    float LocalConfidence);
