'use client';

import React from 'react';
import { ShieldCheck } from 'lucide-react';

export default function TopBar() {
  const isMapOffline = !process.env.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN;

  return (
    <div className="fixed top-0 left-0 right-0 z-[100] flex items-center justify-between px-6 sm:px-10 h-16 sm:h-20 pointer-events-none pt-safe">
      <div className="flex flex-col pointer-events-auto">
        <h1 className="text-xl sm:text-2xl font-black tracking-tighter uppercase italic leading-none text-white">
          WEUP
        </h1>
      </div>

      <div className={`flex items-center gap-2 bg-black/20 backdrop-blur-md border px-3 py-1.5 rounded-full pointer-events-auto transition-all duration-500 ${isMapOffline ? 'border-red-500/40' : 'border-white/5'}`}>
        <div className="relative flex items-center gap-2">
          <div className={`w-1.5 h-1.5 rounded-full ${isMapOffline ? 'bg-red-500' : 'bg-brand-primary'} animate-pulse`} />
          <span className={`text-[8px] font-mono uppercase tracking-widest font-bold ${isMapOffline ? 'text-red-500' : 'text-white/60'}`}>
            {isMapOffline ? 'OFFLINE' : 'ACTIVE'}
          </span>
        </div>
      </div>
    </div>
  );
}
