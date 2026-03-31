'use client';

import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X, Bookmark, Trash2, ExternalLink, MapPin, Calendar, Sparkles } from 'lucide-react';
import Image from 'next/image';
import { NightlifeItem } from '@/types';

interface SavedEventsProps {
  isVisible: boolean;
  onClose: () => void;
  savedEvents: NightlifeItem[];
}

export default function SavedEvents({ isVisible, onClose, savedEvents }: SavedEventsProps) {
  const [isClient, setIsClient] = useState(false);
  React.useEffect(() => {
    const timer = setTimeout(() => setIsClient(true), 0);
    return () => clearTimeout(timer);
  }, []);

  return (
    <AnimatePresence>
      {isVisible && (
        <>
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
            className="fixed inset-0 z-[250] bg-black/40 backdrop-blur-sm pointer-events-auto"
          />
          
          <motion.div
            initial={{ x: '100%' }}
            animate={{ x: 0 }}
            exit={{ x: '100%' }}
            transition={{ type: 'spring', damping: 30, stiffness: 300 }}
            className="fixed inset-y-0 right-0 z-[300] w-full md:w-[480px] bg-[#050505] border-l border-white/10 shadow-[-50px_0_100px_rgba(0,0,0,0.8)] overflow-hidden flex flex-col"
          >
          {/* Header */}
          <div className="p-12 border-b border-white/5 flex justify-between items-center bg-black/40 backdrop-blur-xl">
            <div className="space-y-1">
              <h2 className="text-4xl font-black tracking-tighter uppercase italic text-white">Shortlist</h2>
              <p className="text-white/30 font-mono text-[10px] uppercase tracking-[0.4em]">Curated Cultural Signals</p>
            </div>
            <button 
              onClick={onClose} 
              className="w-12 h-12 flex items-center justify-center rounded-full bg-white/5 hover:bg-white/10 transition-all hover:scale-110 active:scale-90"
            >
              <X className="w-6 h-6 text-white/60" />
            </button>
          </div>

          {/* List */}
          <div className="flex-1 overflow-y-auto p-8 no-scrollbar space-y-6">
            {savedEvents.length > 0 ? (
              <div className="space-y-4">
                {savedEvents.map((event, i) => (
                  <motion.div
                    key={event.id}
                    initial={{ opacity: 0, x: 20 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ delay: i * 0.05 }}
                    className="group relative bg-white/[0.02] border border-white/5 rounded-[2.5rem] p-6 hover:bg-white/[0.05] hover:border-white/10 transition-all duration-500"
                  >
                    <div className="flex gap-6">
                      <div className="relative w-24 h-24 rounded-2xl overflow-hidden flex-shrink-0 border border-white/10">
                        <Image 
                          src={event.image_url} 
                          alt={event.title} 
                          fill
                          className="object-cover grayscale group-hover:grayscale-0 transition-all duration-700" 
                          referrerPolicy="no-referrer"
                        />
                      </div>
                      <div className="flex-1 min-w-0 flex flex-col justify-center space-y-2">
                        <div className="flex items-start justify-between gap-4">
                          <h3 className="text-xl font-black uppercase italic tracking-tight text-white truncate">{event.title}</h3>
                          <button className="text-white/20 hover:text-brand-primary transition-colors">
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </div>
                        <div className="space-y-1">
                          <div className="flex items-center gap-2 text-[10px] font-mono text-white/40 uppercase tracking-widest">
                            <MapPin className="w-3 h-3 text-brand-primary" />
                            <span className="truncate">{event.venue_name} {'//'} {event.neighborhood || 'HOUSTON'}</span>
                          </div>
                          <div className="flex items-center gap-2 text-[10px] font-mono text-white/40 uppercase tracking-widest">
                            <Calendar className="w-3 h-3 text-white/20" />
                            <span>
                              {isClient && new Date(event.start_time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })} {'//'} {new Date(event.start_time).toLocaleDateString([], { month: 'short', day: 'numeric' }).toUpperCase()}
                            </span>
                          </div>
                        </div>
                      </div>
                    </div>

                    {/* Quick Actions Overlay */}
                    <div className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity duration-300 pointer-events-none">
                      <div className="flex gap-2 pointer-events-auto">
                        <button className="px-6 py-3 bg-white text-black rounded-full text-[10px] font-black uppercase tracking-widest hover:scale-105 active:scale-95 transition-all">
                          VIEW SIGNAL
                        </button>
                        <button className="w-12 h-12 bg-white/10 backdrop-blur-md text-white rounded-full flex items-center justify-center hover:bg-white/20 transition-all">
                          <ExternalLink className="w-4 h-4" />
                        </button>
                      </div>
                    </div>
                  </motion.div>
                ))}
              </div>
            ) : (
              <div className="h-full flex flex-col items-center justify-center text-center space-y-8 px-12">
                <div className="relative">
                  <div className="w-32 h-32 rounded-full bg-white/5 border border-white/5 flex items-center justify-center animate-pulse">
                    <Bookmark className="w-12 h-12 text-white/10" />
                  </div>
                  <div className="absolute -top-2 -right-2 w-10 h-10 rounded-full bg-brand-primary/20 flex items-center justify-center border border-brand-primary/30">
                    <Sparkles className="w-5 h-5 text-brand-primary" />
                  </div>
                </div>
                <div className="space-y-3">
                  <p className="text-2xl font-black uppercase italic text-white">No signals archived</p>
                  <p className="text-white/30 font-mono text-[11px] leading-relaxed uppercase tracking-widest">
                    Explore the city grid and bookmark nightlife signals to build your cultural itinerary.
                  </p>
                </div>
                <button 
                  onClick={onClose}
                  className="px-8 py-4 bg-white/5 border border-white/10 rounded-full text-[10px] font-black uppercase tracking-[0.3em] text-white/60 hover:bg-white/10 hover:text-white transition-all"
                >
                  RETURN_TO_RADAR
                </button>
              </div>
            )}
          </div>

          {/* Footer Stats */}
          {savedEvents.length > 0 && (
            <div className="p-12 bg-black/60 border-t border-white/5 flex items-center justify-between">
              <div className="space-y-1">
                <span className="text-[10px] font-mono text-white/20 uppercase tracking-widest">Total Signals</span>
                <p className="text-2xl font-black italic text-brand-primary">{savedEvents.length}</p>
              </div>
              <button className="h-16 px-10 bg-brand-primary text-black rounded-full font-black uppercase tracking-[0.3em] text-[10px] hover:scale-105 active:scale-95 transition-all shadow-[0_10px_40px_rgba(0,255,156,0.3)]">
                EXPORT_ITINERARY
              </button>
            </div>
          )}
        </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
