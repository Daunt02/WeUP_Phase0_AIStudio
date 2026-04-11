/**
 * WeUP Phase 0 — Canonical Event Aggregate
 *
 * This file defines the production-grade event domain model.
 * It separates canonical identity, source, venue, location, temporal,
 * media, moderation, and audit concerns from UI/display projections.
 *
 * The companion file `transitions.ts` encodes lifecycle guards.
 * The companion file `projections.ts` encodes UI-facing view models.
 */

// ---------------------------------------------------------------------------
// Identity
// ---------------------------------------------------------------------------

/** Opaque string ID for events. */
export type EventId = string;

/** Opaque string ID for venues. */
export type VenueId = string;

// ---------------------------------------------------------------------------
// Lifecycle / Status
// ---------------------------------------------------------------------------

/**
 * EventStatus lifecycle:
 *
 *  DRAFT ──► INGESTED ──► NEEDS_REVIEW ──► APPROVED ──► PUBLISHED
 *                                │                         │
 *                                ▼                         ▼
 *                             REJECTED                 ARCHIVED
 *
 * See transitions.ts for legal transition table.
 */
export type EventStatus =
  | "DRAFT"
  | "INGESTED"
  | "NEEDS_REVIEW"
  | "APPROVED"
  | "PUBLISHED"
  | "REJECTED"
  | "ARCHIVED";

// ---------------------------------------------------------------------------
// Category
// ---------------------------------------------------------------------------

export type EventCategory =
  | "nightlife"
  | "lounge"
  | "concert"
  | "private"
  | "restaurant"
  | "rooftop"
  | "startup"
  | "tech"
  | "creator"
  | "career"
  | "other";

// ---------------------------------------------------------------------------
// Geospatial
// ---------------------------------------------------------------------------

export interface GeoPoint {
  lat: number;
  lng: number;
}

export interface AddressSnapshot {
  line1: string;
  city: string;
  state?: string;
  postalCode?: string;
  country: string;
  /** Raw string as entered / extracted — preserved for audit. */
  raw: string;
}

// ---------------------------------------------------------------------------
// Temporal
// ---------------------------------------------------------------------------

export interface EventTimeRange {
  /** ISO 8601 UTC */
  startUtc: string;
  /** ISO 8601 UTC — may be null for open-ended events */
  endUtc: string | null;
}

// ---------------------------------------------------------------------------
// Source provenance (abbreviated — full model in provenance.ts)
// ---------------------------------------------------------------------------

export type SourceKind =
  | "manual_submission"
  | "flyer_upload"
  | "pasted_url"
  | "scraped_venue_page"
  | "external_feed";

export interface SourceRef {
  kind: SourceKind;
  /** Unique reference for traceability — submission id, job id, URL, etc. */
  ref: string;
  ingestedAt: string; // ISO 8601 UTC
}

// ---------------------------------------------------------------------------
// Media reference
// ---------------------------------------------------------------------------

export interface MediaRef {
  assetId: string;
  url: string;
  kind: "image" | "video" | "poster";
  /** Width in px, if known. */
  width?: number;
  /** Height in px, if known. */
  height?: number;
}

// ---------------------------------------------------------------------------
// Audit
// ---------------------------------------------------------------------------

export interface AuditMetadata {
  createdAt: string; // ISO 8601 UTC
  updatedAt: string; // ISO 8601 UTC
  createdBy?: string; // user id or system
}

// ---------------------------------------------------------------------------
// Review (slim aggregate field — full model with evidence refs in provenance.ts)
// ---------------------------------------------------------------------------

export interface ReviewMetadata {
  requiredReason?: string;
  reviewedBy?: string;
  reviewedAt?: string;
  notes?: string;
  rejectionReason?: string;
  /** Whether this event was auto-approved or required manual review. */
  publishDecision?: "auto_approved" | "manually_approved" | "rejected";
}

// ---------------------------------------------------------------------------
// Venue reference
// ---------------------------------------------------------------------------

export interface VenueSnapshot {
  /** Nullable until resolved — some ingested events may only have a name. */
  venueId: VenueId | null;
  name: string;
}

// ---------------------------------------------------------------------------
// Canonical Event Aggregate
// ---------------------------------------------------------------------------

