'use client';

import React from 'react';
interface TopBarProps {
  isIngesting?: boolean;
  isDiscovering?: boolean;
  onDiscover?: () => void;
}

export default function TopBar({ isIngesting }: TopBarProps) {
  const isMapOffline = !process.env.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN;

  return (
    <div className="flex items-center justify-between px-6 h-12 pointer-events-none">
      <div className="flex flex-col pointer-events-auto">
        <h1 className="text-lg font-black tracking-tighter uppercase italic leading-none text-white">
          WEUP
        </h1>
        <span className="text-[6px] font-mono text-white/40 tracking-[0.3em] uppercase">Signal_Network</span>
      </div>

      <div className="flex items-center gap-3 pointer-events-auto">
        {isIngesting && (
          <div className="flex items-center gap-2 bg-[#00FF9C]/10 backdrop-blur-md border border-[#00FF9C]/20 px-3 py-1 rounded-full">
            <div className="w-1 h-1 rounded-full bg-[#00FF9C] animate-ping" />
            <span className="text-[7px] font-mono uppercase tracking-widest font-bold text-[#00FF9C]">
              INGESTING
            </span>
          </div>
        )}

        <div className={`flex items-center gap-2 bg-black/40 backdrop-blur-md border px-3 py-1 rounded-full transition-all duration-500 ${isMapOffline ? 'border-red-500/40' : 'border-white/5'}`}>
          <div className="relative flex items-center gap-2">
            <div className={`w-1 h-1 rounded-full ${isMapOffline ? 'bg-red-500' : 'bg-[#00FF9C]'} animate-pulse`} />
            <span className={`text-[7px] font-mono uppercase tracking-widest font-bold ${isMapOffline ? 'text-red-500' : 'text-white/40'}`}>
              {isMapOffline ? 'SYSTEM_OFFLINE' : 'LIVE_PHASE'}
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
