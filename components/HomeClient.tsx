'use client';

import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { MapPin, X, Navigation } from 'lucide-react';
import { NightlifeItem, ViewMode, UIState } from '@/types';
import { MOCK_EVENTS } from '@/constants/mockData';
import { formatHoustonDateLabel, getHoustonDate } from '@/utils/dateUtils';

// Layers
import WorldLayer from '@/components/WorldLayer';
import InteractionLayer from '@/components/InteractionLayer';
import OverlayLayer from '@/components/OverlayLayer';
import ZoneDrawer from '@/components/ZoneDrawer';

export default function HomeClient() {
  // --- UI State Machine ---
  const [uiState, setUiState] = useState<UIState>({
    activeMode: 'DISCOVER',
    selectedItemId: null,
    modalState: 'ONBOARDING',
    onboardingStep: 0,
    interestedEventId: null,
    socialPanelOpen: false,
    unlockedTiers: [1],
    ghostEvent: null,
    filters: {
      categories: [],
      timeframe: 'tonight',
      radius: 5,
    }
  });

  const [depth, setDepth] = useState(0);
  const [selectedClusterEvents, setSelectedClusterEvents] = useState<NightlifeItem[] | null>(null);
  const [discoveredEvents, setDiscoveredEvents] = useState<NightlifeItem[]>(MOCK_EVENTS);
  const [savedIds, setSavedIds] = useState<string[]>([]);
  const [highlightedIds, setHighlightedIds] = useState<string[]>([]);
  const [searchQuery, setSearchQuery] = useState<string>('');
  
  const [selectedDate, setSelectedDate] = useState<string>(() => formatHoustonDateLabel(getHoustonDate()));
  const [selectedTime, setSelectedTime] = useState<string>("NOW");
  
  const [isTemporalModalOpen, setIsTemporalModalOpen] = useState(false);
  const [isGeoControlsOpen, setIsGeoControlsOpen] = useState(false);
  const [isDiscovering, setIsDiscovering] = useState(false);
  const [isAssistantOpen, setIsAssistantOpen] = useState(false);
  
  const [mapCenter, setMapCenter] = useState({ lat: 29.7604, lng: -95.3698 }); // Houston
  const [anchorPoint, setAnchorPoint] = useState<{ x: number, y: number } | null>(null);

  const [isLoading, setIsLoading] = useState(true);
  const [locationPermission, setLocationPermission] = useState<'prompt' | 'granted' | 'denied'>('prompt');
  const [isWarningDismissed, setIsWarningDismissed] = useState(false);

  // --- Geolocation ---
  useEffect(() => {
    if ('geolocation' in navigator) {
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          setMapCenter({ lat: pos.coords.latitude, lng: pos.coords.longitude });
          setLocationPermission('granted');
          setIsLoading(false);
        },
        () => {
          setLocationPermission('denied');
          setIsLoading(false);
        },
        { timeout: 10000 }
      );
    } else {
      setIsLoading(false);
    }
  }, []);

  const handleRecenter = useCallback(() => {
    if ('geolocation' in navigator) {
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          setMapCenter({ lat: pos.coords.latitude, lng: pos.coords.longitude });
        },
        () => {
          // If denied or fails, fallback to centering on active Houston cluster
          setMapCenter({ lat: 29.7604, lng: -95.3698 });
        }
      );
    } else {
      setMapCenter({ lat: 29.7604, lng: -95.3698 });
    }
  }, []);

  // --- Derived State & Dynamic Filtering ---
  const filteredEvents = useMemo(() => {
    let events = discoveredEvents;
    
    // Category Filtering
    if (uiState.filters.categories.length > 0) {
      events = events.filter(e => 
        uiState.filters.categories.some(cat => 
          e.category?.toLowerCase() === cat.toLowerCase() ||
          (cat.toLowerCase() === 'startup' && e.category?.toLowerCase() === 'tech')
        )
      );
    }

    // Signals Only Filter
    if (uiState.filters.signalsOnly) {
      events = events.filter(e => (e.confidence ?? 1) >= 0.8 && e.status === 'PUBLISHED');
    }

    // Search Query Filtering
    if (searchQuery.trim().length > 0) {
      const q = searchQuery.toLowerCase().trim();
      events = events.filter(e => 
        e.title?.toLowerCase().includes(q) ||
        e.venue_name?.toLowerCase().includes(q) ||
        e.address?.toLowerCase().includes(q) ||
        e.description?.toLowerCase().includes(q) ||
        e.neighborhood?.toLowerCase().includes(q) ||
        e.tags?.some(t => t.toLowerCase().includes(q))
      );
    }

    return events;
  }, [discoveredEvents, uiState.filters, searchQuery]);

  // --- Temporal Scrubbing Highlight Effect ---
  useEffect(() => {
    if (!selectedTime) return;
    const timeUpper = selectedTime.toUpperCase();
    
    const matching = discoveredEvents.filter(e => {
      if (timeUpper === 'NOW') return true;
      if (timeUpper === '6PM') return e.category === 'restaurant' || e.category === 'lounge' || e.start_time?.includes('18:') || e.start_time?.includes('19:');
      if (timeUpper === '9PM') return e.category === 'concert' || e.category === 'nightlife' || e.start_time?.includes('20:') || e.start_time?.includes('21:');
      if (timeUpper === 'MIDNIGHT') return (e.energyLevel && e.energyLevel >= 4) || e.category === 'nightlife';
      if (timeUpper === '3AM') return e.category === 'nightlife' || e.category === 'private' || (e.energyLevel && e.energyLevel >= 7);
      if (['FRI', 'SAT', 'SUN'].includes(timeUpper)) return true;
      return false;
    });

    setHighlightedIds(matching.slice(0, 10).map(item => item.id));
  }, [selectedTime, discoveredEvents]);

  const selectedEvent = useMemo(() => 
    discoveredEvents.find(e => e.id === uiState.selectedItemId) || null,
  [discoveredEvents, uiState.selectedItemId]);

  const savedEvents = useMemo(() => 
    discoveredEvents.filter(e => savedIds.includes(e.id)),
  [discoveredEvents, savedIds]);

  // --- Depth Management ---
  useEffect(() => {
    let newDepth = 0;
    if (uiState.modalState === 'ONBOARDING') newDepth = 4;
    else if (uiState.modalState === 'ADD_EVENT') newDepth = 3;
    else if (uiState.modalState === 'FULL' || uiState.socialPanelOpen || isAssistantOpen) newDepth = 2;
    else if (isTemporalModalOpen || isGeoControlsOpen || uiState.activeMode === 'SAVED' || uiState.activeMode === 'PROFILE' || uiState.activeMode === 'ACTIVITY' || selectedClusterEvents !== null) newDepth = 1;
    setDepth(newDepth);
  }, [uiState.modalState, uiState.socialPanelOpen, isTemporalModalOpen, isGeoControlsOpen, uiState.activeMode, isAssistantOpen, selectedClusterEvents]);

  // --- Event Handlers ---
  const handleEventSelect = useCallback((event: NightlifeItem) => {
    setUiState(prev => ({ 
      ...prev, 
      selectedItemId: event.id,
      modalState: 'FULL'
    }));
  }, []);

  const handleModeChange = useCallback((mode: ViewMode) => {
    setUiState(prev => ({ 
      ...prev, 
      activeMode: mode,
      modalState: mode === 'CREATE' ? 'ADD_EVENT' : null,
      selectedItemId: null 
    }));
  }, []);

  const handleAction = useCallback((action: string | { type: string }) => {
    const actionType = typeof action === 'string' ? action : action.type;
    
    if (actionType === 'CREATE' || actionType === 'ADD') {
      handleModeChange('CREATE');
    } else if (actionType === 'OPEN_TEMPORAL') {
      setIsTemporalModalOpen(true);
    } else if (actionType === 'TOGGLE_SIGNALS_ONLY') {
      setUiState(prev => ({
        ...prev,
        filters: {
          ...prev.filters,
          signalsOnly: !prev.filters.signalsOnly
        }
      }));
    } else if (actionType === 'CLEAR_FILTERS') {
      setUiState(prev => ({
        ...prev,
        filters: {
          ...prev.filters,
          categories: [],
          signalsOnly: false
        }
      }));
      setSearchQuery('');
    } else if (actionType === 'TOGGLE_FILTER') {
      const cat = (action as any).category;
      setUiState(prev => ({
        ...prev,
        filters: {
          ...prev.filters,
          categories: prev.filters.categories.some(c => c.toLowerCase() === cat.toLowerCase())
            ? prev.filters.categories.filter(c => c.toLowerCase() !== cat.toLowerCase())
            : [...prev.filters.categories, cat]
        }
      }));
    }
  }, [handleModeChange]);

  const handleBoundsChange = useCallback(async () => {
    // Bounds tracking for clustering and rendering strategy
  }, []);

  const handleDiscover = useCallback(async () => {
    setIsDiscovering(true);
    await new Promise(resolve => setTimeout(resolve, 2000));
    setIsDiscovering(false);
  }, []);

  const handleToggleSave = useCallback((id: string) => {
    setSavedIds(prev => 
      prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
    );
  }, []);

  const handleCloseDetail = () => setUiState(prev => ({ ...prev, modalState: null, selectedItemId: null }));
  const handleCloseAdd = () => {
    setUiState(prev => ({ ...prev, modalState: null, activeMode: 'DISCOVER' }));
  };
  
  const handleOnboardingNext = () => {
    if (uiState.onboardingStep < 2) {
      setUiState(prev => ({ ...prev, onboardingStep: prev.onboardingStep + 1 }));
    } else {
      setUiState(prev => ({ ...prev, modalState: null, onboardingStep: 0 }));
    }
  };

  return (
    <main className="relative h-screen w-screen bg-[#050505] overflow-hidden selection:bg-brand-primary selection:text-black">
      {/* Layer 0: The World (Map) */}
      <WorldLayer 
        events={filteredEvents}
        selectedEventId={uiState.selectedItemId}
        interestedEventId={uiState.interestedEventId}
        highlightedEventIds={highlightedIds}
        selectedDate={selectedDate}
        activeMode={uiState.activeMode}
        ghostEvent={uiState.ghostEvent}
        depth={depth}
        mapCenter={mapCenter}
        onEventSelect={handleEventSelect}
        onBoundsChange={handleBoundsChange}
        onCenterChange={setMapCenter}
        onAnchorChange={setAnchorPoint}
        onGhostMove={(lat, lng) => setUiState(prev => ({ 
          ...prev, 
          ghostEvent: { ...prev.ghostEvent, latitude: lat, longitude: lng, coordinates: { lat, lng } } 
        }))}
        onClusterSelect={setSelectedClusterEvents}
      />

      {/* Loading & Status Overlays */}
      <AnimatePresence>
        {isLoading && (
          <motion.div
            initial={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[1000] bg-black flex flex-col items-center justify-center space-y-4"
          >
            <div className="w-12 h-12 border-4 border-[#00FF9C]/20 border-t-[#00FF9C] rounded-full animate-spin" />
            <span className="text-[10px] font-black uppercase tracking-[0.3em] text-[#00FF9C]">Initializing_Network</span>
          </motion.div>
        )}

        {locationPermission === 'denied' && !isWarningDismissed && (
          <motion.div
            initial={{ opacity: 0, y: 50 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: 50 }}
            className="fixed bottom-32 left-6 right-6 z-[600] pointer-events-auto max-w-xl mx-auto"
          >
            <div className="bg-red-500/10 backdrop-blur-xl border border-red-500/25 p-5 rounded-[2rem] flex flex-col sm:flex-row sm:items-center gap-4 shadow-xl">
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 rounded-2xl bg-red-500/20 flex items-center justify-center text-red-500 shrink-0">
                  <MapPin className="w-6 h-6 text-red-500" />
                </div>
                <div>
                  <p className="text-[10px] font-black text-white uppercase tracking-wider">Location Access Denied</p>
                  <p className="text-[8.5px] font-mono text-white/50 uppercase tracking-widest mt-0.5">Choose a fallback coordinate below or close this to search active signals.</p>
                </div>
              </div>

              <div className="flex flex-wrap items-center gap-2 sm:ml-auto">
                <button
                  type="button"
                  onClick={() => {
                    setMapCenter({ lat: 29.7461, lng: -95.3781 }); // Houston Event Hub center
                    setIsWarningDismissed(true);
                  }}
                  className="h-9 px-3.5 bg-white/10 hover:bg-white/20 border border-white/10 active:scale-95 rounded-xl text-[8px] font-black uppercase tracking-widest text-[#00FF9C] transition-all flex items-center gap-1.5 cursor-pointer"
                >
                  <Navigation className="w-3 h-3 text-[#00FF9C]" />
                  Houston (Default)
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setMapCenter({ lat: 30.2672, lng: -97.7431 }); // Austin Event center
                    setIsWarningDismissed(true);
                  }}
                  className="h-9 px-3.5 bg-white/10 hover:bg-white/20 border border-white/10 active:scale-95 rounded-xl text-[8px] font-black uppercase tracking-widest text-white hover:text-[#00FF9C] transition-all flex items-center gap-1.5 cursor-pointer"
                >
                  <Navigation className="w-3 h-3 text-white/40" />
                  Austin
                </button>
                <button
                  type="button"
                  onClick={() => setIsWarningDismissed(true)}
                  aria-label="Dismiss location warning"
                  className="w-9 h-9 bg-white/5 hover:bg-white/10 active:scale-95 rounded-xl text-white/40 hover:text-white flex items-center justify-center transition-all ml-1 cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Layer 1: Interactions (Nav, Filters, Overlays) */}
      <InteractionLayer 
        isIngesting={false}
        isDiscovering={isDiscovering}
        onDiscover={handleDiscover}
        activeMode={uiState.activeMode}
        onModeChange={handleModeChange}
        onAction={handleAction}
        isQuickScrubbing={uiState.activeMode === 'DISCOVER' && depth === 0}
        onTimeChange={setSelectedTime}
        selectedTime={selectedTime}
        depth={depth}
        onRecenter={handleRecenter}
        onOpenAssistant={() => setIsAssistantOpen(true)}
        filters={uiState.filters}
        searchQuery={searchQuery}
        onSearchChange={setSearchQuery}
      />

      {/* Layer 2: Overlays (Modals, Details, Flow) */}
      <OverlayLayer 
        depth={depth}
        activeMode={uiState.activeMode}
        modalState={uiState.modalState}
        onboardingStep={uiState.onboardingStep}
        onOnboardingNext={handleOnboardingNext}
        selectedEvent={selectedEvent}
        discoveredEvents={discoveredEvents}
        savedEvents={savedEvents}
        savedIds={savedIds}
        selectedDate={selectedDate}
        selectedTime={selectedTime}
        isTemporalModalOpen={isTemporalModalOpen}
        isGeoControlsOpen={isGeoControlsOpen}
        socialPanelOpen={uiState.socialPanelOpen}
        interestedEventId={uiState.interestedEventId}
        unlockedTiers={uiState.unlockedTiers}
        ghostEvent={uiState.ghostEvent}
        mapCenter={mapCenter}
        anchorPoint={anchorPoint}
        filters={uiState.filters}
        
        isAssistantOpen={isAssistantOpen}
        onCloseAssistant={() => setIsAssistantOpen(false)}
        onRadiusChange={(r) => setUiState(prev => ({
          ...prev,
          filters: { ...prev.filters, radius: r }
        }))}
        onToggleCategory={(cat) => setUiState(prev => ({
          ...prev,
          filters: {
            ...prev.filters,
            categories: prev.filters.categories.includes(cat)
              ? prev.filters.categories.filter(c => c !== cat)
              : [...prev.filters.categories, cat]
          }
        }))}
        
        onCloseTemporal={() => setIsTemporalModalOpen(false)}
        onCloseGeo={() => setIsGeoControlsOpen(false)}
        onCloseSocial={() => setUiState(prev => ({ ...prev, socialPanelOpen: false }))}
        onCloseSaved={() => handleModeChange('DISCOVER')}
        onCloseProfile={() => handleModeChange('DISCOVER')}
        onCloseDetail={handleCloseDetail}
        onCloseAdd={handleCloseAdd}
        onDateSelect={setSelectedDate}
        onTimeSelect={setSelectedTime}
        onViewFullDetails={handleEventSelect}
        onToggleSave={handleToggleSave}
        onHighlight={setHighlightedIds}
        onPublishSignal={(event) => {
          setDiscoveredEvents(prev => [event, ...prev]);
          handleCloseAdd();
        }}
        onGhostUpdate={(ghost) => setUiState(prev => ({ ...prev, ghostEvent: ghost }))}
      />

      {/* Cluster ZoneLens sliding drawer */}
      <ZoneDrawer 
        isOpen={selectedClusterEvents !== null}
        onClose={() => setSelectedClusterEvents(null)}
        events={selectedClusterEvents || []}
        onEventSelect={handleEventSelect}
      />

      {/* Global Vignette */}
      <div className="fixed inset-0 pointer-events-none z-[500] shadow-[inset_0_0_100px_rgba(0,0,0,0.4)]" />
    </main>
  );
}
