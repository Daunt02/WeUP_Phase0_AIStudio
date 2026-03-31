'use client';

import React, { useState, useMemo, useCallback } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import TopBar from '@/components/TopBar';
import RadarMap from '@/components/RadarMap';
import BottomNav from '@/components/BottomNav';
import CulturalCalendar from '@/components/CulturalCalendar';
import SocialSignalPanel from '@/components/SocialSignalPanel';
import SavedEvents from '@/components/SavedEvents';
import ProfilePanel from '@/components/ProfilePanel';
import TimelineControl from '@/components/TimelineControl';
import EventSignalModal from '@/components/EventSignalModal';
import GeoControls from '@/components/GeoControls';
import AddEventModal from '@/components/AddEventModal';
import { NightlifeItem, ViewMode, UIState, ModalState } from '@/types';
import { MOCK_EVENTS } from '@/constants/mockData';
import { eventService, BoundingBox } from '@/services/eventService';
import { AlertCircle } from 'lucide-react';

// Top-level Error Boundary
class GlobalErrorBoundary extends React.Component<{ children: React.ReactNode }, { hasError: boolean, error: any }> {
  constructor(props: { children: React.ReactNode }) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: any) {
    return { hasError: true, error };
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="fixed inset-0 bg-[#050505] flex items-center justify-center p-10 z-[999]">
          <div className="max-w-md w-full space-y-6 text-center">
            <div className="w-20 h-20 rounded-full bg-red-500/10 border border-red-500/20 flex items-center justify-center mx-auto">
              <AlertCircle className="w-10 h-10 text-red-500" />
            </div>
            <div className="space-y-2">
              <h1 className="text-2xl font-black uppercase italic tracking-tighter">System Error</h1>
              <p className="text-white/40 font-mono text-xs uppercase tracking-widest">The cultural radar has encountered an unrecoverable signal error.</p>
            </div>
            <div className="p-4 bg-white/5 border border-white/10 rounded-2xl font-mono text-[10px] text-red-400/80 break-all">
              {this.state.error?.message || 'Unknown Error'}
            </div>
            <button 
              onClick={() => window.location.reload()}
              className="w-full h-16 bg-white text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px]"
            >
              REBOOT SYSTEM
            </button>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}

/**
 * WEUP PHASE 0 - MOBILE PROTOTYPE ROUTING MAP
 * ------------------------------------------
 * 1. MAP DISCOVERY (Depth 0)
 *    - Tap Marker -> Open EventQuickView (Bottom Card)
 *    - Tap Map Canvas -> Close all overlays
 * 
 * 2. CALENDAR FLOW (Depth 1)
 *    - TopBar Calendar Icon -> Open CulturalCalendar (Modal)
 *    - Calendar Tap Event -> Open FlyerDetail (Modal)
 *    - Calendar Long Press -> Trigger Interest Flow (Map Animation + SocialSignalPanel)
 * 
 * 3. EVENT DETAIL (Depth 2)
 *    - EventQuickView "View Details" -> Open FlyerDetail (Modal)
 *    - FlyerDetail "Save" -> Add to SavedEvents
 *    - FlyerDetail "Navigate" -> External Navigation (Placeholder)
 * 
 * 4. SOCIAL INTERACTION (Depth 2)
 *    - Long Press Calendar -> Open SocialSignalPanel (Overlay)
 *    - SocialSignalPanel "Unlock" -> Progress through Tiers
 * 
 * 5. ADD EVENT FLOW (Depth 3)
 *    - BottomNav "+" Tap -> Open AddEventModal (Choice)
 *    - AddEventModal Upload -> OCR -> Location Confirmation (Ghost Marker)
 *    - AddEventModal Publish -> Live Signal on Map
 * 
 * 6. SAVED EVENTS (Side Panel)
 *    - TopBar Saved Icon -> Open SavedEvents (Slide-in)
 */

