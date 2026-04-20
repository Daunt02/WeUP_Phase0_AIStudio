"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  CheckCircle2,
  XCircle,
  MessageSquare,
  Archive,
  AlertTriangle,
  Clock,
  ShieldCheck,
  FileText,
  History,
  ChevronDown,
  ChevronUp,
  Loader2,
} from "lucide-react";
import {
  getModerationItem,
  getModerationEvidence,
  getModerationHistory,
  approveItem,
  rejectItem,
  requestChanges,
  archiveItem,
  type ModerationQueueItemDto,
  type ModerationEvidenceBundle,
  type ReviewHistoryItemDto,
} from "@/services/moderationService";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const DEV_ACTOR = "dev-moderator";

function formatTime(iso: string) {
  try {
    return new Date(iso).toLocaleString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return iso;
  }
}

function confidencePct(v: number) {
  return `${Math.round(v * 100)}%`;
}

function bucketColor(bucket: string) {
  if (bucket === "High") return "text-[#00FF9C]";
  if (bucket === "Medium") return "text-yellow-400";
  return "text-red-400";
}

// ---------------------------------------------------------------------------
// Confidence vector display
// ---------------------------------------------------------------------------

function ConfidenceGrid({
  c,
}: {
  c: ModerationQueueItemDto["confidenceSummary"];
}) {
  const fields: Array<[string, number]> = [
    ["EXTRACTION", c.extraction],
    ["GEOCODE", c.geocode],
    ["TEMPORAL", c.temporal],
    ["VENUE", c.venueMatch],
    ["DUPE_RISK", c.dupeRisk],
    ["AGGREGATE", c.aggregate],
  ];
  return (
    <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
      {fields.map(([label, val]) => {
        const pct = Math.round(val * 100);
        const color =
          val >= 0.85 ? "#00FF9C" : val >= 0.55 ? "#facc15" : "#f87171";
        return (
          <div
            key={label}
            className="p-3 bg-white/[0.02] border border-white/5 rounded-xl"
          >
            <div className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em] mb-2">
              {label}
            </div>
            <div className="flex items-center gap-2">
              <div className="flex-1 h-1 bg-white/10 rounded-full overflow-hidden">
                <div
                  className="h-full rounded-full"
                  style={{ width: `${pct}%`, backgroundColor: color }}
                />
              </div>
              <span className="font-mono text-[10px] text-white/60 w-8 text-right">
                {pct}%
              </span>
            </div>
          </div>
        );
      })}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Collapsible section
// ---------------------------------------------------------------------------

function Section({
  title,
  icon: Icon,
  children,
  defaultOpen = true,
}: {
  title: string;
  icon: React.ComponentType<{ className?: string }>;
  children: React.ReactNode;
  defaultOpen?: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="bg-white/[0.02] border border-white/5 rounded-2xl overflow-hidden">
      <button
        onClick={() => setOpen((p) => !p)}
        className="w-full flex items-center justify-between px-6 py-4 hover:bg-white/[0.02] transition-colors"
      >
        <div className="flex items-center gap-3">
          <Icon className="w-4 h-4 text-[#00FF9C]" />
          <span className="font-mono text-[10px] uppercase tracking-[0.3em] text-white/60">
            {title}
          </span>
        </div>
        {open ? (
          <ChevronUp className="w-4 h-4 text-white/20" />
        ) : (
          <ChevronDown className="w-4 h-4 text-white/20" />
        )}
      </button>
      {open && <div className="px-6 pb-6">{children}</div>}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Review action panel
// ---------------------------------------------------------------------------

type ActiveAction = "approve" | "reject" | "request-changes" | "archive" | null;

const REJECT_REASONS = [
  "Inaccurate information",
  "Duplicate event",
  "Spam / promotional abuse",
  "Prohibited content",
  "Low quality / unverifiable",
  "Missing critical details",
];

function ActionPanel({
  itemId,
  onComplete,
}: {
  itemId: string;
  onComplete: () => void;
}) {
  const [activeAction, setActiveAction] = useState<ActiveAction>(null);
  const [comment, setComment] = useState("");
  const [selectedReasons, setSelectedReasons] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  function toggleReason(r: string) {
    setSelectedReasons((p) =>
      p.includes(r) ? p.filter((x) => x !== r) : [...p, r],
    );
  }

  async function handleSubmit() {
    if (!activeAction) return;
    setSubmitting(true);
    setSubmitError(null);
    try {
      switch (activeAction) {
        case "approve":
          await approveItem(itemId, DEV_ACTOR, comment);
          break;
        case "reject":
          await rejectItem(itemId, DEV_ACTOR, comment, selectedReasons);
          break;
        case "request-changes":
          await requestChanges(itemId, DEV_ACTOR, comment, selectedReasons);
          break;
        case "archive":
          await archiveItem(itemId, DEV_ACTOR, comment);
          break;
      }
      setActiveAction(null);
      setComment("");
      setSelectedReasons([]);
      onComplete();
    } catch (e) {
      setSubmitError(e instanceof Error ? e.message : "Action failed");
    } finally {
      setSubmitting(false);
    }
  }

  const actions = [
    {
      key: "approve" as ActiveAction,
      label: "APPROVE",
      icon: CheckCircle2,
      color:
        "text-[#00FF9C] border-[#00FF9C]/20 hover:bg-[#00FF9C]/5 hover:border-[#00FF9C]/40",
    },
    {
      key: "reject" as ActiveAction,
      label: "REJECT",
      icon: XCircle,
      color:
        "text-red-400 border-red-400/20 hover:bg-red-400/5 hover:border-red-400/40",
    },
    {
      key: "request-changes" as ActiveAction,
      label: "REQUEST CHANGES",
      icon: MessageSquare,
      color:
        "text-yellow-400 border-yellow-400/20 hover:bg-yellow-400/5 hover:border-yellow-400/40",
    },
    {
      key: "archive" as ActiveAction,
      label: "ARCHIVE",
      icon: Archive,
      color:
        "text-white/30 border-white/10 hover:bg-white/5 hover:border-white/20",
    },
  ];

  return (
    <div className="space-y-4">
      {/* Action buttons */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {actions.map(({ key, label, icon: Icon, color }) => (
          <button
            key={key}
            onClick={() => setActiveAction(activeAction === key ? null : key)}
            className={`flex items-center justify-center gap-2 px-4 py-3 border rounded-xl font-mono text-[9px] uppercase tracking-[0.2em] transition-all ${color} ${activeAction === key ? "ring-1 ring-current" : ""}`}
          >
            <Icon className="w-3.5 h-3.5" />
            {label}
          </button>
        ))}
      </div>

      {/* Expand panel */}
      {activeAction && (
        <div className="p-5 bg-white/[0.02] border border-white/5 rounded-2xl space-y-4">
          <div className="font-mono text-[9px] uppercase tracking-[0.3em] text-white/30">
            {activeAction.toUpperCase()} · CONFIRM ACTION
          </div>

          {/* Reasons (for reject and request-changes) */}
          {(activeAction === "reject" ||
            activeAction === "request-changes") && (
            <div className="space-y-2">
              <div className="font-mono text-[9px] uppercase tracking-[0.3em] text-white/20">
                Reasons (optional)
              </div>
              <div className="flex flex-wrap gap-2">
                {REJECT_REASONS.map((r) => (
                  <button
                    key={r}
                    onClick={() => toggleReason(r)}
                    className={`px-3 py-1.5 rounded-xl border text-[9px] font-mono uppercase tracking-[0.15em] transition-all ${
                      selectedReasons.includes(r)
                        ? "border-[#00FF9C]/40 text-[#00FF9C] bg-[#00FF9C]/5"
                        : "border-white/10 text-white/30 hover:border-white/20"
                    }`}
                  >
                    {r}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Comment */}
          <div className="space-y-2">
            <div className="font-mono text-[9px] uppercase tracking-[0.3em] text-white/20">
              Comment{" "}
              {activeAction === "reject" || activeAction === "request-changes"
                ? "(required)"
                : "(optional)"}
            </div>
            <textarea
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              rows={3}
              placeholder="Add a note for the audit trail..."
              className="w-full bg-white/[0.03] border border-white/10 rounded-xl px-4 py-3 text-sm text-white/80 placeholder:text-white/20 font-mono focus:outline-none focus:border-[#00FF9C]/40 resize-none"
            />
          </div>

          {/* Error */}
          {submitError && (
            <div className="flex items-center gap-2 text-red-400 text-xs font-mono">
              <AlertTriangle className="w-3.5 h-3.5 shrink-0" />
              {submitError}
            </div>
          )}

          {/* Submit */}
          <div className="flex justify-end gap-3">
            <button
              onClick={() => setActiveAction(null)}
              className="px-5 py-2.5 border border-white/10 rounded-xl font-mono text-[9px] uppercase tracking-[0.2em] text-white/30 hover:text-white/60 transition-all"
            >
              Cancel
            </button>
            <button
              onClick={handleSubmit}
              disabled={
                submitting ||
                ((activeAction === "reject" ||
                  activeAction === "request-changes") &&
                  !comment.trim())
              }
              className="px-6 py-2.5 bg-white text-black rounded-xl font-mono text-[9px] uppercase tracking-[0.2em] font-bold hover:bg-[#00FF9C] transition-all disabled:opacity-30 flex items-center gap-2"
            >
              {submitting && <Loader2 className="w-3 h-3 animate-spin" />}
              Confirm
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// History panel
// ---------------------------------------------------------------------------

function HistoryPanel({ history }: { history: ReviewHistoryItemDto[] }) {
  if (history.length === 0) {
    return (
      <p className="font-mono text-[10px] text-white/20 uppercase tracking-[0.3em]">
        No review history yet
      </p>
    );
  }
  return (
    <div className="space-y-3">
      {history.map((entry) => (
        <div
          key={entry.itemId + entry.timestamp}
          className="flex items-start gap-4"
        >
          <div className="w-1 h-1 rounded-full bg-[#00FF9C] mt-2 shrink-0" />
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-3 flex-wrap">
              <span className="font-mono text-[10px] uppercase tracking-[0.2em] text-white/60">
                {entry.action}
              </span>
              <span className="font-mono text-[9px] text-white/20">
                by {entry.actorId}
              </span>
              <span className="font-mono text-[9px] text-white/20 ml-auto">
                {formatTime(entry.timestamp)}
              </span>
            </div>
            <div className="font-mono text-[9px] text-white/20 mt-1">
              {entry.previousStatus} → {entry.nextStatus}
            </div>
            {entry.note && (
              <div className="mt-1 text-xs text-white/40 italic">
                {entry.note}
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Evidence panel
// ---------------------------------------------------------------------------

function EvidencePanel({ evidence }: { evidence: ModerationEvidenceBundle }) {
  return (
    <div className="space-y-5">
      {/* Confidence vector */}
      <div className="space-y-2">
        <div className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em]">
          Confidence Vector
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
          {(
            [
              ["EXTRACTION", evidence.confidence.extraction],
              ["GEOCODE", evidence.confidence.geocode],
              ["TEMPORAL", evidence.confidence.temporal],
              ["VENUE", evidence.confidence.venueMatch],
              ["DUPE_RISK", evidence.confidence.dupeRisk],
              ["AGGREGATE", evidence.confidence.aggregate],
              ["REVIEW", evidence.confidence.reviewConfidence],
            ] as Array<[string, number]>
          ).map(([label, val]) => {
            const pct = Math.round(val * 100);
            const color =
              val >= 0.85 ? "#00FF9C" : val >= 0.55 ? "#facc15" : "#f87171";
            return (
              <div key={label} className="flex items-center gap-2">
                <div className="font-mono text-[9px] text-white/20 uppercase w-20 shrink-0">
                  {label}
                </div>
                <div className="flex-1 h-1 bg-white/10 rounded-full overflow-hidden">
                  <div
                    className="h-full rounded-full"
                    style={{ width: `${pct}%`, backgroundColor: color }}
                  />
                </div>
                <span className="font-mono text-[9px] text-white/40 w-7 text-right">
                  {pct}%
                </span>
              </div>
            );
          })}
        </div>
      </div>

      {/* Blockers */}
      {evidence.blockerReasons.length > 0 && (
        <div className="space-y-2">
          <div className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em]">
            Blockers
          </div>
          <div className="flex flex-wrap gap-2">
            {evidence.blockerReasons.map((b) => (
              <span
                key={b}
                className="px-3 py-1 bg-red-400/10 border border-red-400/20 rounded-xl text-[9px] font-mono text-red-300 uppercase"
              >
                {b}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* OCR */}
      {evidence.ocrText && (
        <div className="space-y-2">
          <div className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em]">
            OCR Text
          </div>
          <pre className="text-xs text-white/40 font-mono whitespace-pre-wrap break-words leading-relaxed max-h-48 overflow-y-auto scrollbar-thin p-3 bg-black/30 rounded-xl">
            {evidence.ocrText}
          </pre>
        </div>
      )}

      {/* Resolution explanation */}
      {evidence.resolutionExplanation && (
        <div className="space-y-2">
          <div className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em]">
            Resolution Explanation
          </div>
          <p className="text-sm text-white/40 leading-relaxed">
            {evidence.resolutionExplanation}
          </p>
        </div>
      )}

      {/* Source refs */}
      {evidence.sourceRefs.length > 0 && (
        <div className="space-y-2">
          <div className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em]">
            Source Refs
          </div>
          <div className="space-y-1">
            {evidence.sourceRefs.map((ref) => (
              <div
                key={ref}
                className="font-mono text-[9px] text-white/30 break-all"
              >
                {ref}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default function ModerationItemPage() {
  const params = useParams();
  const router = useRouter();
  const itemId = params.id as string;

  const [item, setItem] = useState<ModerationQueueItemDto | null>(null);
  const [evidence, setEvidence] = useState<ModerationEvidenceBundle | null>(
    null,
  );
  const [history, setHistory] = useState<ReviewHistoryItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [itemRes, historyRes] = await Promise.all([
        getModerationItem(itemId),
        getModerationHistory(itemId),
      ]);
      setItem(itemRes);
      setHistory(historyRes.items);

      // Load evidence separately (may not be available for all item kinds)
      if (itemRes.evidenceAvailable) {
        getModerationEvidence(itemId)
          .then(setEvidence)
          .catch(() => {
            /* evidence unavailable is non-fatal */
          });
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load item");
    } finally {
      setLoading(false);
    }
  }, [itemId]);

  useEffect(() => {
    load();
  }, [load]);

  if (loading) {
    return (
      <main className="min-h-screen bg-[#050505] flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-[#00FF9C] animate-spin" />
      </main>
    );
  }

  if (error || !item) {
    return (
      <main className="min-h-screen bg-[#050505] text-white flex items-center justify-center p-10">
        <div className="space-y-4 text-center">
          <XCircle className="w-12 h-12 text-red-400/40 mx-auto" />
          <p className="font-mono text-xs text-white/20 uppercase tracking-[0.3em]">
            {error ?? "Item not found"}
          </p>
          <button
            onClick={() => router.push("/moderation")}
            className="px-6 py-3 border border-white/10 rounded-xl font-mono text-[10px] uppercase tracking-[0.3em] text-white/40 hover:text-white transition-all"
          >
            Back to Queue
          </button>
        </div>
      </main>
    );
  }

  const candidate = item.candidate;
  const bucket = item.confidenceSummary?.bucket ?? "Low";

  return (
    <main className="min-h-screen bg-[#050505] text-white pb-20">
      {/* Header */}
      <header className="sticky top-0 z-50 px-6 py-4 bg-[#050505]/80 backdrop-blur-3xl border-b border-white/5">
        <div className="max-w-5xl mx-auto flex items-center gap-4">
          <button
            onClick={() => router.push("/moderation")}
            className="w-10 h-10 rounded-full border border-white/10 flex items-center justify-center text-white/40 hover:text-white hover:border-white/30 transition-all"
          >
            <ArrowLeft className="w-4 h-4" />
          </button>
          <div className="flex-1 min-w-0">
            <h1 className="text-sm font-black uppercase italic tracking-tight text-white truncate">
              {candidate?.title ?? item.itemId}
            </h1>
            <div className="flex items-center gap-3 mt-0.5">
              <span className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em]">
                {item.kind}
              </span>
              <span
                className={`font-mono text-[9px] uppercase tracking-[0.2em] ${bucketColor(bucket)}`}
              >
                {confidencePct(item.confidence)} conf
              </span>
              <span className="font-mono text-[9px] text-white/20">·</span>
              <span className="font-mono text-[9px] text-white/30 uppercase">
                {item.status}
              </span>
            </div>
          </div>
          <div className="shrink-0 flex items-center gap-2">
            {item.urgency === "High" || item.urgency === "Critical" ? (
              <div className="flex items-center gap-1.5 px-3 py-1 bg-red-400/10 border border-red-400/20 rounded-full">
                <AlertTriangle className="w-3 h-3 text-red-400" />
                <span className="font-mono text-[9px] text-red-300 uppercase">
                  {item.urgency}
                </span>
              </div>
            ) : null}
          </div>
        </div>
      </header>

      <div className="max-w-5xl mx-auto px-6 py-8 space-y-6">
        {/* Candidate snapshot */}
        {candidate && (
          <Section title="Event Details" icon={ShieldCheck}>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4">
              {[
                ["Title", candidate.title],
                ["Venue", candidate.venueName],
                ["Address", candidate.address],
                ["Category", candidate.category],
                [
                  "Start",
                  candidate.startUtc ? formatTime(candidate.startUtc) : null,
                ],
                ["End", candidate.endUtc ? formatTime(candidate.endUtc) : null],
                ["Timezone", candidate.timezone],
                ["Source", `${candidate.sourceKind} · ${candidate.sourceRef}`],
              ]
                .filter(([, v]) => v)
                .map(([label, value]) => (
                  <div key={label}>
                    <div className="font-mono text-[9px] uppercase tracking-[0.3em] text-white/20 mb-1">
                      {label}
                    </div>
                    <div className="text-sm text-white/70">{value}</div>
                  </div>
                ))}
              {candidate.description && (
                <div className="sm:col-span-2">
                  <div className="font-mono text-[9px] uppercase tracking-[0.3em] text-white/20 mb-1">
                    Description
                  </div>
                  <p className="text-sm text-white/50 leading-relaxed">
                    {candidate.description}
                  </p>
                </div>
              )}
              {candidate.tags && candidate.tags.length > 0 && (
                <div className="sm:col-span-2">
                  <div className="font-mono text-[9px] uppercase tracking-[0.3em] text-white/20 mb-2">
                    Tags
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {candidate.tags.map((t) => (
                      <span
                        key={t}
                        className="px-3 py-1 bg-white/5 border border-white/10 rounded-full font-mono text-[9px] text-white/40 uppercase"
                      >
                        {t}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </Section>
        )}

        {/* Confidence summary */}
        <Section title="Confidence Analysis" icon={BarChart3Icon}>
          <ConfidenceGrid c={item.confidenceSummary} />
          {item.confidenceSummary.reviewBlockers.length > 0 && (
            <div className="mt-4 space-y-2">
              <div className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em]">
                Review Blockers
              </div>
              <div className="flex flex-wrap gap-2">
                {item.confidenceSummary.reviewBlockers.map((b) => (
                  <span
                    key={b}
                    className="px-3 py-1 bg-red-400/10 border border-red-400/20 rounded-xl text-[9px] font-mono text-red-300 uppercase"
                  >
                    {b}
                  </span>
                ))}
              </div>
            </div>
          )}
        </Section>

        {/* Dedupe match */}
        {item.dedupeMatch && item.dedupeMatch.severity !== "None" && (
          <Section title="Dedupe Match" icon={AlertTriangle} defaultOpen={true}>
            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <div className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em] mb-1">
                    Existing Event
                  </div>
                  <div className="text-sm text-white/70">
                    {item.dedupeMatch.existingEventTitle ??
                      item.dedupeMatch.existingEventId}
                  </div>
                </div>
                <div>
                  <div className="font-mono text-[9px] text-white/20 uppercase tracking-[0.3em] mb-1">
                    Match Score
                  </div>
                  <div className="text-sm font-mono text-yellow-400">
                    {confidencePct(item.dedupeMatch.matchScore)} ·{" "}
                    {item.dedupeMatch.severity}
                  </div>
                </div>
              </div>
              {item.dedupeMatch.matchReasons.length > 0 && (
                <div className="flex flex-wrap gap-2">
                  {item.dedupeMatch.matchReasons.map((r) => (
                    <span
                      key={r}
                      className="px-3 py-1 bg-yellow-400/10 border border-yellow-400/20 rounded-xl text-[9px] font-mono text-yellow-300 uppercase"
                    >
                      {r}
                    </span>
                  ))}
                </div>
              )}
            </div>
          </Section>
        )}

        {/* Evidence */}
        {evidence && (
          <Section title="Evidence Bundle" icon={FileText} defaultOpen={false}>
            <EvidencePanel evidence={evidence} />
          </Section>
        )}

        {/* Review reasons */}
        {item.reviewReasons.length > 0 && (
          <Section title="Review Reasons" icon={Clock} defaultOpen={false}>
            <div className="flex flex-wrap gap-2">
              {item.reviewReasons.map((r) => (
                <span
                  key={r}
                  className="px-3 py-1.5 bg-white/5 border border-white/10 rounded-xl text-[9px] font-mono text-white/40 uppercase tracking-[0.15em]"
                >
                  {r}
                </span>
              ))}
            </div>
          </Section>
        )}

        {/* Action panel — only for open/in-review items */}
        {(item.status === "Open" || item.status === "InReview") && (
          <Section title="Review Actions" icon={ShieldCheck}>
            <ActionPanel itemId={item.itemId} onComplete={load} />
          </Section>
        )}

        {/* History */}
        <Section title="Audit History" icon={History} defaultOpen={false}>
          <HistoryPanel history={history} />
        </Section>
      </div>
    </main>
  );
}

// Lucide doesn't export BarChart3 under that name in all versions — inline safe alias
function BarChart3Icon({ className }: { className?: string }) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
    >
      <line x1="18" x2="18" y1="20" y2="10" />
      <line x1="12" x2="12" y1="20" y2="4" />
      <line x1="6" x2="6" y1="20" y2="14" />
    </svg>
  );
}