/**
 * EventAggregate is the source of truth for a WeUP event.
 * UI components must NOT depend on this type directly — use projections instead.
 */
export interface EventAggregate {
  // Identity
  id: EventId;

  // Status
  status: EventStatus;

  // Canonical content
  canonicalTitle: string;
  canonicalDescription: string | null;

  // Category
  category: EventCategory;

  // Venue
  venue: VenueSnapshot;

  // Location
  address: AddressSnapshot;
  geo: GeoPoint;

  // Temporal
  timeRange: EventTimeRange;
  /** IANA timezone, e.g. "America/Chicago" */
  timezone: string;

  // Source traceability
  sourceRefs: SourceRef[];

  // Media
  mediaRefs: MediaRef[];

  // Taxonomy
  tags: string[];

  // Confidence (0–1 scalar; full vector lives in provenance.ts)
  confidence: number;

  // Moderation
  review: ReviewMetadata;

  // Audit
  audit: AuditMetadata;
}

// ---------------------------------------------------------------------------
// Field-requirements by status
// ---------------------------------------------------------------------------

/**
 * Which fields must be non-null / non-empty at each lifecycle stage.
 * Used by transition guards in transitions.ts.
 */
export const REQUIRED_FIELDS_BY_STATUS: Record<
  EventStatus,
  (keyof EventAggregate)[]
> = {
  DRAFT: ["id", "status", "audit"],
  INGESTED: ["id", "status", "canonicalTitle", "sourceRefs", "audit"],
  NEEDS_REVIEW: [
    "id",
    "status",
    "canonicalTitle",
    "sourceRefs",
    "confidence",
    "audit",
  ],
  APPROVED: [
    "id",
    "status",
    "canonicalTitle",
    "venue",
    "address",
    "geo",
    "timeRange",
    "timezone",
    "sourceRefs",
    "confidence",
    "audit",
  ],
  PUBLISHED: [
    "id",
    "status",
    "canonicalTitle",
    "venue",
    "address",
    "geo",
    "timeRange",
    "timezone",
    "sourceRefs",
    "confidence",
    "audit",
  ],
  REJECTED: ["id", "status", "audit"],
  ARCHIVED: ["id", "status", "audit"],
};

// ---------------------------------------------------------------------------
// Source / Normalized / Derived separations
// ---------------------------------------------------------------------------

/**
 * Raw source payload as received from an ingestion pipeline or client.
 * Keep raw fields permissive — parsing/normalization produces the canonical aggregate.
 */
export interface EventSourcePayload {
  // Raw identifiers from source(s)
  sourceId?: string;
  sourceKind?: SourceKind;

  // Raw textual fields (may be messy / OCRed)
  rawTitle?: string | null;
  rawDescription?: string | null;
  rawVenueName?: string | null;
  rawAddress?: string | null; // free-form address string

  // Raw temporal strings as provided by source (may be ambiguous)
  rawStart?: string | null;
  rawEnd?: string | null;

  // Raw geolocation (strings/numbers depending on source)
  rawLat?: number | string | null;
  rawLng?: number | string | null;

  // Raw media / asset refs
  rawMediaUrls?: string[];

  // Any additional source payload preserved for audit
  payload?: Record<string, unknown> | null;
}

/**
 * Normalized event fields — the inputs used to construct `EventAggregate`.
 * These fields are validated, normalized, and type-safe.
 */
export interface EventNormalized {
  id: EventId;
  canonicalTitle: string;
  canonicalDescription?: string | null;
  category?: EventCategory;
  venue?: VenueSnapshot | null;
  address?: AddressSnapshot | null;
  geo?: GeoPoint | null;
  timeRange?: EventTimeRange | null;
  timezone?: string | null;
  mediaRefs?: MediaRef[];
  tags?: string[];
}

/**
 * Derived / display-only fields used by UI projections. These MUST NOT be
 * persisted back to canonical store as authoritative data.
 */
export interface EventDisplayFields {
  // Human-friendly renderable strings
  displayTitle?: string;
  displaySubtitle?: string; // e.g., venue + neighborhood
  displayDate?: string; // locale-aware
  displayTime?: string; // locale-aware
  imageUrl?: string | null;
  // Short summary computed from description
  teaser?: string | null;
}