export default function WeUPApp() {
  const [uiState, setUiState] = useState<UIState>({
    activeMode: 'RADAR',
    selectedItemId: null,
    modalState: null,
    interestedEventId: null,
    socialPanelOpen: false,
    unlockedTiers: []
  });
  const [savedIds, setSavedIds] = useState<string[]>([]);
  const [currentTime, setCurrentTime] = useState('NOW');
  const [selectedDate, setSelectedDate] = useState('MAR 24');
  const [anchorPoint, setAnchorPoint] = useState<{ x: number, y: number } | null>(null);
  const [isGeoControlsOpen, setIsGeoControlsOpen] = useState(false);
  const [isTemporalModalOpen, setIsTemporalModalOpen] = useState(false);
  const [isQuickScrubbing, setIsQuickScrubbing] = useState(false);
  const [mapCenter, setMapCenter] = useState({ lat: 40.7128, lng: -74.0060 });
  const [visibleEvents, setVisibleEvents] = useState<NightlifeItem[]>(MOCK_EVENTS);
  const [discoveredEvents, setDiscoveredEvents] = useState<NightlifeItem[]>(MOCK_EVENTS);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const fetchTimeoutRef = React.useRef<NodeJS.Timeout | null>(null);

  const handleCenterChange = useCallback((center: { lat: number, lng: number }) => {
    setMapCenter(center);
  }, []);

  const activeMode = uiState.activeMode;
  const setActiveMode = (mode: ViewMode) => setUiState(prev => ({ ...prev, activeMode: mode }));
  const selectedEvent = discoveredEvents.find(e => e.id === uiState.selectedItemId) || null;

  const closeQuickView = () => {
    setUiState(prev => ({ ...prev, selectedItemId: null, modalState: null }));
  };

  const handleEventSelect = (event: NightlifeItem) => {
    setUiState(prev => ({ ...prev, selectedItemId: event.id, modalState: 'FULL' }));
  };

  const handleBottomNavAction = useCallback((action: 'WORLD_LONG' | 'TIME_TAP' | 'TIME_HOLD_START' | 'TIME_HOLD_END' | 'ADD' | 'ADD_LONG' | 'SAVED' | 'PROFILE') => {
    switch (action) {
      case 'WORLD_LONG':
        setIsGeoControlsOpen(true);
        break;
      case 'TIME_TAP':
        setIsTemporalModalOpen(true);
        break;
      case 'TIME_HOLD_START':
        setIsQuickScrubbing(true);
        break;
      case 'TIME_HOLD_END':
        setIsQuickScrubbing(false);
        break;
      case 'ADD':
        setUiState(prev => ({ ...prev, modalState: 'ADD_EVENT' }));
        break;
      case 'ADD_LONG':
        // Quick add options could be handled here or inside AddEventModal
        setUiState(prev => ({ ...prev, modalState: 'ADD_EVENT' }));
        break;
      case 'SAVED':
        setActiveMode('SAVED');
        break;
      case 'PROFILE':
        setActiveMode('PROFILE');
        break;
    }
  }, []);

  const handleBoundsChange = useCallback((bounds: BoundingBox) => {
    // Debounce the fetch to avoid overwhelming the service during rapid movement
    if (fetchTimeoutRef.current) clearTimeout(fetchTimeoutRef.current);
    
    fetchTimeoutRef.current = setTimeout(async () => {
      try {
        const events = await eventService.fetchEventsInBounds(bounds);
        setVisibleEvents(events);
        
        // Accumulate discovered events for the calendar/list views
        setDiscoveredEvents(prev => {
          const newEvents = events.filter(e => !prev.some(p => p.id === e.id));
          return [...prev, ...newEvents];
        });
      } catch (err) {
        console.error('Failed to fetch events for bounds:', err);
      }
    }, 200); // 200ms debounce
  }, []);

  const filteredEvents = useMemo(() => 
    visibleEvents.filter(event => {
      const eventDate = new Date(event.start_time);
      const eventDay = eventDate.getDate();
      const selectedDay = parseInt(selectedDate.split(' ')[1]);
      const matchesDate = eventDay === selectedDay;
      const matchesCategory = !selectedCategory || event.category?.toLowerCase() === selectedCategory;
      return matchesDate && matchesCategory;
    }),
    [selectedDate, visibleEvents, selectedCategory]
  );

  const mapEvents = useMemo(() => 
    visibleEvents.filter(event => {
      const eventDate = new Date(event.start_time);
      const eventDay = eventDate.getDate();
      const selectedDay = parseInt(selectedDate.split(' ')[1]);
      const matchesDate = eventDay === selectedDay;
      const matchesCategory = !selectedCategory || event.category?.toLowerCase() === selectedCategory;
      return matchesDate && matchesCategory;
    }),
    [visibleEvents, selectedCategory, selectedDate]
  );

  const handleDateSelect = (date: string) => {
    setSelectedDate(date);
    
    // Requirement 3: Zoom map to relevant events for this date
    const day = parseInt(date.split(' ')[1]);
    const dayEvents = discoveredEvents.filter(e => new Date(e.start_time).getDate() === day);
    
    if (dayEvents.length > 0) {
      // Find the first event to "interest" the map, or we could calculate a bounding box
      // For now, let's just pick the first one to trigger the existing flyTo logic
      setUiState(prev => ({
        ...prev,
        interestedEventId: dayEvents[0].id
      }));
    }
  };

  const savedEvents = useMemo(() => 
    discoveredEvents.filter(e => savedIds.includes(e.id)),
    [savedIds, discoveredEvents]
  );

  const handleViewFullDetails = (event: NightlifeItem) => {
    setUiState(prev => ({
      ...prev,
      selectedItemId: event.id,
      modalState: 'FULL'
    }));
  };

  const handleEventLongPress = (event: NightlifeItem) => {
    // 1. Save interest (mock)
    console.log(`Interest saved for event: ${event.id}`);
    
    // 2. Update UI State
    setUiState(prev => ({
      ...prev,
      interestedEventId: event.id,
      socialPanelOpen: true,
      unlockedTiers: [1], // Unlock tier 1 by default for interest
      activeMode: 'RADAR' // Ensure we are on map to see animation
    }));
  };

  const handleNavigate = (event: NightlifeItem) => {
    setUiState(prev => ({
      ...prev,
      activeMode: 'RADAR',
      selectedItemId: event.id,
      modalState: 'FULL',
      interestedEventId: event.id,
      socialPanelOpen: false
    }));
  };

  const toggleSave = (id: string) => {
    setSavedIds(prev => 
      prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
    );
  };

  const closeDetail = () => {
    setUiState(prev => ({ ...prev, selectedItemId: null, modalState: null }));
  };

  const handleGhostMove = useCallback((lat: number, lng: number) => {
    setUiState(prev => ({
      ...prev,
      ghostEvent: prev.ghostEvent ? { ...prev.ghostEvent, latitude: lat, longitude: lng } : null
    }));
  }, []);

  const handlePublishSignal = useCallback((event: NightlifeItem) => {
    setDiscoveredEvents(prev => [event, ...prev]);
    setVisibleEvents(prev => [event, ...prev]);
    
    // Auto-select and center on the new event
    setUiState(prev => ({
      ...prev,
      selectedItemId: event.id,
      interestedEventId: event.id,
      modalState: 'FULL',
      ghostEvent: null
    }));
  }, []);

  const handleGhostUpdate = useCallback((ghost: Partial<NightlifeItem> | null) => {
    setUiState(prev => ({ ...prev, ghostEvent: ghost }));
  }, []);

  return (
    <GlobalErrorBoundary>
      <main className="fixed inset-0 bg-[#050505] text-white overflow-hidden font-sans selection:bg-brand-primary selection:text-black">
      {/* Zone 1: World Layer (Depth 0) - z-index 1 */}
      <div className={`fixed inset-0 transition-all duration-1000 z-[1] ${['SAVED', 'PROFILE'].includes(activeMode) || uiState.modalState === 'FULL' ? 'blur-md scale-105 opacity-60' : 'blur-0 scale-100 opacity-100'}`}>
        <RadarMap 
          events={mapEvents} 
          onEventSelect={handleEventSelect}
          onBoundsChange={handleBoundsChange}
          onCenterChange={handleCenterChange}
          onAnchorChange={setAnchorPoint}
          selectedEventId={uiState.selectedItemId}
          interestedEventId={uiState.interestedEventId}
          selectedDate={selectedDate}
          activeMode={activeMode}
          ghostEvent={uiState.ghostEvent}
          onGhostMove={handleGhostMove}
        />
      </div>

      {/* Zone 2: Control Layer (Depth 1) - z-index 100-299 */}
      <div className="fixed inset-0 z-[100] pointer-events-none">
        <TopBar />
      </div>

      <AnimatePresence>
        {['RADAR', 'SAVED', 'PROFILE'].includes(activeMode) && (
          <div className="fixed inset-x-0 bottom-0 z-[200] pointer-events-none flex flex-col items-center">
            {/* Quick Time Scrubber - Only during hold */}
            <AnimatePresence>
              {isQuickScrubbing && (
                <motion.div
                  initial={{ opacity: 0, scale: 0.9, y: 20 }}
                  animate={{ opacity: 1, scale: 1, y: 0 }}
                  exit={{ opacity: 0, scale: 0.9, y: 20 }}
                  className="mb-12 pointer-events-auto"
                >
                  <TimelineControl onTimeChange={setCurrentTime} />
                </motion.div>
              )}
            </AnimatePresence>

            {/* Primary Navigation - Centered CTA */}
            <motion.div
              initial={{ opacity: 0, y: 50 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: 50 }}
              className="w-full pointer-events-auto"
            >
              <BottomNav 
                activeMode={activeMode} 
                onModeChange={setActiveMode}
                onAction={handleBottomNavAction}
              />
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* Temporal Modal (Calendar + Scrubber) */}
      <CulturalCalendar 
        isVisible={isTemporalModalOpen} 
        events={discoveredEvents}
        selectedDate={selectedDate}
        onDateSelect={handleDateSelect}
        onEventSelect={handleViewFullDetails}
        onClose={() => setIsTemporalModalOpen(false)}
      />

      {/* Geo Controls Modal (Long Press WORLD) */}
      <GeoControls 
        isVisible={isGeoControlsOpen}
        onClose={() => setIsGeoControlsOpen(false)}
      />

      {/* Zone 3: Focus Layer (Depth 2) - z-index 300+ */}
      <SocialSignalPanel 
        isOpen={uiState.socialPanelOpen}
        event={discoveredEvents.find(e => e.id === uiState.interestedEventId) || null}
        unlockedTiers={uiState.unlockedTiers || []}
        onClose={() => setUiState(prev => ({ ...prev, socialPanelOpen: false }))}
      />

      <SavedEvents 
        isVisible={activeMode === 'SAVED'}
        onClose={() => setActiveMode('RADAR')}
        savedEvents={savedEvents}
      />

      <ProfilePanel 
        isVisible={activeMode === 'PROFILE'}
        onClose={() => setActiveMode('RADAR')}
      />

      <EventSignalModal 
        event={selectedEvent}
        state={uiState.modalState}
        onClose={closeDetail}
        onSave={toggleSave}
        isSaved={selectedEvent ? savedIds.includes(selectedEvent.id) : false}
        anchorPoint={anchorPoint}
      />

      <AddEventModal 
        isVisible={uiState.modalState === 'ADD_EVENT'}
        onClose={() => setUiState(prev => ({ ...prev, modalState: null, ghostEvent: null }))}
        onPublish={handlePublishSignal}
        onGhostUpdate={handleGhostUpdate}
        ghostEvent={uiState.ghostEvent}
        mapCenter={mapCenter}
      />

      {/* Depth Vignette */}
      <div className="pointer-events-none fixed inset-0 z-[50] shadow-[inset_0_0_150px_rgba(0,0,0,0.8)]" />
    </main>
    </GlobalErrorBoundary>
  );
}
