export type ViewMode = 'RADAR' | 'CALENDAR' | 'DETAIL' | 'UPLOAD' | 'NORMALIZE' | 'SAVED' | 'PROFILE';

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

export type ModalState = 'FULL' | 'ADD_EVENT' | null;

export interface UIState {
  activeMode: ViewMode;
  selectedItemId: string | null;
  modalState: ModalState;
  interestedEventId: string | null;
  socialPanelOpen: boolean;
  unlockedTiers: number[];
  ghostEvent?: Partial<NightlifeItem> | null;
}
