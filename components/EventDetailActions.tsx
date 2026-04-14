"use client";

import { useEffect } from "react";
import { Bookmark, Navigation, Share2 } from "lucide-react";
import { useSavedEventsAuthority } from "@/hooks/useSavedEventsAuthority";

interface EventDetailActionsProps {
  eventId: string;
}

export default function EventDetailActions({
  eventId,
}: EventDetailActionsProps) {
  const {
    sessionKind,
    isSaved,
    loading,
    syncing,
    error,
    clearError,
    toggleSavedEvent,
  } = useSavedEventsAuthority();

  useEffect(() => {
    clearError();
  }, [eventId, clearError]);

  async function toggleSave() {
    await toggleSavedEvent(eventId);
  }

  async function shareSignal() {
    try {
      await navigator.share?.({
        url: window.location.href,
        title: document.title,
      });
    } catch {
      await navigator.clipboard?.writeText(window.location.href);
    }
  }

  return (
    <div className="space-y-4">
      <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
        <div className="text-[9px] font-mono uppercase tracking-[0.3em] text-white/20">
          SESSION
        </div>
        <div
          className="mt-2 text-sm font-bold uppercase text-white/80"
          data-testid="event-session-state"
        >
          {sessionKind === "authenticated" ? "AUTHENTICATED" : "ANONYMOUS"}
        </div>
        {error ? (
          <div className="mt-2 text-[10px] font-mono text-red-300">{error}</div>
        ) : null}
      </div>

      <button
        type="button"
        onClick={toggleSave}
        disabled={loading || syncing}
        data-testid="event-save-button"
        className="w-full h-20 bg-white text-black rounded-2xl font-black uppercase tracking-[0.3em] text-[11px] flex items-center justify-between px-8 hover:scale-[1.02] active:scale-[0.98] transition-all group disabled:opacity-60"
      >
        {isSaved(eventId) ? "UNSAVE_SIGNAL" : "SAVE_SIGNAL"}
        <Bookmark
          className={`w-5 h-5 transition-colors ${isSaved(eventId) ? "text-[#00FF9C]" : "group-hover:text-[#00FF9C]"}`}
        />
      </button>

      <button className="w-full h-20 bg-white/5 border border-white/10 text-white rounded-2xl font-black uppercase tracking-[0.3em] text-[11px] flex items-center justify-between px-8 hover:bg-white/10 transition-all group">
        GET_DIRECTIONS
        <Navigation className="w-5 h-5 group-hover:text-[#00FF9C] transition-colors" />
      </button>

      <button
        type="button"
        onClick={shareSignal}
        className="w-full h-20 bg-white/5 border border-white/10 text-white rounded-2xl font-black uppercase tracking-[0.3em] text-[11px] flex items-center justify-between px-8 hover:bg-white/10 transition-all group"
      >
        SHARE_SIGNAL
        <Share2 className="w-5 h-5 group-hover:text-[#00FF9C] transition-colors" />
      </button>
    </div>
  );
}
