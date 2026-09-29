export type ViewMode = 'DISCOVER' | 'ACTIVITY' | 'SAVED' | 'PROFILE' | 'CREATE';

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
  spatial_label?: string;
  corridor?: string;
  density_score?: number;
  influence_radius?: number;
  temporal_weight?: number;
  phase?: 'upcoming' | 'active' | 'ending' | 'ended';
  cell_id?: string;
  cell_type?: 'cluster' | 'edge' | 'isolated';
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

export type ModalState = 'FULL' | 'ADD_EVENT' | 'ONBOARDING' | null;

export interface OnboardingStep {
  id: number;
  title: string;
  content: string;
}

export interface UIState {
  activeMode: ViewMode;
  selectedItemId: string | null;
  modalState: ModalState;
  onboardingStep: number;
  interestedEventId: string | null;
  socialPanelOpen: boolean;
  unlockedTiers: number[];
  ghostEvent: Partial<NightlifeItem> | null;
  filters: {
    categories: string[];
    timeframe: 'now' | 'tonight' | 'tomorrow' | 'weekend';
    radius: number;
  };
}

export interface WLLSConfig {
  color: string;
  motion: string;
  intensity: string;
  rhythm: string;
}

export interface MapMarkerPayload {
  label: string;
  district: string;
  state: 'upcoming' | 'live' | 'ended';
  confidence: number;
  wlls: WLLSConfig;
}

export interface EventCardUI {
  title: string;
  time: string;
  location: string;
  badges: string[];
}

export interface ProfileState {
  city: string;
  saved_count: number;
  folders: Array<{ name: string; count: number }>;
}

export interface SaveActionResponse {
  event_id: string;
  folder: string;
  state: 'saved' | 'unsaved';
  timestamp: string;
}

export interface AggregatedEventPayload {
  event: {
    id: string;
    title: string;
    district: string;
    time: string;
    density: number;
    temporal: number;
    confidence: number;
    wlls: WLLSConfig;
    saved: boolean;
  };
}

export interface BoundingBox {
  minLat: number;
  maxLat: number;
  minLng: number;
  maxLng: number;
}

