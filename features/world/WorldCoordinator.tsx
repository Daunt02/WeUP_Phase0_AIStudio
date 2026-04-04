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
import TemporalDebugPanel from '@/components/TemporalDebugPanel';
import { NightlifeItem, ViewMode } from '@/types';
import { eventService } from '@/services/eventService';
import submissionService from '@/services/submissionService';
import * as temporalService from '@/services/temporalService';
import * as analyticsService from '@/services/analyticsService';
import { useWorldSurfaceState } from '@/hooks/useWorldSurfaceState';

export default function WorldCoordinator() {
  const { state, selectEvent, interestEvent, openModal, closeModal, setMapCenter, setMapBounds, beginDraft, updateDraft, publishDraft, toggleSave } = useWorldSurfaceState();

  const [events, setEvents] = useState<NightlifeItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [currentTime, setCurrentTime] = useState('NOW');
  const [activeMode, setActiveMode] = useState<ViewMode>('RADAR');
  const [temporalWindow, setTemporalWindow] = useState<any>(null);
  const [temporalLoading, setTemporalLoading] = useState(false);

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

  // Query temporal service when time preset changes
  useEffect(() => {
    let cancelled = false;
    async function queryTemporal() {
      setTemporalLoading(true);
      try {
        const response = await temporalService.getEventsAtTime({
          preset: currentTime,
          marketTimezone: 'America/Los_Angeles',
        });
        if (!cancelled) {
          setTemporalWindow(response);
          // Record analytics event
          analyticsService.recordSimpleEvent('TemporalPresetSelected', undefined);
          console.log('Temporal query:', response);
        }
      } catch (err) {
        console.error('Temporal query failed:', err);
      } finally {
        if (!cancelled) setTemporalLoading(false);
      }
    }
    queryTemporal();
    return () => { cancelled = true; };
  }, [currentTime]);

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
    if (action === 'WORLD_LONG' || action === 'WORLD') setActiveMode('RADAR');
    if (action === 'TIME_TAP') setActiveMode(activeMode === 'CALENDAR' ? 'RADAR' : 'CALENDAR');
    if (action === 'ADD' || action === 'ADD_LONG') openModal('ADD_EVENT');
  };

  const savedEventItems = events.filter(e => state.savedEventIds.includes(e.id));

  return (
    <div className="w-full h-screen relative">
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

      {/* Timeline scrubber — fixed above BottomNav */}
      <div className="fixed bottom-36 left-0 right-0 z-[150] flex flex-col items-center pointer-events-none">
        <div className="pointer-events-auto">
          <TimelineControl onTimeChange={setCurrentTime} />
        </div>
      </div>

      <TemporalDebugPanel
        preset={currentTime}
        presetLabel={temporalWindow?.presetLabel}
        timeWindowStart={temporalWindow?.timeWindowStart}
        timeWindowEnd={temporalWindow?.timeWindowEnd}
        timezone={temporalWindow?.timezone}
        eventCount={temporalWindow?.count}
        loading={temporalLoading}
      />

      <BottomNav
        activeMode={activeMode}
        onModeChange={setActiveMode}
        onAction={handleBottomNavAction}
      />

      {/* Overlay panels */}
      <CulturalCalendar
        isVisible={activeMode === 'CALENDAR'}
        events={events}
        onEventSelect={handleEventSelect}
        onClose={() => setActiveMode('RADAR')}
        selectedDate={state.selectedDate}
        onDateSelect={(date) => interestEvent(date as any)}
      />
      <SavedEvents
        isVisible={activeMode === 'SAVED'}
        onClose={() => setActiveMode('RADAR')}
        savedEvents={savedEventItems}
      />
      <ProfilePanel
        isVisible={activeMode === 'PROFILE'}
        onClose={() => setActiveMode('RADAR')}
      />

      <AddEventModal
        isVisible={state.modal.kind === 'STACK' && state.modal.stack[state.modal.stack.length - 1] === 'ADD_EVENT'}
        onClose={() => closeModal()}
        onPublish={handlePublish}
        onGhostUpdate={handleGhostUpdate}
        ghostEvent={state.ghostDraft as any}
        mapCenter={state.mapCenter}
      />

      <EventSignalModal
        event={state.selectedEventId ? (events.find(e => e.id === state.selectedEventId) ?? null) : null}
        state={state.modal.kind === 'STACK' && state.modal.stack.includes('EVENT_DETAIL') ? 'FULL' : null}
        onClose={() => closeModal()}
        onSave={(id) => toggleSave(id)}
        isSaved={state.selectedEventId ? state.savedEventIds.includes(state.selectedEventId) : false}
      />
    </div>
  );
}
