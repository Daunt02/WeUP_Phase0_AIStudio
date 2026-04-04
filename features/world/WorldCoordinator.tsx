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
import { NightlifeItem, ViewMode } from '@/types';
import { eventService } from '@/services/eventService';
import submissionService from '@/services/submissionService';
import { useWorldSurfaceState } from '@/hooks/useWorldSurfaceState';

export default function WorldCoordinator() {
  const { state, selectEvent, interestEvent, openModal, closeModal, setMapCenter, setMapBounds, beginDraft, updateDraft, publishDraft, toggleSave } = useWorldSurfaceState();

  const [events, setEvents] = useState<NightlifeItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [currentTime, setCurrentTime] = useState('NOW');
  const [activeMode, setActiveMode] = useState<ViewMode>('RADAR');

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

  const handlePublish = async (event: NightlifeItem) => {
    // Attempt to create a durable submission, then submit for review.
    try {
      const payload = { ...event };
      const created = await submissionService.createDraft(payload);
      try {
        await submissionService.submitForReview(created.id);
      } catch (submitErr) {
        console.warn('Submit for review failed:', submitErr);
      }
      // Update UI state to reflect published/queued signal
      publishDraft(created.id || event.id);
      setEvents(prev => [event, ...prev]);
    } catch (err) {
      console.error('Failed to persist submission:', err);
      // Fallback to local behavior so UX remains responsive
      publishDraft(event.id);
      setEvents(prev => [event, ...prev]);
    }
  };

  const handleBottomNavAction = (action: string) => {
    if (action === 'SAVED') setActiveMode('SAVED');
    if (action === 'PROFILE') setActiveMode('PROFILE');
    if (action === 'WORLD_LONG' || action === 'WORLD' || !action) setActiveMode('RADAR');
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

      <TimelineControl onTimeChange={setCurrentTime} />
      <BottomNav
        activeMode={activeMode}
        onModeChange={setActiveMode}
        onAction={handleBottomNavAction}
      />

      <AddEventModal
        isVisible={state.modal.kind === 'STACK' && state.modal.stack[state.modal.stack.length - 1] === 'ADD_EVENT'}
        onClose={() => closeModal()}
        onPublish={handlePublish}
        onGhostUpdate={handleGhostUpdate}
        ghostEvent={state.ghostDraft as any}
        mapCenter={state.mapCenter}
      />

      {/* TODO: Wire remaining panels in future iterations. For P19, focus on map/modal flow. */}
    </div>
  );
}
