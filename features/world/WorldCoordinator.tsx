"use client";
import React, { useEffect, useState } from 'react';
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
import { NightlifeItem } from '@/types';
import { eventService } from '@/services/eventService';
import { useWorldSurfaceState } from '@/hooks/useWorldSurfaceState';

export default function WorldCoordinator() {
  const { state, selectEvent, interestEvent, openModal, closeModal, setMapCenter, setMapBounds, beginDraft, updateDraft, publishDraft, toggleSave } = useWorldSurfaceState();

  const [events, setEvents] = useState<NightlifeItem[]>([]);
  const [loading, setLoading] = useState(false);

  // Fetch events when bounds change
  useEffect(() => {
    let cancelled = false;
    async function load() {
      if (!state.mapBounds) return;
      setLoading(true);
      try {
        const b = state.mapBounds;
        const result = await eventService.fetchEventsInBounds({ minLat: b.minLat, maxLat: b.maxLat, minLng: b.minLng, maxLng: b.maxLng });
        if (!cancelled) setEvents(result);
      } catch (err) {
        console.error('Error fetching events', err);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    load();
    return () => { cancelled = true; };
  }, [state.mapBounds]);

  // Handlers wired to coordinator actions
  const handleEventSelect = (e: NightlifeItem) => {
    selectEvent(e.id);
    openModal('EVENT_DETAIL');
  };

  const handleGhostUpdate = (patch: Partial<NightlifeItem> | null) => {
    if (patch) beginDraft(patch as any);
    else updateDraft({} as any);
  };

  const handlePublish = (event: NightlifeItem) => {
    // Hook currently clears draft and toggles save locally; real persistence is for P09
    publishDraft(event.id);
    // keep UI consistent by adding to events list locally
    setEvents(prev => [event, ...prev]);
  };

  return (
    <div className="w-full h-full relative">
      <TopBar />
      <RadarMap
        events={events}
        onEventSelect={handleEventSelect}
        onBoundsChange={(bounds) => setMapBounds(bounds)}
        onCenterChange={(c) => setMapCenter({ lat: c.lat, lng: c.lng })}
        onAnchorChange={() => {}}
        selectedEventId={state.selectedEventId || undefined}
        interestedEventId={state.interestedEventId || undefined}
        ghostEvent={state.ghostDraft as any}
        onGhostMove={(lat, lng) => updateDraft({ latitude: lat, longitude: lng } as any)}
        selectedDate={state.selectedDate}
        activeMode={state.viewMode}
      />

      <TimelineControl />
      <BottomNav />

      <AddEventModal
        isVisible={state.modal.kind === 'STACK' && state.modal.stack[state.modal.stack.length - 1] === 'ADD_EVENT'}
        onClose={() => closeModal()}
        onPublish={handlePublish}
        onGhostUpdate={handleGhostUpdate}
        ghostEvent={state.ghostDraft as any}
        mapCenter={state.mapCenter}
      />

      {/* Keep other panels rendered for layout; they should be controlled via state in future iterations */}
      <CulturalCalendar />
      <SocialSignalPanel />
      <SavedEvents />
      <ProfilePanel />
      <EventSignalModal />
      <GeoControls />
    </div>
  );
}
"use client";

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

// Top-level Error Boundary kept local to the runtime spine
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

export default function WorldCoordinator() {
  // NOTE: This component intentionally mirrors the existing prototype's runtime
  // responsibilities but centralizes orchestration here so the page remains thin.
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
    if (fetchTimeoutRef.current) clearTimeout(fetchTimeoutRef.current);
    fetchTimeoutRef.current = setTimeout(async () => {
      try {
        const events = await eventService.fetchEventsInBounds(bounds);
        setVisibleEvents(events);
        setDiscoveredEvents(prev => {
          const newEvents = events.filter(e => !prev.some(p => p.id === e.id));
          return [...prev, ...newEvents];
        });
      } catch (err) {
        console.error('Failed to fetch events for bounds:', err);
      }
    }, 200);
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
  ,);

  const mapEvents = filteredEvents;

  const handleDateSelect = (date: string) => {
    setSelectedDate(date);
    const day = parseInt(date.split(' ')[1]);
    const dayEvents = discoveredEvents.filter(e => new Date(e.start_time).getDate() === day);
    if (dayEvents.length > 0) {
      setUiState(prev => ({
        ...prev,
        interestedEventId: dayEvents[0].id
      }));
    }
  };

  const savedEvents = useMemo(() => discoveredEvents.filter(e => savedIds.includes(e.id)), [savedIds, discoveredEvents]);

  const handleViewFullDetails = (event: NightlifeItem) => {
    setUiState(prev => ({
      ...prev,
      selectedItemId: event.id,
      modalState: 'FULL'
    }));
  };

  const handleEventLongPress = (event: NightlifeItem) => {
    console.log(`Interest saved for event: ${event.id}`);
    setUiState(prev => ({
      ...prev,
      interestedEventId: event.id,
      socialPanelOpen: true,
      unlockedTiers: [1],
      activeMode: 'RADAR'
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
    setSavedIds(prev => prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]);
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

        <div className="fixed inset-0 z-[100] pointer-events-none">
          <TopBar />
        </div>

        <AnimatePresence>
          {['RADAR', 'SAVED', 'PROFILE'].includes(activeMode) && (
            <div className="fixed inset-x-0 bottom-0 z-[200] pointer-events-none flex flex-col items-center">
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

        <CulturalCalendar 
          isVisible={isTemporalModalOpen} 
          events={discoveredEvents}
          selectedDate={selectedDate}
          onDateSelect={handleDateSelect}
          onEventSelect={handleViewFullDetails}
          onClose={() => setIsTemporalModalOpen(false)}
        />

        <GeoControls 
          isVisible={isGeoControlsOpen}
          onClose={() => setIsGeoControlsOpen(false)}
        />

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

        <div className="pointer-events-none fixed inset-0 z-[50] shadow-[inset_0_0_150px_rgba(0,0,0,0.8)]" />
      </main>
    </GlobalErrorBoundary>
  );
}
