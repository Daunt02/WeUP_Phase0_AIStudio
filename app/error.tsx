'use client';

import React from 'react';

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="min-h-screen bg-[#050505] text-white flex flex-col items-center justify-center p-8 selection:bg-[#00FF9C] selection:text-black">
      <div className="max-w-md w-full space-y-6 text-center">
        <div className="w-16 h-16 rounded-full bg-red-500/10 border border-red-500/20 flex items-center justify-center mx-auto">
          <div className="w-3 h-3 rounded-full bg-red-500 shadow-[0_0_10px_#ef4444]" />
        </div>
        <div className="space-y-3">
          <h1 className="text-3xl font-black uppercase italic tracking-tighter text-[#ef4444]">SYSTEM_FAILURE_DETECTION</h1>
          <p className="text-white/40 font-mono text-xs uppercase tracking-[0.2em] leading-relaxed">
            {error?.message || 'An unexpected runtime trace abnormality has occurred.'}
          </p>
        </div>
        <button 
          onClick={() => reset()}
          className="inline-flex items-center justify-center px-8 h-12 bg-white text-black font-semibold rounded-xl text-xs uppercase tracking-widest hover:scale-105 active:scale-95 transition-all cursor-pointer"
        >
          RETRY_SYSTEM_THREAD
        </button>
      </div>
    </div>
  );
}
