'use client';

import React from 'react';
import dynamic from 'next/dynamic';

const HomeClient = dynamic(() => import('@/components/HomeClient'), {
  ssr: false,
  loading: () => (
    <div className="fixed inset-0 bg-[#050505] flex items-center justify-center text-white/30 font-mono text-xs uppercase tracking-[0.3em] animate-pulse z-[100]">
      INITIALIZING_RADAR_SYSTEM...
    </div>
  ),
});

export default function DynamicHome() {
  return <HomeClient />;
}
