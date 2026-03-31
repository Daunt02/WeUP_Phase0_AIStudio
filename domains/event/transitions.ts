/**
 * WeUP Phase 0 — Event Lifecycle Transitions
 *
 * Legal transition table and guard functions.
 * Illegal transitions throw or return an error result — never silently succeed.
 */

import { EventStatus, EventAggregate, REQUIRED_FIELDS_BY_STATUS } from './types';

// ---------------------------------------------------------------------------
// Legal transition table
// ---------------------------------------------------------------------------

const LEGAL_TRANSITIONS: Record<EventStatus, EventStatus[]> = {
  DRAFT:        ['INGESTED', 'REJECTED'],
  INGESTED:     ['NEEDS_REVIEW', 'APPROVED', 'REJECTED'],
  NEEDS_REVIEW: ['APPROVED', 'REJECTED'],
  APPROVED:     ['PUBLISHED', 'NEEDS_REVIEW', 'REJECTED'],
  PUBLISHED:    ['ARCHIVED', 'NEEDS_REVIEW'],
  REJECTED:     ['DRAFT'],               // allow re-submission via reset to DRAFT
  ARCHIVED:     [],                      // terminal — no automatic egress
};

// ---------------------------------------------------------------------------
// Transition guard
// ---------------------------------------------------------------------------

export interface TransitionResult {
  ok: boolean;
  reason?: string;
}

/** Returns whether the transition from → to is legal. */
export function canTransitionEventStatus(from: EventStatus, to: EventStatus): TransitionResult {
  const allowed = LEGAL_TRANSITIONS[from];
  if (!allowed.includes(to)) {
    return {
      ok: false,
      reason: `Illegal transition: ${from} → ${to}. Allowed from ${from}: [${allowed.join(', ') || 'none'}]`,
    };
  }
  return { ok: true };
}

/** Applies a status transition, throwing on illegal moves. */
export function transitionEventStatus(event: EventAggregate, to: EventStatus): EventAggregate {
  const result = canTransitionEventStatus(event.status, to);
  if (!result.ok) throw new Error(result.reason);
  return { ...event, status: to, audit: { ...event.audit, updatedAt: new Date().toISOString() } };
}

// ---------------------------------------------------------------------------
// Field-completeness guard
// ---------------------------------------------------------------------------

/** Returns any required fields that are missing / falsy for the given status. */
export function getMissingFieldsForStatus(
  event: Partial<EventAggregate>,
  status: EventStatus,
): string[] {
  const required = REQUIRED_FIELDS_BY_STATUS[status];
  return required.filter(field => {
    const val = (event as Record<string, unknown>)[field];
    if (val === null || val === undefined) return true;
    if (typeof val === 'string' && val.trim() === '') return true;
    if (Array.isArray(val) && val.length === 0 && field === 'sourceRefs') return true;
    return false;
  });
}

/** True if the event has all required fields for the target status. */
export function hasRequiredMetadataForStatus(
  event: Partial<EventAggregate>,
  status: EventStatus,
): boolean {
  return getMissingFieldsForStatus(event, status).length === 0;
}

// ---------------------------------------------------------------------------
// Publish eligibility
// ---------------------------------------------------------------------------

/** Minimum confidence threshold for auto-approval without manual review. */
export const AUTO_APPROVE_CONFIDENCE_THRESHOLD = 0.85;

export function isPublishableEvent(event: Partial<EventAggregate>): boolean {
  if (!hasRequiredMetadataForStatus(event, 'PUBLISHED')) return false;
  if ((event.confidence ?? 0) < AUTO_APPROVE_CONFIDENCE_THRESHOLD) return false;
  return true;
}
