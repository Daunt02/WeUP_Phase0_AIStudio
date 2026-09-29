'use client';

import React from 'react';
import { AnimatePresence, motion } from 'motion/react';
import CulturalCalendar from './CulturalCalendar';
import SavedEvents from './SavedEvents';
import ProfilePanel from './ProfilePanel';
import EventSignalModal from './EventSignalModal';
import AddEventModal from './AddEventModal';
import SocialSignalPanel from './SocialSignalPanel';
import GeoControls from './GeoControls';
import SpikeAssistantPanel from './SpikeAssistantPanel';
import { NightlifeItem, ViewMode, ModalState } from '@/types';
import { Activity, Sparkles, ShieldCheck } from 'lucide-react';

interface OverlayLayerProps {
  depth: number;
  activeMode: ViewMode;
  modalState: ModalState;
  onboardingStep: number;
  onOnboardingNext: () => void;
  selectedEvent: NightlifeItem | null;
  discoveredEvents: NightlifeItem[];
  savedEvents: NightlifeItem[];
  savedIds: string[];
  selectedDate: string;
  selectedTime: string;
  isTemporalModalOpen: boolean;
  isGeoControlsOpen: boolean;
  socialPanelOpen: boolean;
  interestedEventId: string | null;
  unlockedTiers: number[];
  ghostEvent: Partial<NightlifeItem> | null;
  mapCenter: { lat: number, lng: number };
  anchorPoint: { x: number, y: number } | null;
  filters: any;
  
  isAssistantOpen: boolean;
  onCloseAssistant: () => void;
  onRadiusChange: (radius: number) => void;
  onToggleCategory: (category: string) => void;
  
  onCloseTemporal: () => void;
  onCloseGeo: () => void;
  onCloseSocial: () => void;
  onCloseSaved: () => void;
  onCloseProfile: () => void;
  onCloseDetail: () => void;
  onCloseAdd: () => void;
  onDateSelect: (date: string) => void;
  onTimeSelect: (time: string) => void;
  onViewFullDetails: (event: NightlifeItem) => void;
  onToggleSave: (id: string) => void;
  onHighlight: (ids: string[]) => void;
  onPublishSignal: (event: NightlifeItem) => void;
  onGhostUpdate: (ghost: Partial<NightlifeItem> | null) => void;
}

