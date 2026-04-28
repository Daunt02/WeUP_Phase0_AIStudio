// ------------------------------------------------------------
// File: WeUP.Contracts/Ingestion/CandidateEvent.cs
// ------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace WeUP.Contracts.Ingestion;

/// <summary>
/// Intermediate representation produced by the Normalization Engine.
/// This object will later be fed to deduplication and moderation pipelines.
/// </summary>
public sealed record CandidateEvent(
    Guid CandidateId,
    Guid RequestId,
    string? Title,
    DateTimeOffset? InferredStartUtc,
    string? RawLocationText,
    float OverallExtractionConfidence,
    IReadOnlyDictionary<string, string> RawFields);   // key = field name, value = raw extracted string
