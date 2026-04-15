"use client";

import React, { useMemo, useEffect, useState } from "react";
import { motion, AnimatePresence } from "motion/react";
import Image from "next/image";
import { EventSignalState } from "@/types";
import type { RuntimeEventProjection } from "@/features/world/runtimeTypes";
import {
  X,
  MapPin,
  Clock,
  Calendar,
  Zap,
  Bookmark,
  Navigation,
  Share2,
  Info,
  Activity,
  ArrowRight,
  Sparkles,
  ChevronUp,
  Heart,
  ExternalLink,
  Map as MapIcon,
} from "lucide-react";

interface EventSignalModalProps {
  event: RuntimeEventProjection | null;
  state: EventSignalState;
  onClose: () => void;
  onSave?: (id: string) => void;
  isSaved?: boolean;
  anchorPoint?: { x: number; y: number } | null;
}

const CATEGORY_CONFIG: Record<
  string,
  { color: string; glow: string; signalType: string }
> = {
  nightlife: {
    color: "text-brand-primary",
    glow: "shadow-brand-primary/20",
    signalType: "bloom",
  },
  tech: {
    color: "text-cyan-400",
    glow: "shadow-cyan-400/20",
    signalType: "angular",
  },
  culture: {
    color: "text-purple-400",
    glow: "shadow-purple-400/20",
    signalType: "radiant",
  },
  wellness: {
    color: "text-emerald-400",
    glow: "shadow-emerald-400/20",
    signalType: "waveform",
  },
  default: {
    color: "text-white",
    glow: "shadow-white/20",
    signalType: "bloom",
  },
};

// Signal Background Elements based on category
const SignalBackground = ({ type, color }: { type: string; color: string }) => {
  const glowColor = color.replace("text-", "bg-");

  switch (type) {
    case "angular":
      return (
        <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-20">
          <div
            className={`absolute top-0 left-0 w-full h-full border-[0.5px] ${color.replace("text-", "border-")}/10 rotate-45 translate-x-1/2 -translate-y-1/2`}
          />
          <div
            className={`absolute bottom-0 right-0 w-full h-full border-[0.5px] ${color.replace("text-", "border-")}/10 -rotate-45 -translate-x-1/2 translate-y-1/2`}
          />
          <div
            className={`absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-64 h-64 ${glowColor}/5 blur-3xl rounded-full`}
          />
        </div>
      );
    case "radiant":
      return (
        <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-20">
          {[...Array(12)].map((_, i) => (
            <div
              key={i}
              className={`absolute top-1/2 left-1/2 w-[200%] h-[1px] bg-gradient-to-r from-transparent via-${color.replace("text-", "")}/20 to-transparent`}
              style={{
                transform: `translate(-50%, -50%) rotate(${i * 15}deg)`,
              }}
            />
          ))}
          <div
            className={`absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-80 h-80 ${glowColor}/10 blur-[100px] rounded-full`}
          />
        </div>
      );
    case "waveform":
      return (
        <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-10">
          <svg
            viewBox="0 0 100 20"
            className={`absolute bottom-0 left-0 w-full h-32 ${color} fill-none stroke-current stroke-[0.5]`}
          >
            <motion.path
              d="M0 10 Q 25 0, 50 10 T 100 10"
              animate={{
                d: [
                  "M0 10 Q 25 0, 50 10 T 100 10",
                  "M0 10 Q 25 20, 50 10 T 100 10",
                  "M0 10 Q 25 0, 50 10 T 100 10",
                ],
              }}
              transition={{ duration: 4, repeat: Infinity, ease: "easeInOut" }}
            />
            <motion.path
              d="M0 15 Q 25 5, 50 15 T 100 15"
              animate={{
                d: [
                  "M0 15 Q 25 5, 50 15 T 100 15",
                  "M0 15 Q 25 25, 50 15 T 100 15",
                  "M0 15 Q 25 5, 50 15 T 100 15",
                ],
              }}
              transition={{
                duration: 6,
                repeat: Infinity,
                ease: "easeInOut",
                delay: 1,
              }}
            />
          </svg>
          <div
            className={`absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-full h-full ${glowColor}/5 blur-[120px] rounded-full`}
          />
        </div>
      );
    default: // bloom
      return (
        <div className="absolute inset-0 overflow-hidden pointer-events-none">
          <div
            className={`absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-full h-full rounded-full bg-gradient-radial from-${color.replace("text-", "")}/10 to-transparent blur-3xl`}
          />
        </div>
      );
  }
};

