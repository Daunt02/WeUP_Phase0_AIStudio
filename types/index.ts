// ViewMode is owned by types/ui.ts (world-surface coordinator) — re-exported here
// for backward-compatible component imports.
export type { ViewMode } from '@/types/ui';

export interface NightlifeItem {
  id: string;
  title: string;
  description: string;
  venue_name: string;
  address: string;
  latitude: number;
  longitude: number;
  start_time: string;
  end_time: string;
  category: 'nightlife' | 'lounge' | 'concert' | 'private' | 'restaurant' | 'rooftop' | 'startup' | string;
  price_tier: '$' | '$$' | '$$$' | '$$$$' | 'FREE' | string;
  source: 'manual' | 'scraped' | 'api';
  image_url: string;
  status?: 'DRAFT' | 'NEEDS_REVIEW' | 'PUBLISHED' | 'ARCHIVED';
  confidence?: number; // 0 to 1
  
  // Compatibility fields for existing UI
  neighborhood?: string;
  energyLevel?: number;
  tags?: string[];
  
  // Derived/Legacy fields for RadarMap compatibility if needed
  coordinates?: {
    lat: number;
    lng: number;
  };
}

export interface InterestRecord {
  id: string;
  user_id: string;
  event_id: string;
  timestamp: string;
  source: 'calendar_long_press' | 'map_click' | 'detail_view';
  interest_strength: number;
  social_unlock_state: 'pending' | 'unlocked' | 'hidden';
  geo_context: string;
}

export interface SocialInviteTier {
  id: string;
  event_id: string;
  tier_level: 1 | 2 | 3;
  title: string;
  visibility: 'visible' | 'hidden';
  unlock_status: 'locked' | 'unlocked';
  trust_requirement: number;
  proximity_radius_meters: number;
  invite_type: string;
}

/**
 * EventSignalState — display state prop for EventSignalModal.
 * This is a presentational signal, NOT the coordinator's modal stack.
 * The coordinator's ModalState lives in types/ui.ts.
 */
export type EventSignalState = 'FULL' | 'ADD_EVENT' | null;

/** @deprecated Use EventSignalState. */
export type ModalState = EventSignalState;
