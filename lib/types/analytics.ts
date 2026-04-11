export type AnalyticsEventName =
  | "map_feed_viewed"
  | "event_detail_opened"
  | "signal_saved"
  | "signal_unsaved"
  | "frontend_exception"
  | "calendar_opened"
  | "temporal_preset_selected"
  | "submission_draft_created"
  | "submission_sent_for_review"
  | "moderation_item_resolved";

export interface AnalyticsEvent {
  name: AnalyticsEventName;
  // Keep payload minimal and avoid PII
  props?: Record<string, string | number | boolean>;
  userIdHash?: string; // hashed/anonymized id if needed
  timestamp?: string;
}
