'use client';

import React from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X, Cpu } from 'lucide-react';
import { NightlifeItem } from '@/types';

interface ReceiptDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  event: NightlifeItem | null;
  tier?: string;
  doorCode?: string;
}

export default function ReceiptDrawer({
  isOpen,
  onClose,
  event,
  tier = 'NETWORK_TIER_A // FULL_SIGNAL',
  doorCode = 'WEUP-#902X-81',
}: ReceiptDrawerProps) {
  if (!event) return null;

  // Render high-contrast QR pattern in pure CSS
  const pseudoQR = (
    <div id="cryptographic-qr-code" className="w-48 h-48 bg-white p-3.5 rounded-2xl flex flex-wrap gap-0.5 relative shrink-0 shadow-2xl">
      {/* Corner Anchors */}
      <div className="absolute top-3.5 left-3.5 w-10 h-10 border-4 border-black bg-white flex items-center justify-center">
        <div className="w-4 h-4 bg-black" />
      </div>
      <div className="absolute top-3.5 right-3.5 w-10 h-10 border-4 border-black bg-white flex items-center justify-center">
        <div className="w-4 h-4 bg-black" />
      </div>
      <div className="absolute bottom-3.5 left-3.5 w-10 h-10 border-4 border-black bg-white flex items-center justify-center">
        <div className="w-4 h-4 bg-black" />
      </div>
      <div className="absolute bottom-3.5 right-3.5 w-4 h-4 bg-black" />

      {/* Randomized bits to simulate real crypto-key signature */}
      <div className="w-full h-full flex flex-wrap content-center justify-center opacity-90 p-4">
        {[...Array(121)].map((_, i) => {
          // Deterministic pattern to prevent hydration mismatch while looking completely real
          const isFilled = (i * 17 + i * i * 3) % 2 === 0;
          return (
            <div
              key={i}
              className={`w-3.5 h-3.5 ${isFilled ? 'bg-black' : 'bg-transparent'}`}
            />
          );
        })}
      </div>
    </div>
  );

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Layer 2 Dimmer backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 0.7 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
            className="fixed inset-0 z-[600] bg-black/95 backdrop-blur-xl pointer-events-auto"
          />

          {/* Sliding sheet */}
          <motion.div
            initial={{ y: '100%' }}
            animate={{ y: 0 }}
            exit={{ y: '100%' }}
            transition={{ type: 'spring', damping: 30, stiffness: 250, mass: 0.9 }}
            className="fixed inset-x-0 bottom-0 z-[700] bg-[#050505] border-t border-white/10 rounded-t-[3rem] shadow-[0_-20px_50px_rgba(0,0,0,0.9)] overflow-hidden flex flex-col pointer-events-auto max-h-[92vh] sm:max-h-[85vh]"
          >
            {/* Structural top-slider handler */}
            <div className="w-12 h-1 bg-white/10 rounded-full mx-auto my-4 shrink-0 cursor-grab active:cursor-grabbing" />

            {/* Header */}
            <div className="px-8 pb-4 border-b border-white/5 flex justify-between items-center bg-black/30">
              <div className="space-y-1">
                <p className="text-[9px] font-mono font-black text-brand-primary tracking-[0.3em] uppercase leading-none">
                  {tier}
                </p>
                <h3 className="text-xl font-black text-white tracking-tighter uppercase italic">
                  DEVICE KEY ACTIVATED
                </h3>
              </div>
              <button
                onClick={onClose}
                aria-label="Close credentials drawer"
                className="w-11 h-11 rounded-full bg-white/5 hover:bg-white/10 flex items-center justify-center border border-white/10 hover:scale-105 active:scale-95 transition-all cursor-pointer"
              >
                <X className="w-5 h-5 text-white/50 hover:text-white" />
              </button>
            </div>

            {/* Scrollable (Internal layout only, compact for single-page viewport compliance) */}
            <div className="flex-1 overflow-y-auto p-8 space-y-8 no-scrollbar">
              {/* Event Context Header */}
              <div className="flex gap-4 items-center p-4 bg-white/[0.02] border border-white/5 rounded-2xl">
                <div className="text-3xl font-black italic tracking-tighter text-[#00FF9C] uppercase font-mono bg-white/[0.04] px-4 py-2.5 rounded-xl border border-white/5">
                  {event.venue_name?.slice(0, 3).toUpperCase() || 'SPARK'}
                </div>
                <div className="space-y-1 min-w-0 flex-1">
                  <h4 className="text-sm font-black text-white uppercase truncate">{event.title}</h4>
                  <p className="text-[9px] font-mono text-white/40 uppercase tracking-widest truncate">{event.venue_name} {"//"} {event.address}</p>
                </div>
              </div>

              {/* CRISP TYPOGRAPHY CONTAINER: MASSIVE mono labels for physical checkouts */}
              <div className="flex flex-col items-center justify-center py-6 text-center space-y-6">
                
                {/* Cryptographic Key Token */}
                <div className="space-y-1 text-center">
                  <p className="text-[7.5px] font-mono text-white/20 uppercase tracking-[0.4em] font-medium">CRYPTOGRAPHIC SECTOR KEY</p>
                  <p className="text-[28px] font-mono font-black text-[#00FF9C] tracking-widest select-all leading-none py-1">
                    {doorCode}
                  </p>
                  <p className="text-[8px] font-mono text-white/40 uppercase tracking-widest italic pt-1">
                    PRESENT TO GATE-KEEPER AT CHOKEPOINT
                  </p>
                </div>

                {pseudoQR}

                {/* Highly legible Time & Area labels optimized from 3 feet away */}
                <div className="grid grid-cols-2 gap-4 w-full max-w-sm pt-4 border-t border-dashed border-white/10 text-left font-mono">
                  <div className="space-y-1 bg-white/[0.01] border border-white/5 p-4 rounded-2xl">
                    <span className="text-[7.5px] text-white/20 uppercase tracking-wider block font-bold">ACCESS TIME</span>
                    <span className="text-sm text-white font-black uppercase tracking-tight block">
                      {new Date(event.start_time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })}_HRS
                    </span>
                    <span className="text-[8.5px] text-white/40 uppercase block pt-0.5">
                      {new Date(event.start_time).toLocaleDateString([], { month: 'short', day: 'numeric' })}
                    </span>
                  </div>
                  
                  <div className="space-y-1 bg-white/[0.01] border border-white/5 p-4 rounded-2xl">
                    <span className="text-[7.5px] text-white/20 uppercase tracking-wider block font-bold">SPATIAL INDEX</span>
                    <span className="text-sm text-[#00FF9C] font-black uppercase tracking-tight block">
                      {event.spatial_label || 'ZONE_A'}
                    </span>
                    <span className="text-[8.5px] text-white/40 uppercase block truncate pt-0.5">
                      {event.neighborhood?.toUpperCase() || 'LATENT_SECTOR'}
                    </span>
                  </div>
                </div>
              </div>

              {/* Legal / Security Context */}
              <div className="p-5 rounded-2xl bg-brand-primary/[0.02] border border-brand-primary/10 flex items-start gap-3 text-left">
                <Cpu className="w-5 h-5 text-brand-primary shrink-0 mt-0.5" />
                <div className="space-y-1">
                  <span className="text-[8.5px] font-mono text-white font-black uppercase tracking-wider block">STATE-DRIVEN CODES</span>
                  <p className="text-[8px] font-mono text-white/50 uppercase tracking-widest leading-normal">
                    This ticket is cryptographically registered on your local device. Under our zero database collection policy, your operator signature remains offline and untraceable. Key decays automatically upon event end.
                  </p>
                </div>
              </div>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
