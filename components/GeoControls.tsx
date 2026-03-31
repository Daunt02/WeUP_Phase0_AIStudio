'use client';

import React from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { MapPin, Navigation, Target, Globe } from 'lucide-react';

interface GeoControlsProps {
  isVisible: boolean;
  onClose: () => void;
}

export default function GeoControls({ isVisible, onClose }: GeoControlsProps) {
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
          
          <div className="fixed inset-x-0 bottom-36 z-[300] pointer-events-none flex flex-col items-center">
            <motion.div
              initial={{ y: 100, opacity: 0, scale: 0.9 }}
              animate={{ y: 0, opacity: 1, scale: 1 }}
              exit={{ y: 100, opacity: 0, scale: 0.9 }}
              className="w-full max-w-sm bg-black/80 backdrop-blur-3xl border border-white/10 rounded-[2.5rem] p-8 pointer-events-auto shadow-[0_40px_80px_rgba(0,0,0,0.8)]"
            >
              <div className="flex items-center gap-4 mb-8">
                <div className="w-12 h-12 rounded-full bg-white/5 flex items-center justify-center border border-white/10">
                  <Globe className="w-6 h-6 text-white" />
                </div>
                <div>
                  <h3 className="text-lg font-black uppercase italic tracking-tighter text-white">Spatial_Core</h3>
                  <p className="text-[10px] font-mono text-white/40 uppercase tracking-widest">Adjust Map Parameters</p>
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4">
                <GeoButton icon={Target} label="RECENTER_MAP" sublabel="Snap to current location" onClick={onClose} />
                <GeoButton icon={Navigation} label="SET_RADIUS" sublabel="Adjust signal discovery range" onClick={onClose} />
                <GeoButton icon={MapPin} label="SWITCH_CITY" sublabel="Jump to another cultural hub" onClick={onClose} />
              </div>
            </motion.div>
          </div>
        </>
      )}
    </AnimatePresence>
  );
}

function GeoButton({ icon: Icon, label, sublabel, onClick }: { icon: any, label: string, sublabel: string, onClick: () => void }) {
  return (
    <button 
      onClick={onClick}
      className="w-full p-4 bg-white/5 hover:bg-white/10 border border-white/10 rounded-3xl flex items-center gap-4 transition-all group text-left"
    >
      <div className="w-10 h-10 rounded-2xl bg-white/5 flex items-center justify-center border border-white/10 group-hover:scale-110 transition-transform">
        <Icon className="w-5 h-5 text-white/60 group-hover:text-white transition-colors" />
      </div>
      <div>
        <div className="text-[11px] font-black uppercase italic tracking-tighter text-white/80 group-hover:text-white transition-colors">{label}</div>
        <div className="text-[8px] font-mono text-white/20 uppercase tracking-widest">{sublabel}</div>
      </div>
    </button>
  );
}
