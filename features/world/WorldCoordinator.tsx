"use client";
import React, { useState, useCallback, useEffect } from "react";
import TopBar from "@/components/TopBar";
import RadarMap from "@/components/RadarMap";
import BottomNav from "@/components/BottomNav";
import CulturalCalendar from "@/components/CulturalCalendar";
import SocialSignalPanel from "@/components/SocialSignalPanel";
import SavedEvents from "@/components/SavedEvents";
import ProfilePanel from "@/components/ProfilePanel";
import TimelineControl from "@/components/TimelineControl";
import EventSignalModal from "@/components/EventSignalModal";
import GeoControls from "@/components/GeoControls";
import AddEventModal from "@/components/AddEventModal";
import TemporalDebugPanel from "@/components/TemporalDebugPanel";
import { NightlifeItem } from "@/types";
import { useWorldSurfaceState } from "@/hooks/useWorldSurfaceState";
import { useEventFeed } from "@/hooks/useEventFeed";
import { useTemporalQuery } from "@/hooks/useTemporalQuery";
import { useEventSubmission } from "@/hooks/useEventSubmission";

export default function WorldCoordinator() {
  const {
    state,
    dispatch,
    selectEvent,
    interestEvent,
    openModal,
    closeModal,
    setMapCenter,
    setMapBounds,
    beginDraft,
    updateDraft,
    publishDraft,
    toggleSave,
    persistedSnapshot,
    restorePersistedState,
  } = useWorldSurfaceState();

  // Persisted UI snapshot key
  const PERSIST_KEY = 'weup.ui.persisted.v1';

  // Hydrate persisted UI slice on mount
  useEffect(() => {
    try {
      const raw = localStorage.getItem(PERSIST_KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (parsed && typeof parsed === 'object') {
          restorePersistedState(parsed);
        }
      }
    } catch (err) {
      console.warn('Failed to restore persisted UI state', err);
    }
  }, [restorePersistedState]);

  // Save persisted slice when it changes
  useEffect(() => {
    try {
      const snap = persistedSnapshot();
      localStorage.setItem(PERSIST_KEY, JSON.stringify(snap));
    } catch (err) {
      console.warn('Failed to persist UI snapshot', err);
    }
  }, [state.savedEventIds, state.lastKnownMapCenter, persistedSnapshot]);

  // ── Data layers ────────────────────────────────────────────────────────────
  const { events, prependEvent } = useEventFeed(state.mapBounds);

  // Timeline scrubber value owns the temporal preset string.
  const [currentTimePreset, setCurrentTimePreset] = useState("NOW");
  const { temporalWindow, loading: temporalLoading } =
    useTemporalQuery(currentTimePreset);

  const { handlePublish } = useEventSubmission();

  // ── Event handlers ─────────────────────────────────────────────────────────

  const handleEventSelect = useCallback(
    (e: NightlifeItem) => {
      selectEvent(e.id);
      openModal("EVENT_DETAIL");
    },
    [selectEvent, openModal],
  );

  const handleGhostUpdate = useCallback(
    (patch: Partial<NightlifeItem> | null) => {
      if (patch) beginDraft(patch as any);
      else updateDraft({} as any);
    },
    [beginDraft, updateDraft],
  );

  const handleConfirmPublish = useCallback(
    async (event: NightlifeItem) => {
      await handlePublish(event, (published, id) => {
        publishDraft(id);
        prependEvent(published);
      });
    },
    [handlePublish, publishDraft, prependEvent],
  );

  const handleBottomNavAction = useCallback(
    (action: string) => {
      switch (action) {
        case "SAVED":
          dispatch({ type: "SET_VIEW_MODE", mode: "SAVED" });
          break;
        case "PROFILE":
          dispatch({ type: "SET_VIEW_MODE", mode: "PROFILE" });
          break;
        case "WORLD_LONG":
        case "WORLD":
          dispatch({ type: "SET_VIEW_MODE", mode: "RADAR" });
          break;
        case "TIME_TAP":
          dispatch({
            type: "SET_VIEW_MODE",
            mode: state.viewMode === "CALENDAR" ? "RADAR" : "CALENDAR",
          });
          break;
        case "ADD":
        case "ADD_LONG":
          openModal("ADD_EVENT");
          break;
      }
    },
    [dispatch, state.viewMode, openModal],
  );

  // ── Derived ────────────────────────────────────────────────────────────────
  const savedEventItems = events.filter((e) =>
    state.savedEventIds.includes(e.id),
  );
  const topModalKind =
    state.modal.kind === "STACK"
      ? state.modal.stack[state.modal.stack.length - 1]
      : null;

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
        onGhostMove={(lat, lng) =>
          updateDraft({ latitude: lat, longitude: lng } as any)
        }
        selectedDate={state.selectedDate}
        activeMode={state.viewMode}
      />

      {/* Timeline scrubber — fixed above BottomNav */}
      <div className="fixed bottom-36 left-0 right-0 z-[150] flex flex-col items-center pointer-events-none">
        <div className="pointer-events-auto">
          <TimelineControl onTimeChange={setCurrentTimePreset} />
        </div>
      </div>

      <TemporalDebugPanel
        preset={currentTimePreset}
        presetLabel={temporalWindow?.presetLabel}
        timeWindowStart={temporalWindow?.timeWindowStart}
        timeWindowEnd={temporalWindow?.timeWindowEnd}
        timezone={temporalWindow?.timezone}
        eventCount={temporalWindow?.count}
        loading={temporalLoading}
      />

      <BottomNav
        activeMode={state.viewMode}
        onModeChange={(mode) => dispatch({ type: "SET_VIEW_MODE", mode })}
        onAction={handleBottomNavAction}
      />

      {/* Overlay panels */}
      <CulturalCalendar
        isVisible={state.viewMode === "CALENDAR"}
        events={events}
        onEventSelect={handleEventSelect}
        onClose={() => dispatch({ type: "SET_VIEW_MODE", mode: "RADAR" })}
        selectedDate={state.selectedDate}
        onDateSelect={(date) => interestEvent(date as any)}
      />
      <SavedEvents
        isVisible={state.viewMode === "SAVED"}
        onClose={() => dispatch({ type: "SET_VIEW_MODE", mode: "RADAR" })}
        savedEvents={savedEventItems}
      />
      <ProfilePanel
        isVisible={state.viewMode === "PROFILE"}
        onClose={() => dispatch({ type: "SET_VIEW_MODE", mode: "RADAR" })}
      />

      <AddEventModal
        isVisible={topModalKind === "ADD_EVENT"}
        onClose={() => closeModal()}
        onPublish={handleConfirmPublish}
        onGhostUpdate={handleGhostUpdate}
        ghostEvent={state.ghostDraft as any}
        mapCenter={state.mapCenter}
      />

      <EventSignalModal
        event={
          state.selectedEventId
            ? (events.find((e) => e.id === state.selectedEventId) ?? null)
            : null
        }
        state={
          state.modal.kind === "STACK" &&
          state.modal.stack.includes("EVENT_DETAIL")
            ? "FULL"
            : null
        }
        onClose={() => closeModal()}
        onSave={(id) => toggleSave(id)}
        isSaved={
          state.selectedEventId
            ? state.savedEventIds.includes(state.selectedEventId)
            : false
        }
      />
    </div>
  );
}
