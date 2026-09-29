'use client';

import React, { useMemo, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import Image from 'next/image';
import { ChevronLeft, ChevronRight, Zap, Clock, Bookmark, MapPin } from 'lucide-react';
import { NightlifeItem } from '@/types';
import { getUpcomingWeek, formatHoustonDateLabel } from '@/utils/dateUtils';
import TimelineControl from './TimelineControl';

interface CulturalCalendarProps {
  isVisible: boolean;
  events: NightlifeItem[];
  selectedDate: string;
  selectedTime: string;
  onDateSelect: (date: string) => void;
  onTimeSelect: (time: string) => void;
  onEventSelect: (event: NightlifeItem) => void;
  onToggleSave: (id: string) => void;
  onHighlight?: (ids: string[]) => void;
  savedIds: string[];
  onClose: () => void;
}

export default function CulturalCalendar({ 
  isVisible, 
  events, 
  selectedDate, 
  selectedTime,
  onDateSelect, 
  onTimeSelect,
  onEventSelect,
  onToggleSave,
  onHighlight,
  savedIds,
  onClose
}: CulturalCalendarProps) {
  const dates = useMemo(() => getUpcomingWeek(), []);

  const [isClient, setIsClient] = useState(false);
  React.useEffect(() => {
    const timer = setTimeout(() => setIsClient(true), 0);
    return () => clearTimeout(timer);
  }, []);

  const filteredItems = useMemo(() => {
    return events.filter(e => formatHoustonDateLabel(new Date(e.start_time)) === selectedDate);
  }, [selectedDate, events]);

  // Emit highlighted IDs when filteredItems change
  React.useEffect(() => {
    if (onHighlight) {
      onHighlight(filteredItems.slice(0, 8).map(item => item.id));
    }
  }, [filteredItems, onHighlight]);

  // Default expand the first event to demonstrate interaction
  const [expandedEventId, setExpandedEventId] = useState<string | null>(null);
  
  React.useEffect(() => {
    if (filteredItems.length > 0 && !expandedEventId) {
      setExpandedEventId(filteredItems[0].id);
    }
  }, [filteredItems, expandedEventId]);

  return (
    <AnimatePresence>
      {isVisible && (
        <>
          {/* Backdrop for Layer Separation - Minimal to keep map visible */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
            className="fixed inset-0 z-[250] bg-black/5 backdrop-blur-[0.5px] pointer-events-none"
          />
          
          <div className="fixed inset-x-0 bottom-24 z-[300] pointer-events-none flex flex-col items-center px-6">
            {/* Temporal Modal Container - Floating Glass Layer */}
            <motion.div
              initial={{ y: '20%', opacity: 0, scale: 0.98 }}
              animate={{ y: 0, opacity: 1, scale: 1 }}
              exit={{ y: '20%', opacity: 0, scale: 0.98 }}
              transition={{ type: 'spring', damping: 28, stiffness: 220 }}
              className="w-full max-w-lg h-[68vh] bg-black/50 backdrop-blur-[20px] border border-white/10 rounded-[3rem] p-5 pb-8 pointer-events-auto shadow-[0_20px_100px_rgba(0,0,0,0.9)] flex flex-col overflow-hidden relative"
            >
              {/* Signal Density Feedback - Faint background glow */}
              <div className="absolute top-0 right-0 w-64 h-64 bg-brand-primary/5 blur-[120px] rounded-full -translate-y-1/2 translate-x-1/2 pointer-events-none" />

              {/* Drag Handle */}
              <div className="w-8 h-1 bg-white/10 rounded-full mx-auto mb-4 shrink-0" />

              <div className="flex items-center justify-between mb-4 px-2 shrink-0">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 rounded-full bg-white/5 flex items-center justify-center border border-white/10">
                    <Clock className="w-3.5 h-3.5 text-white/60" />
                  </div>
                  <div>
                    <h3 className="text-base font-black uppercase italic tracking-tighter text-white leading-none">Temporal Core</h3>
                    <p className="text-[6px] font-mono text-white/20 uppercase tracking-[0.3em] mt-0.5">Time Layer Active</p>
                  </div>
                </div>
                
                {/* Tonight / Now Pill */}
                <div className="flex items-center gap-1.5 bg-brand-primary/10 border border-brand-primary/20 px-2.5 py-1 rounded-full">
                  <div className="w-1 h-1 rounded-full bg-brand-primary animate-pulse" />
                  <span className="text-[8px] font-black uppercase italic tracking-tighter text-brand-primary">Tonight / Now</span>
                </div>
              </div>

              {/* Time Scrubber Integrated - Highly Compressed */}
              <div className="mb-1 shrink-0 scale-[0.75] origin-top">
                <TimelineControl initialTime={selectedTime} onTimeChange={onTimeSelect} />
              </div>
              
              <div className="text-center mb-4 shrink-0">
                <p className="text-[6px] font-mono text-white/10 uppercase tracking-[0.4em]">Tonight 3AM</p>
              </div>

              {/* Date Selector - Compact */}
              <div className="flex justify-between items-center mb-5 px-1 shrink-0 overflow-x-auto no-scrollbar gap-2">
                {dates.map((d) => {
                  const isActive = selectedDate === d.date;
                  const dayNum = parseInt(d.date.split(' ')[1]);
                  const hasEvents = events.some(e => new Date(e.start_time).getDate() === dayNum);

                  return (
                    <button
                      key={d.date}
                      onClick={() => onDateSelect(d.date)}
                      className="flex flex-col items-center gap-1.5 group relative shrink-0"
                    >
                      <span className={`text-[6px] font-mono font-black tracking-[0.2em] transition-colors ${isActive ? 'text-white' : 'text-white/20 group-hover:text-white/40'}`}>
                        {d.day}
                      </span>
                      <div className={`
                        w-8 h-8 rounded-full flex items-center justify-center transition-all duration-500 border
                        ${isActive ? 'bg-white text-black border-white scale-105' : 'bg-white/5 text-white/40 border-white/10 group-hover:bg-white/10'}
                      `}>
                        <span className="text-[9px] font-black">{d.date.split(' ')[1]}</span>
                      </div>
                      {hasEvents && !isActive && (
                        <div className="absolute -bottom-1 left-1/2 -translate-x-1/2 w-0.5 h-0.5 rounded-full bg-brand-primary/40" />
                      )}
                    </button>
                  );
                })}
              </div>

              <div className="px-2 mb-3 shrink-0">
                <p className="text-[7px] font-mono text-white/20 uppercase tracking-[0.5em] font-black">Active Signals</p>
              </div>

              {/* Event Discovery Layer - Masonry/Stacked Style */}
              <div className="flex-1 overflow-y-auto pr-1 no-scrollbar">
                <AnimatePresence mode="popLayout">
                  {filteredItems.length > 0 ? (
                    <div className="flex flex-col gap-3 pb-6">
                      {filteredItems.slice(0, 8).map((item, idx) => {
                        const isExpanded = expandedEventId === item.id;
                        const isSaved = savedIds.includes(item.id);

                        return (
                          <motion.div
                            key={`event-${item.id}-${idx}`}
                            layout
                            initial={{ opacity: 0, y: 20 }}
                            animate={{ 
                              opacity: 1, 
                              y: 0,
                              zIndex: isExpanded ? 10 : 1
                            }}
                            exit={{ opacity: 0, scale: 0.95 }}
                            className={`
                              relative group flex flex-col rounded-[1.5rem] overflow-hidden transition-all duration-500
                              ${isExpanded ? 'bg-white/[0.12] border-white/30 shadow-[0_15px_50px_rgba(0,0,0,0.6)]' : 'bg-white/[0.04] border-white/5'}
                              border hover:border-white/20
                            `}
                          >
                            {/* Signal Glow for Active/Nearby */}
                            {(idx < 2 || isExpanded) && (
                              <div className="absolute -top-10 -left-10 w-24 h-24 bg-brand-primary/10 blur-2xl rounded-full pointer-events-none" />
                            )}

                            <div 
                              className="p-4 cursor-pointer flex items-center justify-between"
                              onClick={() => setExpandedEventId(isExpanded ? null : item.id)}
                            >
                              <div className="flex items-center gap-4">
                                <div className="relative w-10 h-10 rounded-xl overflow-hidden border border-white/10 shrink-0">
                                  <Image 
                                    src={item.image_url} 
                                    alt={item.title} 
                                    fill 
                                    className="object-cover opacity-60 group-hover:opacity-100 transition-all duration-700" 
                                    referrerPolicy="no-referrer"
                                  />
                                </div>
                                
                                <div className="text-left">
                                  <h4 className="text-[11px] font-black uppercase italic tracking-tighter text-white leading-tight mb-1 group-hover:text-brand-primary transition-colors">
                                    {item.title}
                                  </h4>
                                  <div className="flex items-center gap-2">
                                    <div className="flex items-center gap-1 text-[6px] font-mono text-white/30 uppercase tracking-widest">
                                      <Clock className="w-2 h-2 text-white/20" />
                                      {isClient && new Date(item.start_time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })}
                                    </div>
                                    <div className="w-0.5 h-0.5 rounded-full bg-white/10" />
                                    <div className="flex items-center gap-1 text-[6px] font-mono text-white/30 uppercase tracking-widest truncate max-w-[100px]">
                                      <MapPin className="w-2 h-2 text-brand-primary/40" />
                                      {item.venue_name}
                                    </div>
                                  </div>
                                </div>
                              </div>

                              <div className="w-6 h-6 rounded-full bg-white/5 flex items-center justify-center border border-white/5 group-hover:border-white/20 transition-all">
                                {isExpanded ? <ChevronLeft className="w-3 h-3 text-white/40 rotate-90" /> : <ChevronRight className="w-3 h-3 text-white/40" />}
                              </div>
                            </div>

                            {/* Expanded State Content */}
                            <AnimatePresence>
                              {isExpanded && (
                                <motion.div
                                  initial={{ height: 0, opacity: 0 }}
                                  animate={{ height: 'auto', opacity: 1 }}
                                  exit={{ height: 0, opacity: 0 }}
                                  className="px-4 pb-4 overflow-hidden"
                                >
                                  <div className="h-px bg-white/10 w-full mb-3" />
                                  <div className="flex gap-4">
                                    <div className="flex-1">
                                      <p className="text-[8px] font-mono text-white/40 leading-relaxed uppercase tracking-wider mb-4">
                                        {item.description || "High intensity cultural signal detected. Recommended for immediate extraction."}
                                      </p>
                                      <div className="flex gap-2">
                                        <button 
                                          onClick={(e) => {
                                            e.stopPropagation();
                                            onEventSelect(item);
                                          }}
                                          className="flex-1 h-8 bg-white text-black rounded-full text-[7px] font-black uppercase tracking-widest flex items-center justify-center gap-2"
                                        >
                                          VIEW SIGNAL
                                        </button>
                                        <button 
                                          onClick={(e) => {
                                            e.stopPropagation();
                                            onToggleSave(item.id);
                                          }}
                                          className={`w-8 h-8 rounded-full flex items-center justify-center border transition-all ${isSaved ? 'bg-brand-primary border-brand-primary text-black' : 'bg-white/5 border-white/10 text-white/40'}`}
                                        >
                                          <Bookmark className={`w-3 h-3 ${isSaved ? 'fill-current' : ''}`} />
                                        </button>
                                      </div>
                                    </div>
                                    <div className="w-20 h-20 relative rounded-xl overflow-hidden border border-white/10 shrink-0">
                                      <Image src={item.image_url} alt="" fill className="object-cover" />
                                    </div>
                                  </div>
                                </motion.div>
                              )}
                            </AnimatePresence>
                          </motion.div>
                        );
                      })}
                    </div>
                  ) : (
                    <div className="py-20 text-center">
                      <div className="w-16 h-16 rounded-full bg-white/5 border border-dashed border-white/10 flex items-center justify-center mx-auto mb-4">
                        <Zap className="w-6 h-6 text-white/10" />
                      </div>
                      <p className="text-[8px] font-mono uppercase tracking-[0.4em] text-white/20">No Signals Detected</p>
                    </div>
                  )}
                </AnimatePresence>
              </div>
            </motion.div>
          </div>
        </>
      )}
    </AnimatePresence>
  );
}
