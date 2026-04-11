"use client";

import { useEffect, useState } from "react";
import { Bookmark, Navigation, Share2 } from "lucide-react";
import { getAuthHeader } from "@/services/auth";
import { toApiUrl } from "@/services/apiBase";

type SessionState =
  | { kind: "anonymous" }
  | { kind: "authenticated"; label: string };

interface SavedEventsResponse {
  items: Array<{ eventId: string }>;
}

interface EventDetailActionsProps {
  eventId: string;
}

export default function EventDetailActions({
  eventId,
}: EventDetailActionsProps) {
  const [session, setSession] = useState<SessionState>({ kind: "anonymous" });
  const [isSaved, setIsSaved] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let disposed = false;

    async function hydrate() {
      const headers = getAuthHeader();
      if (!headers.Authorization) {
        if (!disposed) {
          setSession({ kind: "anonymous" });
          setIsSaved(false);
        }
        return;
      }

      try {
        const [meResponse, savesResponse] = await Promise.all([
          fetch(toApiUrl("/auth/me"), { headers }),
          fetch(toApiUrl("/api/users/me/saves?page=1&pageSize=100"), {
            headers,
          }),
        ]);

        if (!meResponse.ok) {
          throw new Error("Session expired.");
        }

        const me = await meResponse.json();
        const saves = savesResponse.ok
          ? ((await savesResponse.json()) as SavedEventsResponse)
          : { items: [] };

        if (!disposed) {
          setSession({
            kind: "authenticated",
            label: me.displayName ?? me.email ?? me.userId,
          });
          setIsSaved(saves.items.some((item) => item.eventId === eventId));
        }
      } catch (hydrateError) {
        if (!disposed) {
          setSession({ kind: "anonymous" });
          setIsSaved(false);
          setError(
            hydrateError instanceof Error
              ? hydrateError.message
              : "Unable to load session.",
          );
        }
      }
    }

    hydrate();
    return () => {
      disposed = true;
    };
  }, [eventId]);

  async function toggleSave() {
    const headers = getAuthHeader();
    if (!headers.Authorization) {
      setError("Login to save signals.");
      return;
    }

    setIsBusy(true);
    setError(null);

    try {
      const response = await fetch(
        toApiUrl(`/api/users/me/saves/${encodeURIComponent(eventId)}`),
        {
          method: isSaved ? "DELETE" : "POST",
          headers,
        },
      );

      if (!response.ok) {
        throw new Error(`Save request failed with status ${response.status}.`);
      }

      setIsSaved((current) => !current);
    } catch (toggleError) {
      setError(
        toggleError instanceof Error
          ? toggleError.message
          : "Unable to update save state.",
      );
    } finally {
      setIsBusy(false);
    }
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
          {session.kind === "authenticated" ? session.label : "ANONYMOUS"}
        </div>
        {error ? (
          <div className="mt-2 text-[10px] font-mono text-red-300">{error}</div>
        ) : null}
      </div>

      <button
        type="button"
        onClick={toggleSave}
        disabled={isBusy}
        data-testid="event-save-button"
        className="w-full h-20 bg-white text-black rounded-2xl font-black uppercase tracking-[0.3em] text-[11px] flex items-center justify-between px-8 hover:scale-[1.02] active:scale-[0.98] transition-all group disabled:opacity-60"
      >
        {isSaved ? "UNSAVE_SIGNAL" : "SAVE_SIGNAL"}
        <Bookmark
          className={`w-5 h-5 transition-colors ${isSaved ? "text-[#00FF9C]" : "group-hover:text-[#00FF9C]"}`}
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
