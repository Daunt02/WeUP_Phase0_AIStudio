'use client';

import React from 'react';
import { motion } from 'motion/react';

interface FlyerSkeletonProps {
  aspectRatio?: '4:5' | '16:9' | string;
  className?: string;
}

export default function FlyerSkeleton({
  aspectRatio = '4:5',
  className = '',
}: FlyerSkeletonProps) {
  const ratioStyles = aspectRatio === '16:9' ? 'aspect-video' : 'aspect-[4/5]';

  return (
    <div
      id="flyer-skeleton-container"
      className={`relative w-full ${ratioStyles} rounded-[2rem] overflow-hidden bg-white/[0.02] border border-white/5 flex flex-col justify-end p-8 ${className}`}
    >
      {/* Pulse overlay with simple opacity shifts */}
      <motion.div
        animate={{ opacity: [0.3, 0.6, 0.3] }}
        transition={{ duration: 2.2, repeat: Infinity, ease: 'easeInOut' }}
        className="absolute inset-0 bg-[#00FF9C]/[0.02] pointer-events-none"
      />

      <div className="space-y-4 relative z-10">
        {/* Category badge slot */}
        <motion.div
          animate={{ opacity: [0.4, 0.8, 0.4] }}
          transition={{ duration: 1.8, repeat: Infinity, ease: 'easeInOut' }}
          className="w-20 h-4 bg-white/5 rounded-full"
        />

        {/* Title slot */}
        <div className="space-y-2">
          <motion.div
            animate={{ opacity: [0.4, 0.8, 0.4] }}
            transition={{ duration: 1.8, repeat: Infinity, ease: 'easeInOut', delay: 0.2 }}
            className="w-2/3 h-8 bg-white/10 rounded-xl"
          />
          <motion.div
            animate={{ opacity: [0.4, 0.8, 0.4] }}
            transition={{ duration: 1.8, repeat: Infinity, ease: 'easeInOut', delay: 0.4 }}
            className="w-1/2 h-5 bg-white/5 rounded-lg"
          />
        </div>

        {/* Info row */}
        <div className="grid grid-cols-2 gap-4 pt-1">
          <motion.div
            animate={{ opacity: [0.4, 0.8, 0.4] }}
            transition={{ duration: 1.8, repeat: Infinity, ease: 'easeInOut', delay: 0.6 }}
            className="h-10 bg-white/[0.03] rounded-xl"
          />
          <motion.div
            animate={{ opacity: [0.4, 0.8, 0.4] }}
            transition={{ duration: 1.8, repeat: Infinity, ease: 'easeInOut', delay: 0.8 }}
            className="h-10 bg-white/[0.03] rounded-xl"
          />
        </div>
      </div>
    </div>
  );
}