export default function OverlayLayer({
  activeMode,
  modalState,
  onboardingStep,
  onOnboardingNext,
  selectedEvent,
  discoveredEvents,
  savedEvents,
  savedIds,
  selectedDate,
  selectedTime,
  isTemporalModalOpen,
  isGeoControlsOpen,
  socialPanelOpen,
  interestedEventId,
  unlockedTiers,
  ghostEvent,
  mapCenter,
  anchorPoint,
  filters,
  
  isAssistantOpen,
  onCloseAssistant,
  onRadiusChange,
  onToggleCategory,
  
  onCloseTemporal,
  onCloseGeo,
  onCloseSocial,
  onCloseSaved,
  onCloseProfile,
  onCloseDetail,
  onCloseAdd,
  onDateSelect,
  onTimeSelect,
  onViewFullDetails,
  onToggleSave,
  onHighlight,
  onPublishSignal,
  onGhostUpdate
}: OverlayLayerProps) {
  const onboardingContent = [
    { 
      title: "EXPLORE SIGNALS", 
      content: "WEUP filters the noise to reveal real-time nightlife pulses. Each signal on your radar represents an active or upcoming high-energy event.",
      icon: <Activity className="w-10 h-10 text-[#00FF9C]" />
    },
    { 
      title: "BROADCAST PULSE", 
      content: "Found something hot? Contribute to the network. Upload a flyer or paste a link to broadcast a new signal to the community.",
      icon: <Sparkles className="w-10 h-10 text-[#00FF9C]" />
    },
    { 
      title: "SOCIAL NETWORK", 
      content: "Interaction earns you reputation. Save signals, follow creators, and unlock exclusive network tiers as you navigate the city.",
      icon: <ShieldCheck className="w-10 h-10 text-[#00FF9C]" />
    }
  ];

  return (
    <div className="fixed inset-0 z-[300] pointer-events-none">
      <AnimatePresence mode="wait">
        {/* Onboarding Overlay */}
        {modalState === 'ONBOARDING' && (
          <motion.div
            key="onboarding"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/80 backdrop-blur-3xl pointer-events-auto flex items-center justify-center p-6"
          >
            <motion.div 
              initial={{ scale: 0.9, y: 40 }}
              animate={{ scale: 1, y: 0 }}
              className="max-w-sm w-full bg-[#0a0a0a] border border-white/10 rounded-[48px] p-10 text-center space-y-8 shadow-[0_40px_120px_rgba(0,0,0,1)] relative overflow-hidden"
            >
              <div className="absolute top-0 inset-x-0 h-1 bg-gradient-to-r from-transparent via-[#00FF9C] to-transparent opacity-20" />
              
              <div className="w-24 h-24 bg-[#00FF9C]/5 rounded-[32px] mx-auto flex items-center justify-center shadow-[inset_0_0_20px_rgba(0,255,156,0.1)] border border-[#00FF9C]/10">
                {onboardingContent[onboardingStep].icon}
              </div>
              
              <div className="space-y-3">
                <h2 className="text-2xl font-black tracking-tighter text-white uppercase italic leading-none">{onboardingContent[onboardingStep].title}</h2>
                <p className="text-white/40 text-[11px] leading-relaxed uppercase tracking-wider font-mono font-medium max-w-[240px] mx-auto">
                  {onboardingContent[onboardingStep].content}
                </p>
              </div>
              
              <div className="flex gap-3 justify-center">
                {[0, 1, 2].map(i => (
                  <div key={i} className={`h-1.5 rounded-full transition-all duration-500 ${onboardingStep === i ? 'w-10 bg-[#00FF9C]' : 'w-2 bg-white/10'}`} />
                ))}
              </div>
              
              <button 
                onClick={onOnboardingNext}
                className="w-full bg-[#00FF9C] text-black h-16 rounded-3xl font-black uppercase tracking-[0.25em] text-[11px] hover:bg-white transition-all transform hover:scale-105 active:scale-95 shadow-[0_20px_40px_rgba(0,255,156,0.2)]"
              >
                {onboardingStep === 2 ? 'ENTER_THE_NETWORK' : 'PROCEED'}
              </button>
            </motion.div>
          </motion.div>
        )}

        {/* Depth 1: Contextual Overlays */}
        {(isTemporalModalOpen || activeMode === 'ACTIVITY') && (
          <CulturalCalendar 
            key="temporal-overlay"
            isVisible={true} 
            events={discoveredEvents}
            selectedDate={selectedDate}
            selectedTime={selectedTime}
            onDateSelect={onDateSelect}
            onTimeSelect={onTimeSelect}
            onEventSelect={onViewFullDetails}
            onToggleSave={onToggleSave}
            onHighlight={onHighlight}
            savedIds={savedIds}
            onClose={() => {
              onCloseTemporal();
              if (activeMode === 'ACTIVITY') onCloseSaved();
            }}
          />
        )}
...

        {isGeoControlsOpen && (
          <GeoControls 
            key="geo-overlay"
            isVisible={true}
            onClose={onCloseGeo}
          />
        )}

        {activeMode === 'SAVED' && (
          <SavedEvents 
            key="saved-overlay"
            isVisible={true}
            onClose={onCloseSaved}
            savedEvents={savedEvents}
          />
        )}

        {activeMode === 'PROFILE' && (
          <ProfilePanel 
            key="profile-overlay"
            isVisible={true}
            onClose={onCloseProfile}
          />
        )}

        {isAssistantOpen && (
          <SpikeAssistantPanel
            key="spike-assistant-panel"
            isVisible={true}
            onClose={onCloseAssistant}
            mapCenter={mapCenter}
            radius={filters?.radius || 5}
            onRadiusChange={onRadiusChange}
            activeCategories={filters?.categories || []}
            onToggleCategory={onToggleCategory}
            onPublishSignal={onPublishSignal}
          />
        )}

        {/* Depth 2: Item Details & Social */}
        {socialPanelOpen && (
          <SocialSignalPanel 
            key="social-overlay"
            isOpen={true}
            event={discoveredEvents.find(e => e.id === interestedEventId) || null}
            unlockedTiers={unlockedTiers}
            onClose={onCloseSocial}
          />
        )}

        {modalState === 'FULL' && selectedEvent && (
          <EventSignalModal 
            key={`detail-${selectedEvent.id}`}
            event={selectedEvent}
            state="FULL"
            onClose={onCloseDetail}
            onSave={onToggleSave}
            isSaved={savedIds.includes(selectedEvent.id)}
            anchorPoint={anchorPoint}
          />
        )}

        {/* Depth 3: Action Flows */}
        {modalState === 'ADD_EVENT' && (
          <AddEventModal 
            key="add-event-overlay"
            isVisible={true}
            onClose={onCloseAdd}
            onPublish={onPublishSignal}
            onGhostUpdate={onGhostUpdate}
            ghostEvent={ghostEvent}
            mapCenter={mapCenter}
          />
        )}
      </AnimatePresence>
    </div>
  );
}
