'use client';

import React from 'react';
import { motion } from 'motion/react';

interface VenuePulseProps {
  color: string;
  strength: number; // Normalized strength from 0.1 to 1.0
  isSelected?: boolean;
  isInterested?: boolean;
}

export default function VenuePulse({
  color,
  strength,
  isSelected = false,
  isInterested = false,
}: VenuePulseProps) {
  // Ensure strength is safely clamped between 0.1 and 1.0
  const activeStrength = Math.min(1.0, Math.max(0.1, strength));

  // Determine animation parameters based on signal strength and user interest status
  // Stronger signals or user interest = faster pulse, larger expansion, and brighter glow
  const duration = 2.6 - activeStrength * 1.6; // Speed ranges from 2.6s (weak) to 1.0s (strong)
  const maxScale = 1.4 + activeStrength * 1.4 + (isInterested ? 0.3 : 0);  // Scale ranges from 1.4x to 3.1x
  const peakOpacity = 0.12 + activeStrength * 0.33 + (isInterested ? 0.15 : 0); // Translucent glow from 0.12 to 0.6 opacity

  // Staggered delay for secondary pulse if the spot is active enough (high strength)
  const hasMultipleRings = activeStrength > 0.5;

  return (
    <div className="absolute inset-0 pointer-events-none flex items-center justify-center z-[-1]">
      {/* 1. Underlying continuous breathing glow (blur layer) */}
      <motion.div
        animate={{
          scale: [1, 1.15, 1],
          opacity: [peakOpacity * 0.5, peakOpacity * 0.8, peakOpacity * 0.5],
        }}
        transition={{
          duration: duration * 1.5,
          repeat: Infinity,
          ease: 'easeInOut',
        }}
        className="absolute w-10 h-10 rounded-full blur-md opacity-30"
        style={{
          backgroundColor: color,
        }}
      />

      {/* 2. Primary expanding wave ring */}
      <motion.div
        initial={{ scale: 0.8, opacity: peakOpacity }}
        animate={{
          scale: maxScale,
          opacity: 0,
        }}
        transition={{
          duration: duration,
          repeat: Infinity,
          ease: 'easeOut',
        }}
        className="absolute w-8 h-8 rounded-full border-2"
        style={{
          borderColor: color,
          boxShadow: `0 0 12px ${color}`,
        }}
      />

      {/* 3. Secondary staggered expanding wave ring (only for energetic hot-spots to optimize rendering) */}
      {hasMultipleRings && (
        <motion.div
          initial={{ scale: 0.8, opacity: peakOpacity }}
          animate={{
            scale: maxScale,
            opacity: 0,
          }}
          transition={{
            duration: duration,
            delay: duration * 0.5,
            repeat: Infinity,
            ease: 'easeOut',
          }}
          className="absolute w-8 h-8 rounded-full border border-dashed"
          style={{
            borderColor: color,
            opacity: peakOpacity * 0.7,
          }}
        />
      )}

      {/* 4. Extra-wide ultra subtle locator ray for selected venues */}
      {isSelected && (
        <motion.div
          animate={{
            scale: [1, 3.2, 1],
            opacity: [0.1, 0, 0.1],
          }}
          transition={{
            duration: 3,
            repeat: Infinity,
            ease: 'easeInOut',
          }}
          className="absolute w-8 h-8 rounded-full border border-white/30"
          style={{
            boxShadow: 'inset 0 0 10px rgba(255, 255, 255, 0.2)',
          }}
        />
      )}
    </div>
  );
}
