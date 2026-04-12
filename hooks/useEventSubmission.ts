"use client";
/**
 * useEventSubmission
 *
 * Encapsulates the draft-create → submit-for-review flow. Provides a stable
 * handlePublish callback that the coordinator calls on AddEventModal confirm.
 *
 * Seam: submissionService targets /api/events/submissions; replace with a
 * real backend in P09 without touching this hook or the coordinator.
 */

import { useCallback } from "react";
import submissionService from "@/services/submissionService";
import {
  RuntimeEventProjection,
  toLegacyNightlifeItem,
} from "@/features/world/runtimeTypes";

export interface EventSubmissionResult {
  /**
   * Submit an event draft to the backend. On success (or fallback), calls
   * onSuccess with the published event and its assigned id.
   */
  handlePublish: (
    event: RuntimeEventProjection,
    onSuccess: (event: RuntimeEventProjection, id: string) => void,
  ) => Promise<void>;
}

export function useEventSubmission(): EventSubmissionResult {
  const handlePublish = useCallback(
    async (
      event: RuntimeEventProjection,
      onSuccess: (event: RuntimeEventProjection, id: string) => void,
    ) => {
      try {
        const legacyDraft = toLegacyNightlifeItem(event);
        const created = await submissionService.createDraft({ ...legacyDraft });
        try {
          await submissionService.submitForReview(created.id);
        } catch (submitErr) {
          // Review submission is best-effort; draft creation succeeded.
          console.warn(
            "[useEventSubmission] submitForReview failed:",
            submitErr,
          );
        }
        onSuccess(event, created.id || event.id);
      } catch (err) {
        console.error("[useEventSubmission] createDraft failed:", err);
        // Fallback — keep UX responsive when backend is unavailable (Phase 0)
        onSuccess(event, event.id);
      }
    },
    [],
  );

  return { handlePublish };
}
