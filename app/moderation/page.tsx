"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import {
  ShieldCheck,
  AlertTriangle,
  Clock,
  CheckCircle2,
  XCircle,
  RefreshCw,
  Filter,
  ChevronRight,
  BarChart3,
} from "lucide-react";
import {
  getModerationQueue,
  getModerationStats,
  type ConfidenceBucket,
  type ModerationItemKind,
  type ModerationItemStatus,
  type ModerationQueueFilters,
  type ModerationQueueItemDto,
  type ModerationStatsDto,
} from "@/services/moderationService";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const KIND_LABEL: Record<ModerationItemKind, string> = {
  CandidateReview: "CANDIDATE",
  DedupeReview: "DEDUPE",
  IngestionFailure: "INGEST_FAIL",
  PublishBlocked: "PUB_BLOCKED",
};

const STATUS_COLORS: Record<ModerationItemStatus, string> = {
  Open: "text-[#00FF9C]",
  InReview: "text-yellow-400",
  Resolved: "text-white/40",
  Closed: "text-white/20",
};

const BUCKET_COLORS: Record<ConfidenceBucket, string> = {
  High: "bg-[#00FF9C]/10 text-[#00FF9C]",
  Medium: "bg-yellow-400/10 text-yellow-400",
  Low: "bg-red-400/10 text-red-400",
};

function confidenceBar(value: number) {
  const pct = Math.round(value * 100);
  const color =
    value >= 0.85 ? "#00FF9C" : value >= 0.55 ? "#facc15" : "#f87171";
  return (
    <div className="flex items-center gap-2">
      <div className="w-20 h-1 bg-white/10 rounded-full overflow-hidden">
        <div
          className="h-full rounded-full transition-all"
          style={{ width: `${pct}%`, backgroundColor: color }}
        />
      </div>
      <span className="font-mono text-[10px] text-white/40">{pct}%</span>
    </div>
  );
}