export default function EventSignalModal({
  event,
  state,
  onClose,
  onSave,
  isSaved,
  anchorPoint,
}: EventSignalModalProps) {
  const config = useMemo(() => {
    if (!event) return CATEGORY_CONFIG.default;
    const cat = event.category?.toLowerCase() || "default";
    return CATEGORY_CONFIG[cat] || CATEGORY_CONFIG.default;
  }, [event]);

  const [isClient, setIsClient] = useState(false);
  const [isCopied, setIsCopied] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setIsClient(true), 0);
    return () => clearTimeout(timer);
  }, []);

  const handleShare = () => {
    setIsCopied(true);
    setTimeout(() => setIsCopied(false), 2000);
    if (navigator.share) {
      navigator
        .share({
          title: event?.title,
          text: event?.description,
          url: window.location.href,
        })
        .catch(() => {});
    }
  };

  if (!event || !state) return null;

  const isFull = state === "FULL";

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className={`fixed inset-0 z-[500] pointer-events-none flex items-center justify-center ${isFull ? "bg-black/40 backdrop-blur-sm" : ""}`}
      >
        {/* Background Overlay for Full */}
        {isFull && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="absolute inset-0 pointer-events-auto"
            onClick={onClose}
          />
        )}

        <motion.div
          layoutId={`event-signal-${event.id}`}
          initial={
            anchorPoint
              ? {
                  x: anchorPoint.x - window.innerWidth / 2,
                  y: anchorPoint.y - window.innerHeight / 2,
                  scale: 0.5,
                  opacity: 0,
                }
              : { scale: 0.8, opacity: 0 }
          }
          animate={{
            x: 0,
            y: 0,
            scale: 1,
            opacity: 1,
            width: "100%",
            maxWidth: "900px",
            height: "auto",
            maxHeight: "90vh",
          }}
          transition={{
            type: "spring",
            damping: 25,
            stiffness: 200,
            mass: 1,
          }}
          className={`
            relative pointer-events-auto overflow-hidden
            bg-black/90 backdrop-blur-3xl border border-white/10
            shadow-[0_20px_60px_rgba(0,0,0,0.8)]
            rounded-[3rem] flex flex-col md:flex-row
          `}
        >
          {/* Luminous Edge */}
          <div
            className="absolute inset-0 pointer-events-none rounded-[inherit] border border-white/5"
            style={{
              boxShadow: `inset 0 0 20px ${config.color}15`,
              borderColor: `${config.color}30`,
            }}
          />

          <SignalBackground type={config.signalType} color={config.color} />

          {/* FULL DETAIL STATE */}
          {isFull && (
            <>
              {/* Media Section */}
              <div className="w-full md:w-1/2 h-[40vh] md:h-auto relative shrink-0">
                <Image
                  src={event.imageUrl}
                  alt={event.title}
                  fill
                  className="object-cover"
                  referrerPolicy="no-referrer"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-black md:bg-gradient-to-r md:from-transparent md:to-black/20" />

                {/* Category Badge */}
                <div className="absolute top-8 left-8">
                  <div className="px-4 py-2 rounded-full bg-black/60 backdrop-blur-xl border border-white/10 flex items-center gap-2">
                    <div
                      className={`w-2 h-2 rounded-full animate-pulse ${config.color.replace("text-", "bg-")}`}
                    />
                    <span className="text-[10px] font-mono font-black uppercase tracking-[0.2em] text-white/80">
                      {event.category}
                    </span>
                  </div>
                </div>
              </div>

              {/* Content Section */}
              <div className="flex-1 p-8 md:p-12 flex flex-col relative">
                <button
                  onClick={onClose}
                  className="absolute top-8 right-8 w-12 h-12 bg-white/5 rounded-full border border-white/10 flex items-center justify-center hover:bg-white/10 transition-all group"
                >
                  <X className="w-6 h-6 text-white/40 group-hover:text-white" />
                </button>

                <div className="flex-1 flex flex-col justify-center space-y-8">
                  <div className="space-y-4">
                    <h2 className="text-4xl md:text-6xl font-black tracking-tighter uppercase italic leading-[0.85] text-white">
                      {event.title}
                    </h2>

                    <div className="grid grid-cols-2 gap-6">
                      <div className="space-y-1">
                        <p className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                          LOCATION
                        </p>
                        <div className="flex items-center gap-2">
                          <MapPin size={12} className={config.color} />
                          <p className="text-xs font-black uppercase text-white/80">
                            {event.venueName}
                          </p>
                        </div>
                        <p className="text-[10px] text-white/40 ml-5 leading-tight">
                          {event.address || event.neighborhood || "DOWNTOWN"}
                        </p>
                      </div>
                      <div className="space-y-1">
                        <p className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                          TEMPORAL
                        </p>
                        <div className="flex items-center gap-2">
                          <Clock size={12} className={config.color} />
                          <p className="text-xs font-black uppercase text-white/80">
                            {isClient &&
                              new Date(event.startTime).toLocaleTimeString([], {
                                hour: "2-digit",
                                minute: "2-digit",
                                hour12: false,
                              })}
                          </p>
                        </div>
                        <p className="text-[10px] text-white/40 ml-5">
                          {new Date(event.startTime).toLocaleDateString([], {
                            weekday: "short",
                            month: "short",
                            day: "numeric",
                          })}
                        </p>
                      </div>
                    </div>
                  </div>

                  <div className="space-y-6">
                    <div className="flex flex-col gap-2">
                      <div className="flex items-center justify-between">
                        <p className="text-[9px] font-mono text-white/20 uppercase tracking-[0.3em] font-bold">
                          SIGNAL_INTENSITY
                        </p>
                        <span className="text-[10px] font-black text-brand-primary italic">
                          EXTREME
                        </span>
                      </div>
                      <div className="flex gap-1.5 h-2">
                        {[...Array(12)].map((_, i) => (
                          <motion.div
                            key={i}
                            initial={{ scaleY: 0.5, opacity: 0.3 }}
                            animate={{
                              scaleY: [0.5, 1, 0.5],
                              opacity: i < 8 ? 1 : 0.1,
                            }}
                            transition={{
                              duration: 1.5,
                              repeat: Infinity,
                              delay: i * 0.1,
                              ease: "easeInOut",
                            }}
                            className={`flex-1 rounded-full ${i < 8 ? config.color.replace("text-", "bg-") : "bg-white/10"}`}
                          />
                        ))}
                      </div>
                    </div>
                    <p className="text-sm md:text-lg text-white/60 leading-relaxed font-medium">
                      {event.description}
                    </p>
                    {event.tags && (
                      <div className="flex flex-wrap gap-2">
                        {event.tags.map((tag) => (
                          <span
                            key={tag}
                            className="px-3 py-1 rounded-full bg-white/5 border border-white/5 text-[8px] font-mono text-white/40 uppercase tracking-widest hover:text-white hover:bg-white/10 transition-colors cursor-default"
                          >
                            #{tag}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>

                  <div className="flex gap-3 pt-4">
                    <button
                      onClick={() => onSave?.(event.id)}
                      className={`flex-[2] h-16 rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] flex items-center justify-center gap-3 transition-all border ${
                        isSaved
                          ? "bg-brand-primary text-black border-brand-primary"
                          : "bg-white/5 text-white border-white/10 hover:bg-white/10"
                      }`}
                    >
                      <Zap
                        className={`w-4 h-4 ${isSaved ? "fill-black" : ""}`}
                      />
                      {isSaved ? "SIGNAL_LOCKED" : "SAVE_SIGNAL"}
                    </button>

                    <button className="flex-[2] h-16 bg-white text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] flex items-center justify-center gap-3 hover:bg-brand-primary transition-all active:scale-95">
                      <Navigation className="w-4 h-4" />
                      NAVIGATE
                    </button>

                    <button className="flex-1 h-16 bg-white/5 border border-white/10 rounded-2xl flex items-center justify-center text-white/40 hover:text-white transition-all">
                      <ExternalLink size={20} />
                    </button>

                    <button
                      onClick={handleShare}
                      className="relative flex-1 h-16 bg-white/5 border border-white/10 rounded-2xl flex items-center justify-center text-white/40 hover:text-white transition-all overflow-hidden"
                    >
                      <AnimatePresence mode="wait">
                        {isCopied ? (
                          <motion.span
                            key="copied"
                            initial={{ y: 20, opacity: 0 }}
                            animate={{ y: 0, opacity: 1 }}
                            exit={{ y: -20, opacity: 0 }}
                            className="text-[8px] font-mono font-black text-brand-primary"
                          >
                            LINK_COPIED
                          </motion.span>
                        ) : (
                          <motion.div
                            key="share"
                            initial={{ scale: 0.8, opacity: 0 }}
                            animate={{ scale: 1, opacity: 1 }}
                            exit={{ scale: 0.8, opacity: 0 }}
                          >
                            <Share2 size={20} />
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </button>
                  </div>
                </div>
              </div>
            </>
          )}
        </motion.div>
      </motion.div>
    </AnimatePresence>
  );
}
