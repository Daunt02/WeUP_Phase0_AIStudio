'use client';

import React from 'react';
import Image from 'next/image';
import { motion } from 'motion/react';

interface MediaFlyerCardProps {
  src?: string;
  alt: string;
  policyStatus?: 'PENDING' | 'APPROVED' | string;
  status?: 'DRAFT' | 'NEEDS_REVIEW' | 'PUBLISHED' | 'ARCHIVED' | string;
  aspectRatio?: '4:5' | '16:9' | string;
  className?: string;
}

export default function MediaFlyerCard({
  src,
  alt,
  policyStatus,
  status,
  aspectRatio = '4:5',
  className = '',
}: MediaFlyerCardProps) {
  // Enforce aspect ratio
  const ratioStyles = aspectRatio === '16:9' ? 'aspect-video' : 'aspect-[4/5]';
  const isPending = policyStatus === 'PENDING' || status === 'NEEDS_REVIEW';

  const defaultImage = 'https://picsum.photos/seed/weup/600/750';
  const imageSrc = src || defaultImage;

  return (
    <div id="media-flyer-card-container" className={`relative w-full ${ratioStyles} rounded-[2rem] overflow-hidden bg-[#0a0a0a] border border-white/5 group ${className}`}>
      {/* Dynamic Scanline & Grain Texture overlays inside WeUP Material Language */}
      <div className="absolute inset-0 pointer-events-none bg-[radial-gradient(ellipse_at_center,rgba(255,255,255,0.03),transparent)] z-[10]" />
      
      {/* Grain / Noise texture */}
      <div 
        className="absolute inset-0 pointer-events-none opacity-[0.03] bg-repeat z-[11]" 
        style={{ 
          backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noise'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.65' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noise)'/%3E%3C/svg%3E")`
        }} 
      />

      {/* Main Image */}
      <Image
        src={imageSrc}
        alt={alt}
        fill
        className={`object-cover ${isPending ? 'blur-md scale-105' : 'group-hover:scale-102'} transition-all duration-700 ease-out`}
        referrerPolicy="no-referrer"
        sizes="(max-width: 768px) 100vw, 50vw"
      />

      {/* Backdrop blur overlay for pending signal policy status */}
      {isPending && (
        <div id="awaiting-signal-overlay" className="absolute inset-0 bg-black/60 backdrop-blur-xl flex flex-col items-center justify-center p-4 text-center z-20">
          <motion.div
            initial={{ opacity: 0.8 }}
            animate={{ opacity: [0.4, 0.8, 0.4] }}
            transition={{ duration: 3, repeat: Infinity, ease: 'easeInOut' }}
            className="space-y-2"
          >
            <p className="text-[10px] font-mono font-black uppercase text-brand-primary tracking-[0.3em] bg-brand-primary/10 border border-brand-primary/20 px-3 py-1.5 rounded-xl">
              AWAITING SIGNAL
            </p>
            <p className="text-[7px] font-mono text-white/40 uppercase tracking-widest max-w-[150px] mx-auto">
              MEDIA CURRENTLY UNDERGOING SECURITY VERIFICATION
            </p>
          </motion.div>
        </div>
      )}

      {/* Bottom info vignette */}
      <div className="absolute inset-x-0 bottom-0 h-1/3 bg-gradient-to-t from-black via-black/45 to-transparent pointer-events-none z-[5]" />
    </div>
  );
}