function formatTime(iso: string) {
  try {
    return new Date(iso).toLocaleString("en-US", {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return iso;
  }
}

// ---------------------------------------------------------------------------
// Stats bar
// ---------------------------------------------------------------------------

function StatsBar({ stats }: { stats: ModerationStatsDto }) {
  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
      {[
        {
          label: "OPEN",
          value: stats.totalOpen,
          icon: AlertTriangle,
          color: "text-[#00FF9C]",
        },
        {
          label: "IN_REVIEW",
          value: stats.totalInReview,
          icon: Clock,
          color: "text-yellow-400",
        },
        {
          label: "RESOLVED",
          value: stats.totalResolved,
          icon: CheckCircle2,
          color: "text-white/40",
        },
        {
          label: "HIGH_CONF",
          value: stats.highConfidenceOpen,
          icon: BarChart3,
          color: "text-blue-400",
        },
      ].map(({ label, value, icon: Icon, color }) => (
        <div
          key={label}
          className="p-5 bg-white/[0.02] border border-white/5 rounded-2xl flex items-center gap-4"
        >
          <Icon className={`w-5 h-5 shrink-0 ${color}`} />
          <div>
            <div className="font-mono text-[9px] text-white/20 tracking-[0.3em] uppercase">
              {label}
            </div>
            <div className="text-2xl font-black text-white">{value}</div>
          </div>
        </div>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Queue item row
// ---------------------------------------------------------------------------

function QueueRow({ item }: { item: ModerationQueueItemDto }) {
  const title =
    item.candidate?.title ??
    item.ingestionJob?.jobId ??
    item.itemId.slice(0, 8);
  const bucket = item.confidenceSummary?.bucket ?? "Low";

  return (
    <Link href={`/moderation/${item.itemId}`}>
      <div className="group flex items-center gap-4 px-6 py-5 bg-white/[0.02] border border-white/5 rounded-2xl hover:border-white/10 hover:bg-white/[0.04] transition-all cursor-pointer">
        {/* Kind badge */}
        <div className="shrink-0 w-28 font-mono text-[9px] tracking-[0.3em] text-white/30 uppercase">
          {KIND_LABEL[item.kind]}
        </div>

        {/* Title */}
        <div className="flex-1 min-w-0">
          <div className="text-sm font-bold text-white truncate group-hover:text-[#00FF9C] transition-colors">
            {title}
          </div>
          {item.candidate?.venueName && (
            <div className="text-[10px] font-mono text-white/30 uppercase truncate mt-0.5">
              {item.candidate.venueName}
            </div>
          )}
        </div>

        {/* Confidence */}
        <div className="shrink-0 hidden sm:block">
          {confidenceBar(item.confidence)}
        </div>

        {/* Bucket */}
        <div
          className={`shrink-0 px-3 py-1 rounded-full text-[9px] font-mono uppercase tracking-[0.2em] ${BUCKET_COLORS[bucket]}`}
        >
          {bucket}
        </div>

        {/* Status */}
        <div
          className={`shrink-0 font-mono text-[10px] uppercase tracking-[0.2em] w-20 text-right ${STATUS_COLORS[item.status]}`}
        >
          {item.status}
        </div>

        {/* Time */}
        <div className="shrink-0 hidden md:block text-[10px] font-mono text-white/20 w-32 text-right">
          {formatTime(item.createdAt)}
        </div>

        <ChevronRight className="shrink-0 w-4 h-4 text-white/20 group-hover:text-white/60 transition-colors" />
      </div>
    </Link>
  );
}

// ---------------------------------------------------------------------------
// Filters toolbar
// ---------------------------------------------------------------------------

const STATUS_OPTIONS: Array<{
  value: ModerationItemStatus | "";
  label: string;
}> = [
  { value: "", label: "ALL STATUS" },
  { value: "Open", label: "OPEN" },
  { value: "InReview", label: "IN REVIEW" },
  { value: "Resolved", label: "RESOLVED" },
  { value: "Closed", label: "CLOSED" },
];

const KIND_OPTIONS: Array<{ value: ModerationItemKind | ""; label: string }> = [
  { value: "", label: "ALL KINDS" },
  { value: "CandidateReview", label: "CANDIDATE" },
  { value: "DedupeReview", label: "DEDUPE" },
  { value: "IngestionFailure", label: "INGEST FAIL" },
  { value: "PublishBlocked", label: "PUB BLOCKED" },
];

const BUCKET_OPTIONS: Array<{ value: ConfidenceBucket | ""; label: string }> = [
  { value: "", label: "ALL CONFIDENCE" },
  { value: "High", label: "HIGH" },
  { value: "Medium", label: "MEDIUM" },
  { value: "Low", label: "LOW" },
];

function Select({
  value,
  onChange,
  options,
}: {
  value: string;
  onChange: (v: string) => void;
  options: Array<{ value: string; label: string }>;
}) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="bg-white/[0.03] border border-white/10 text-white/60 text-[10px] font-mono uppercase tracking-[0.2em] rounded-xl px-3 py-2 focus:outline-none focus:border-[#00FF9C]/40"
    >
      {options.map((o) => (
        <option key={o.value} value={o.value} className="bg-[#0a0a0a]">
          {o.label}
        </option>
      ))}
    </select>
  );
}

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default function ModerationQueuePage() {
  const [filters, setFilters] = useState<ModerationQueueFilters>({
    pageSize: 20,
  });
  const [items, setItems] = useState<ModerationQueueItemDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [stats, setStats] = useState<ModerationStatsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (overrideFilters?: ModerationQueueFilters) => {
      const active = overrideFilters ?? filters;
      setLoading(true);
      setError(null);
      try {
        const [queueRes, statsRes] = await Promise.all([
          getModerationQueue(active),
          getModerationStats(),
        ]);
        setItems(queueRes.items);
        setTotalCount(queueRes.totalCount);
        setNextCursor(queueRes.nextCursor ?? null);
        setStats(statsRes);
      } catch (e) {
        setError(
          e instanceof Error ? e.message : "Failed to load moderation queue",
        );
      } finally {
        setLoading(false);
      }
    },
    [filters],
  );

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function applyFilter(partial: Partial<ModerationQueueFilters>) {
    const next = { ...filters, ...partial, cursor: undefined };
    setFilters(next);
    load(next);
  }

  function loadNextPage() {
    if (!nextCursor) return;
    const next = { ...filters, cursor: nextCursor };
    setFilters(next);
    load(next);
  }

  return (
    <main className="min-h-screen bg-[#050505] text-white pb-20">
      {/* Header */}
      <header className="sticky top-0 z-50 px-6 py-5 bg-[#050505]/80 backdrop-blur-3xl border-b border-white/5">
        <div className="max-w-7xl mx-auto flex items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <ShieldCheck className="w-6 h-6 text-[#00FF9C]" />
            <div>
              <h1 className="text-lg font-black uppercase italic tracking-tight text-white">
                Moderation Queue
              </h1>
              <p className="text-[9px] font-mono text-white/20 tracking-[0.4em] uppercase">
                M3-P15 · Review Control Surface
              </p>
            </div>
          </div>
          <button
            onClick={() => load()}
            disabled={loading}
            className="w-10 h-10 rounded-full border border-white/10 flex items-center justify-center text-white/40 hover:text-[#00FF9C] hover:border-[#00FF9C]/30 transition-all disabled:opacity-30"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} />
          </button>
        </div>
      </header>

      <div className="max-w-7xl mx-auto px-6 py-8 space-y-8">
        {/* Stats */}
        {stats && <StatsBar stats={stats} />}

        {/* Filters */}
        <div className="flex flex-wrap items-center gap-3">
          <Filter className="w-3 h-3 text-white/20 shrink-0" />
          <Select
            value={filters.status ?? ""}
            onChange={(v) =>
              applyFilter({ status: (v as ModerationItemStatus) || undefined })
            }
            options={STATUS_OPTIONS}
          />
          <Select
            value={filters.kind ?? ""}
            onChange={(v) =>
              applyFilter({ kind: (v as ModerationItemKind) || undefined })
            }
            options={KIND_OPTIONS}
          />
          <Select
            value={filters.confidenceBucket ?? ""}
            onChange={(v) =>
              applyFilter({
                confidenceBucket: (v as ConfidenceBucket) || undefined,
              })
            }
            options={BUCKET_OPTIONS}
          />
          <span className="ml-auto font-mono text-[10px] text-white/20">
            {totalCount} ITEM{totalCount !== 1 ? "S" : ""}
          </span>
        </div>

        {/* Error */}
        {error && (
          <div className="flex items-center gap-3 px-5 py-4 bg-red-400/5 border border-red-400/20 rounded-2xl">
            <XCircle className="w-4 h-4 text-red-400 shrink-0" />
            <span className="text-sm text-red-300 font-mono">{error}</span>
          </div>
        )}

        {/* List */}
        {loading && items.length === 0 ? (
          <div className="space-y-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <div
                key={i}
                className="h-[68px] bg-white/[0.02] border border-white/5 rounded-2xl animate-pulse"
              />
            ))}
          </div>
        ) : items.length === 0 ? (
          <div className="text-center py-20 space-y-4">
            <CheckCircle2 className="w-12 h-12 text-white/10 mx-auto" />
            <p className="font-mono text-xs text-white/20 uppercase tracking-[0.3em]">
              Queue Empty
            </p>
          </div>
        ) : (
          <div className="space-y-2">
            {items.map((item) => (
              <QueueRow key={item.itemId} item={item} />
            ))}
          </div>
        )}

        {/* Pagination */}
        {nextCursor && (
          <div className="flex justify-center">
            <button
              onClick={loadNextPage}
              disabled={loading}
              className="px-8 py-3 border border-white/10 rounded-xl font-mono text-[10px] uppercase tracking-[0.3em] text-white/40 hover:text-[#00FF9C] hover:border-[#00FF9C]/30 transition-all disabled:opacity-30"
            >
              Load More
            </button>
          </div>
        )}
      </div>
    </main>
  );
}
