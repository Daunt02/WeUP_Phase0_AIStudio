'use client';

import React from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X, MapPin, Zap, ArrowRight, Shield } from 'lucide-react';
import { NightlifeItem } from '@/types';

interface ZoneDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  events: NightlifeItem[];
  onEventSelect: (event: NightlifeItem) => void;
}

export default function ZoneDrawer({
  isOpen,
  onClose,
  events,
  onEventSelect,
}: ZoneDrawerProps) {
  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Subtle map dimmed backdrop overlay when Zone drawer is open */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 0.3 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
            className="fixed inset-0 z-[150] bg-black/40 pointer-events-auto"
          />

          {/* ZoneDrawer slide-up panel */}
          <motion.div
            initial={{ y: '100%' }}
            animate={{ y: 0 }}
            exit={{ y: '100%' }}
            transition={{ type: 'spring', damping: 28, stiffness: 220 }}
            className="fixed inset-x-0 bottom-0 z-[180] h-[360px] bg-black/95 backdrop-blur-3xl border-t border-white/10 rounded-t-[2.5rem] flex flex-col pointer-events-auto"
          >
            {/* Grab handle bar */}
            <div className="w-10 h-1 bg-white/10 rounded-full mx-auto my-3 shrink-0" />

            {/* Header */}
            <div className="px-6 pb-2 border-b border-white/5 flex items-center justify-between">
              <div>
                <span className="text-[7.5px] font-mono text-brand-primary tracking-[0.3em] uppercase block">
                  CONVERGING_RADAR_INDEX
                </span>
                <h3 className="text-md font-black text-white uppercase italic tracking-tight">
                  CLUSTER_ZONELENS // {events.length} ACTIVE SIGNALS
                </h3>
              </div>
              <button
                onClick={onClose}
                aria-label="Close cluster signals drawer"
                className="w-9 h-9 rounded-full bg-white/5 border border-white/10 flex items-center justify-center hover:bg-white/10 hover:scale-105 active:scale-95 transition-all text-white/40 hover:text-white cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* List Content Area: Single viewport-locked layout (no scrolling, side-by-side pagination if many, or grid with scroll container max 3) */}
            <div className="flex-1 p-6 overflow-x-auto flex gap-4 no-scrollbar items-center justify-start">
              {events.slice(0, 4).map((event) => {
                // Determine Category Color
                const categoryColor = {
                  nightlife: 'text-[#00FF9C] border-[#00FF9C]/20',
                  lounge: 'text-purple-400 border-purple-400/20',
                  concert: 'text-rose-400 border-rose-400/20',
                  private: 'text-amber-400 border-amber-400/20',
                  restaurant: 'text-blue-400 border-blue-400/20'
                }[event.category?.toLowerCase()] || 'text-white border-white/20';

                return (
                  <motion.div
                    key={event.id}
                    onClick={() => {
                      onEventSelect(event);
                      onClose();
                    }}
                    whileHover={{ scale: 1.02, borderColor: 'rgba(255, 255, 255, 0.2)' }}
                    whileTap={{ scale: 0.98 }}
                    className="w-[280px] sm:w-[320px] shrink-0 h-[210px] bg-white/[0.02] border border-white/10 rounded-3xl p-5 flex flex-col justify-between cursor-pointer transition-colors relative overflow-hidden select-none hover:bg-white/[0.04]"
                  >
                    {/* Corner accent */}
                    <div className="absolute top-0 right-0 w-16 h-16 bg-gradient-to-br from-white/5 to-transparent pointer-events-none" />

                    <div className="space-y-2">
                      <div className="flex items-center justify-between">
                        <span className={`text-[7.5px] font-mono border px-2 py-0.5 rounded-full uppercase tracking-widest ${categoryColor}`}>
                          {event.category}
                        </span>
                        <div className="flex items-center gap-1">
                          <Zap className="w-3 h-3 text-brand-primary" />
                          <span className="text-[8px] font-mono font-bold text-white/40">{event.energyLevel || 8}/12</span>
                        </div>
                      </div>

                      <h4 className="text-sm font-black text-white uppercase italic tracking-tight line-clamp-1">
                        {event.title}
                      </h4>
                      <p className="text-[10px] text-white/50 line-clamp-2 leading-relaxed">
                        {event.description}
                      </p>
                    </div>

                    <div className="flex items-center justify-between pt-2 border-t border-white/5">
                      <div className="flex flex-col gap-0.5 min-w-0">
                        <div className="flex items-center gap-1 text-[9px] text-white/70 font-black uppercase">
                          <MapPin className="w-3 h-3 shrink-0" />
                          <span className="truncate">{event.venue_name}</span>
                        </div>
                        <div className="text-[7.5px] font-mono text-white/30 uppercase tracking-widest pl-4">
                          {event.neighborhood || 'MIDTOWN'}
                        </div>
                      </div>
                      <div className="w-8 h-8 rounded-full bg-white/10 flex items-center justify-center shrink-0 hover:bg-brand-primary hover:text-black transition-all">
                        <ArrowRight className="w-4 h-4 text-white hover:text-black" />
                      </div>
                    </div>
                  </motion.div>
                );
              })}

              {events.length > 4 && (
                <div className="w-[140px] shrink-0 h-[210px] border border-dashed border-white/10 rounded-3xl flex flex-col items-center justify-center text-center p-4">
                  <Shield className="w-6 h-6 text-white/20 mb-2" />
                  <span className="text-[8px] font-mono text-white/30 uppercase tracking-widest leading-normal">
                    +{events.length - 4} SIGNAL CODES UNREPORTED
                  </span>
                </div>
              )}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
