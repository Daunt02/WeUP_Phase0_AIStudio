using System;

namespace WeUP.Contracts.Resolution;

/// <summary>
/// One immutable provenance record for a single field change during a merge.
/// </summary>
public sealed record ProvenanceEntry(
    Guid Id,
    Guid EventId,                 // Canonical event being modified
    Guid CandidateId,             // Source of the new data
    string FieldName,             // e.g., "Title", "StartUtc", "VenueName"
    string OldValue,
    string NewValue,
    DateTimeOffset ChangedAtUtc,
    string ChangedBy,              // Typically the system user “MergeEngine”
    string Reason);               // Human‑readable justification (e.g., "Auto‑merge high similarity")
