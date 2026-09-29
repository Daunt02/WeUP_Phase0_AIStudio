'use client';

import Link from 'next/link';

export default function NotFound() {
  return (
    <div className="min-h-screen bg-[#050505] text-white flex flex-col items-center justify-center p-8 selection:bg-[#00FF9C] selection:text-black">
      <div className="max-w-md w-full space-y-6 text-center">
        <div className="w-16 h-16 rounded-full bg-red-500/10 border border-red-500/20 flex items-center justify-center mx-auto animate-pulse">
          <div className="w-4 h-4 rounded-full bg-red-500" />
        </div>
        <div className="space-y-4">
          <h1 className="text-3xl font-black uppercase italic tracking-tighter">404_SIGNAL_INTERRUPTED</h1>
          <p className="text-white/40 font-mono text-xs uppercase tracking-[0.3em] leading-relaxed">
            The requested coordinates did not return any active signal. Return to the main grid to scan for nearby vectors.
          </p>
        </div>
        <Link 
          href="/"
          className="inline-flex items-center justify-center px-8 h-12 bg-white text-black font-semibold rounded-xl text-xs uppercase tracking-widest hover:scale-105 active:scale-95 transition-all cursor-pointer"
        >
          RESET_GRID_RADAR
        </Link>
      </div>
    </div>
  );
}

