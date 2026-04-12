"use client";

import React, { useMemo, useState } from "react";
import { motion, AnimatePresence } from "motion/react";
import {
  Calendar as CalendarIcon,
  ChevronLeft,
  ChevronRight,
  Zap,
  Clock,
} from "lucide-react";
import type { RuntimeEventProjection } from "@/features/world/runtimeTypes";
import TimelineControl from "./TimelineControl";

interface CulturalCalendarProps {
  isVisible: boolean;
  events: RuntimeEventProjection[];
  selectedDate: string;
  onDateSelect: (date: string) => void;
  onEventSelect: (event: RuntimeEventProjection) => void;
  onClose: () => void;
}

export default function CulturalCalendar({
  isVisible,
  events,
  selectedDate,
  onDateSelect,
  onEventSelect,
  onClose,
}: CulturalCalendarProps) {
  const dates = [
    { day: "MON", date: "MAR 23" },
    { day: "TUE", date: "MAR 24" },
    { day: "WED", date: "MAR 25" },
    { day: "THU", date: "MAR 26" },
    { day: "FRI", date: "MAR 27" },
    { day: "SAT", date: "MAR 28" },
    { day: "SUN", date: "MAR 29" },
  ];

  const [isClient, setIsClient] = useState(false);
  React.useEffect(() => {
    const timer = setTimeout(() => setIsClient(true), 0);
    return () => clearTimeout(timer);
  }, []);

  const filteredItems = useMemo(() => {
    const day = parseInt(selectedDate.split(" ")[1]);
    return events.filter((e) => new Date(e.startTime).getDate() === day);
  }, [selectedDate, events]);

  return (
    <AnimatePresence>
      {isVisible && (
        <>
          {/* Backdrop for Layer Separation */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
            className="fixed inset-0 z-[250] bg-black/40 backdrop-blur-sm pointer-events-auto"
          />

          <div className="fixed inset-x-0 bottom-36 z-[300] pointer-events-none flex flex-col items-center gap-6">
            {/* Temporal Modal Container */}
            <motion.div
              initial={{ y: 100, opacity: 0, scale: 0.9 }}
              animate={{ y: 0, opacity: 1, scale: 1 }}
              exit={{ y: 100, opacity: 0, scale: 0.9 }}
              className="w-full max-w-md bg-black/80 backdrop-blur-3xl border border-white/10 rounded-[2.5rem] p-8 pointer-events-auto shadow-[0_40px_80px_rgba(0,0,0,0.8)]"
            >
              <div className="flex items-center justify-between mb-8 px-2">
                <div className="flex items-center gap-4">
                  <div className="w-12 h-12 rounded-full bg-white/5 flex items-center justify-center border border-white/10">
                    <Clock className="w-6 h-6 text-white" />
                  </div>
                  <div>
                    <h3 className="text-lg font-black uppercase italic tracking-tighter text-white">
                      Temporal Core
                    </h3>
                    <p className="text-[10px] font-mono text-white/40 uppercase tracking-widest">
                      Navigate Time & Space
                    </p>
                  </div>
                </div>
                <button
                  onClick={onClose}
                  className="w-10 h-10 rounded-full bg-white/5 flex items-center justify-center border border-white/10 hover:bg-white/10 transition-colors"
                >
                  <ChevronLeft className="w-5 h-5 text-white/60 rotate-180" />
                </button>
              </div>

              {/* Time Scrubber Integrated */}
              <div className="mb-10">
                <TimelineControl onTimeChange={() => {}} />
              </div>

              {/* Date Selector */}
              <div className="flex justify-between items-center mb-8 px-2">
                {dates.map((d) => {
                  const isActive = selectedDate === d.date;
                  const dayNum = parseInt(d.date.split(" ")[1]);
                  const hasEvents = events.some(
                    (e) => new Date(e.startTime).getDate() === dayNum,
                  );

                  return (
                    <button
                      key={d.date}
                      onClick={() => onDateSelect(d.date)}
                      className="flex flex-col items-center gap-3 group relative"
                    >
                      <span
                        className={`text-[8px] font-mono font-black tracking-widest transition-colors ${isActive ? "text-white" : "text-white/20 group-hover:text-white/40"}`}
                      >
                        {d.day}
                      </span>
                      <div
                        className={`
                        w-10 h-10 rounded-2xl flex items-center justify-center transition-all duration-500 border
                        ${isActive ? "bg-white text-black border-white scale-110" : "bg-white/5 text-white/40 border-white/10 group-hover:bg-white/10"}
                      `}
                      >
                        <span className="text-xs font-black">
                          {d.date.split(" ")[1]}
                        </span>
                      </div>
                      {hasEvents && !isActive && (
                        <div className="absolute -bottom-2 left-1/2 -translate-x-1/2 w-1 h-1 rounded-full bg-white/20" />
                      )}
                    </button>
                  );
                })}
              </div>

              {/* Quick List Preview */}
              <div className="space-y-3 max-h-[200px] overflow-y-auto pr-2 custom-scrollbar">
                <AnimatePresence mode="popLayout">
                  {filteredItems.map((item) => (
                    <motion.button
                      key={item.id}
                      initial={{ opacity: 0, x: -20 }}
                      animate={{ opacity: 1, x: 0 }}
                      exit={{ opacity: 0, x: 20 }}
                      onClick={() => onEventSelect(item)}
                      className="w-full p-4 bg-white/5 hover:bg-white/10 border border-white/10 rounded-2xl flex items-center justify-between group transition-all"
                    >
                      <div className="flex items-center gap-4">
                        <div
                          className={`w-2 h-2 rounded-full bg-white/40 group-hover:bg-white transition-colors`}
                        />
                        <div className="text-left">
                          <div className="text-[10px] font-black uppercase italic tracking-tighter text-white/80 group-hover:text-white truncate max-w-[180px]">
                            {item.title}
                          </div>
                          <div className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                            {isClient &&
                              new Date(item.startTime).toLocaleTimeString([], {
                                hour: "2-digit",
                                minute: "2-digit",
                                hour12: false,
                              })}{" "}
                            @ {item.venueName}
                          </div>
                        </div>
                      </div>
                      <ChevronRight className="w-4 h-4 text-white/20 group-hover:text-white/60 transition-colors" />
                    </motion.button>
                  ))}
                </AnimatePresence>
                {filteredItems.length === 0 && (
                  <div className="py-8 text-center opacity-20">
                    <Zap className="w-6 h-6 mx-auto mb-2" />
                    <p className="text-[10px] font-mono uppercase tracking-widest">
                      No Signals Detected
                    </p>
                  </div>
                )}
              </div>
            </motion.div>
          </div>
        </>
      )}
    </AnimatePresence>
  );
}
