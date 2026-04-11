"use client";

import React, { useMemo } from "react";
import { useParams, useRouter } from "next/navigation";
import { motion } from "motion/react";
import {
  ArrowLeft,
  MapPin,
  Clock,
  Calendar,
  Bookmark,
  Navigation,
  Share2,
  Activity,
  Info,
  AlertCircle,
} from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import EventDetailActions from "@/components/EventDetailActions";
import { SEEDED_EVENTS } from "@/lib/testing/phase0Seed";

export default function EventPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const event = useMemo(() => SEEDED_EVENTS.find((e) => e.id === id), [id]);

  const [isClient, setIsClient] = React.useState(false);
  React.useEffect(() => {
    const timer = setTimeout(() => setIsClient(true), 0);
    return () => clearTimeout(timer);
  }, []);

  if (!event) {
    return (
      <main className="min-h-screen bg-[#050505] text-white flex items-center justify-center p-10">
        <div className="max-w-md w-full space-y-8 text-center">
          <div className="w-24 h-24 rounded-full bg-white/5 border border-white/10 flex items-center justify-center mx-auto">
            <AlertCircle className="w-12 h-12 text-white/20" />
          </div>
          <div className="space-y-4">
            <h1 className="text-4xl font-black uppercase italic tracking-tighter">
              Signal Lost
            </h1>
            <p className="text-white/40 font-mono text-xs uppercase tracking-[0.3em] leading-relaxed">
              The requested event signal could not be located in the current
              sector. It may have been decommissioned or moved to a restricted
              frequency.
            </p>
          </div>
          <Link
            href="/"
            className="inline-flex items-center justify-center w-full h-16 bg-white text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] hover:scale-[1.02] active:scale-[0.98] transition-all"
          >
            RETURN_TO_RADAR
          </Link>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-[#050505] text-white selection:bg-[#00FF9C] selection:text-black pb-32">
      {/* Navigation HUD */}
      <nav className="fixed top-0 inset-x-0 z-[100] p-8 pointer-events-none">
        <div className="max-w-7xl mx-auto flex items-center justify-between pointer-events-auto">
          <button
            onClick={() => router.back()}
            className="w-14 h-14 rounded-full bg-black/40 backdrop-blur-3xl border border-white/10 flex items-center justify-center text-white hover:bg-white/10 transition-all hover:scale-110 active:scale-90 group"
          >
            <ArrowLeft className="w-6 h-6 group-hover:text-[#00FF9C] transition-colors" />
          </button>

          <div className="flex items-center gap-3 px-6 py-3 bg-black/40 backdrop-blur-3xl border border-white/10 rounded-full font-mono text-[10px] tracking-[0.4em] uppercase font-bold text-[#00FF9C]">
            <div className="w-2 h-2 rounded-full bg-[#00FF9C] animate-pulse shadow-[0_0_10px_#00FF9C]" />
            LIVE_SIGNAL
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <div className="relative w-full h-[85vh] overflow-hidden">
        <Image
          src={event.image_url}
          alt={event.title}
          fill
          className="object-cover opacity-40 scale-105 blur-[2px]"
          priority
          referrerPolicy="no-referrer"
        />
        <div className="absolute inset-0 bg-gradient-to-t from-[#050505] via-[#050505]/60 to-transparent" />

        <div className="absolute inset-0 flex items-center justify-center px-10">
          <div className="max-w-7xl w-full">
            <motion.div
              initial={{ opacity: 0, y: 60 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 1, ease: [0.23, 1, 0.32, 1] }}
              className="space-y-8 text-center md:text-left"
            >
              <div className="flex items-center justify-center md:justify-start gap-4 text-white/20 font-mono text-[11px] tracking-[0.5em] font-bold">
                <span className="uppercase">INTELLIGENCE_REPORT</span>
                <div className="h-px w-24 bg-white/10 hidden md:block" />
              </div>

              <h1 className="text-6xl sm:text-8xl md:text-[10rem] font-black tracking-tighter uppercase italic leading-[0.8] text-white drop-shadow-2xl">
                {event.title}
              </h1>

              <div className="flex flex-wrap justify-center md:justify-start gap-3">
                {event.tags?.map((tag) => (
                  <span
                    key={tag}
                    className="px-6 py-2 rounded-full bg-white/5 border border-white/10 text-[10px] font-mono uppercase tracking-[0.3em] text-white/40"
                  >
                    {tag}
                  </span>
                ))}
              </div>
            </motion.div>
          </div>
        </div>
      </div>

      {/* Content Section */}
      <div className="max-w-7xl mx-auto px-10 -mt-20 relative z-10 grid grid-cols-1 lg:grid-cols-3 gap-20">
        <div className="lg:col-span-2 space-y-20">
          {/* Intelligence Grid */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-y-16 gap-x-20 p-12 bg-white/[0.02] backdrop-blur-3xl border border-white/5 rounded-[3rem]">
            <div className="space-y-4">
              <div className="flex items-center gap-2 text-[10px] font-mono text-white/20 uppercase tracking-[0.4em]">
                <MapPin className="w-3 h-3 text-[#00FF9C]" />
                <span>LOCATION_DATA</span>
              </div>
              <div className="space-y-2">
                <div className="text-4xl font-black uppercase italic tracking-tight text-white leading-none">
                  {event.venue_name}
                </div>
                <div className="space-y-1">
                  <p className="text-[#00FF9C]/60 font-mono text-[11px] uppercase tracking-widest font-bold">
                    {event.neighborhood || "HOUSTON"}
                  </p>
                  <p className="text-white/30 font-mono text-[10px] uppercase tracking-widest">
                    {event.address}
                  </p>
                </div>
              </div>
            </div>

            <div className="space-y-4">
              <div className="flex items-center gap-2 text-[10px] font-mono text-white/20 uppercase tracking-[0.4em]">
                <Clock className="w-3 h-3 text-[#00FF9C]" />
                <span>TEMPORAL_WINDOW</span>
              </div>
              <div className="space-y-2">
                <div className="text-4xl font-black uppercase italic tracking-tight text-white leading-none">
                  {new Date(event.start_time)
                    .toLocaleDateString([], { month: "short", day: "numeric" })
                    .toUpperCase()}
                </div>
                <div className="text-white/40 font-mono text-[11px] uppercase tracking-widest">
                  {isClient &&
                    new Date(event.start_time).toLocaleTimeString([], {
                      hour: "2-digit",
                      minute: "2-digit",
                      hour12: false,
                    })}
                </div>
              </div>
            </div>

            <div className="space-y-4 sm:col-span-2">
              <div className="flex items-center gap-2 text-[10px] font-mono text-white/20 uppercase tracking-[0.4em]">
                <Activity className="w-3 h-3 text-[#00FF9C]" />
                <span>ENERGY_ANALYSIS</span>
              </div>
              <div className="flex items-end gap-2 h-12">
                {[...Array(10)].map((_, i) => (
                  <div
                    key={i}
                    className={`flex-1 rounded-sm transition-all duration-1000 ${i < (event.energyLevel || 4) ? "bg-[#00FF9C] shadow-[0_0_20px_rgba(0,255,156,0.4)]" : "bg-white/5"}`}
                    style={{ height: `${(i + 1) * 10}%` }}
                  />
                ))}
              </div>
            </div>
          </div>

          {/* Briefing */}
          <div className="space-y-8 px-4">
            <div className="flex items-center gap-2 text-[10px] font-mono text-white/20 uppercase tracking-[0.4em]">
              <Info className="w-3 h-3" />
              <span>MISSION_BRIEFING</span>
            </div>
            <p className="text-white/70 leading-relaxed text-3xl md:text-4xl italic font-medium max-w-4xl">
              {event.description}
            </p>
          </div>
        </div>

        {/* Sidebar Actions */}
        <div className="space-y-8">
          <div className="sticky top-32 bg-white/[0.03] border border-white/10 rounded-[3rem] p-10 space-y-10 backdrop-blur-3xl shadow-2xl">
            <div className="text-[10px] font-mono text-white/20 uppercase tracking-[0.4em]">
              ACTION_INTERFACE
            </div>

            <EventDetailActions eventId={event.id} />

            <div className="pt-6 border-t border-white/5">
              <div className="text-[9px] font-mono text-white/20 uppercase tracking-widest mb-4">
                SIGNAL_STRENGTH
              </div>
              <div className="w-full h-1.5 bg-white/5 rounded-full overflow-hidden">
                <motion.div
                  initial={{ width: 0 }}
                  animate={{ width: "85%" }}
                  className="h-full bg-[#00FF9C]"
                />
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
}
