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
  SubmissionDraftProjection,
  toDraftSubmissionRequest,
} from "@/features/world/runtimeTypes";

export interface EventSubmissionResult {
  /**
   * Submit an event draft to the backend. On success (or fallback), calls
   * onSuccess with the published event and its assigned id.
   */
  handlePublish: (
    event: SubmissionDraftProjection,
    onSuccess: (event: SubmissionDraftProjection, id: string) => void,
  ) => Promise<void>;
}

export function useEventSubmission(): EventSubmissionResult {
  const handlePublish = useCallback(
    async (
      event: SubmissionDraftProjection,
      onSuccess: (event: SubmissionDraftProjection, id: string) => void,
    ) => {
      try {
        const draftRequest = toDraftSubmissionRequest(event);
        const created = await submissionService.createDraft(draftRequest);
        try {
          await submissionService.submitForReview(created.submissionId);
        } catch (submitErr) {
          // Review submission is best-effort; draft creation succeeded.
          console.warn(
            "[useEventSubmission] submitForReview failed:",
            submitErr,
          );
        }
        onSuccess(event, created.submissionId || event.id);
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
