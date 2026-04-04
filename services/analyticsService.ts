/**
 * Analytics Service — P23
 * Records product analytics events with PII guards
 */

import { getAuthHeader } from './auth';

export interface RecordAnalyticsRequest {
  eventType: string;
  userId?: string;
  properties?: Record<string, any>;
}

export interface EventTypeDto {
  value: number;
  name: string;
}

export interface EventTypeListResponse {
  eventTypes: EventTypeDto[];
}

/**
 * Analytics event types (matches backend AnalyticsEventType enum)
 */
export enum AnalyticsEventType {
  MapViewed = 'MapViewed',
  EventSaved = 'EventSaved',
  EventUnsaved = 'EventUnsaved',
  EventSubmitted = 'EventSubmitted',
  EventDetailViewed = 'EventDetailViewed',
  TemporalPresetSelected = 'TemporalPresetSelected',
  DistrictFilterApplied = 'DistrictFilterApplied',
  ErrorOccurred = 'ErrorOccurred',
}

/**
 * Record an analytics event
 * Note: Do NOT include PII (email, phone, location) in properties
 */
export async function recordEvent(
  eventType: AnalyticsEventType | string,
  userId?: string,
  properties?: Record<string, any>,
  signal?: AbortSignal
): Promise<void> {
  const request: RecordAnalyticsRequest = {
    eventType,
    userId,
    properties,
  };

  const response = await fetch('/api/analytics/events', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...getAuthHeader(),
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    console.warn(`Analytics event failed (${response.status}): ${response.statusText}`);
    // Don't throw — analytics failures shouldn't break user workflows
  }
}

/**
 * List all available analytics event types
 */
export async function getEventTypes(
  signal?: AbortSignal
): Promise<EventTypeListResponse> {
  const response = await fetch('/api/analytics/event-types', {
    method: 'GET',
    headers: getAuthHeader(),
    signal,
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch event types: ${response.statusText}`);
  }

  return response.json();
}

/**
 * Helper to record event with custom properties
 */
export async function recordEventWithProperties(
  eventType: AnalyticsEventType | string,
  properties: Record<string, any> = {},
  userId?: string
): Promise<void> {
  return recordEvent(eventType, userId, properties);
}

/**
 * Helper to record simple event (no properties)
 */
export async function recordSimpleEvent(
  eventType: AnalyticsEventType | string,
  userId?: string
): Promise<void> {
  return recordEvent(eventType, userId, {});
}
