"use client";

import React, { useState, useRef, useEffect, useMemo } from "react";
import { motion, useMotionValue, useSpring, useTransform } from "motion/react";
import { Clock } from "lucide-react";
import type { TemporalPresetSelection } from "@/features/world/runtimeTypes";

interface TimelineControlProps {
  onTimeChange: (time: TemporalPresetSelection) => void;
}

const TimelineLabel = ({
  label,
  index,
  springX,
  currentTime,
}: {
  label: string;
  index: number;
  springX: any;
  currentTime: string;
}) => {
  const opacity = useTransform(
    springX,
    [-150 + index * 40, -110 + index * 40, -70 + index * 40],
    [0.2, 1, 0.2],
  );
  return (
    <motion.span
      style={{ opacity }}
      className={`text-[9px] font-mono font-bold tracking-widest shrink-0 ${currentTime === label ? "text-brand-primary" : "text-white/20"}`}
    >
      {label}
    </motion.span>
  );
};

export default function TimelineControl({
  onTimeChange,
}: TimelineControlProps) {
  const [isHolding, setIsHolding] = useState(false);
  const [currentTime, setCurrentTime] =
    useState<TemporalPresetSelection>("Today");
  const containerRef = useRef<HTMLDivElement>(null);

  const x = useMotionValue(0);
  const springX = useSpring(x, { stiffness: 300, damping: 30 });

  const timeLabels = useMemo<TemporalPresetSelection[]>(
    () => ["Today", "Tonight", "Weekend", "Next7Days"],
    [],
  );

  // Map x position to time labels
  const labelIndex = useTransform(
    springX,
    [-150, 150],
    [0, timeLabels.length - 1],
  );

  useEffect(() => {
    return labelIndex.on("change", (latest) => {
      const index = Math.round(
        Math.max(0, Math.min(timeLabels.length - 1, latest)),
      );
      const newTime = timeLabels[index];
      if (newTime !== currentTime) {
        setCurrentTime(newTime);
        onTimeChange(newTime);
      }
    });
  }, [labelIndex, currentTime, onTimeChange, timeLabels]);

  const handleTouchStart = () => setIsHolding(true);
  const handleTouchEnd = () => {
    setIsHolding(false);
    x.set(0);
  };

  const handleTouchMove = (e: React.TouchEvent | React.MouseEvent) => {
    if (!isHolding) return;

    const clientX = "touches" in e ? e.touches[0].clientX : e.clientX;
    if (containerRef.current) {
      const rect = containerRef.current.getBoundingClientRect();
      const centerX = rect.left + rect.width / 2;
      const deltaX = clientX - centerX;
      const constrainedX = Math.max(-150, Math.min(150, deltaX));
      x.set(constrainedX);
    }
  };

  const scrubberWidth = useTransform(springX, [-150, 150], [0, 240]);

  return (
    <div className="flex flex-col items-center gap-4 w-full max-w-[90vw] px-4 select-none touch-none">
      <div
        ref={containerRef}
        className="relative group flex items-center gap-4 bg-black/80 backdrop-blur-3xl border border-white/10 px-6 py-4 rounded-full shadow-[0_20px_50px_rgba(0,0,0,0.8)] hover:border-brand-primary/40 transition-all duration-500"
        onMouseDown={handleTouchStart}
        onMouseUp={handleTouchEnd}
        onMouseMove={handleTouchMove}
        onTouchStart={handleTouchStart}
        onTouchEnd={handleTouchEnd}
        onTouchMove={handleTouchMove}
      >
        <motion.div
          animate={{
            scale: isHolding ? 1.2 : 1,
            color: isHolding ? "#00FF9C" : "#FFFFFF",
          }}
          className="flex items-center gap-3"
        >
          <Clock
            className={`w-4 h-4 ${isHolding ? "text-brand-primary" : "text-white/40"}`}
          />
          <div className="flex flex-col">
            <span className="text-[10px] font-mono font-black tracking-[0.3em] uppercase italic text-white/90">
              {isHolding ? "EXPLORING FUTURE" : "TONIGHT"}
            </span>
            <span className="text-[14px] font-mono font-black text-brand-primary leading-none">
              {currentTime}
            </span>
          </div>
        </motion.div>

        {isHolding && (
          <motion.div
            initial={{ opacity: 0, width: 0, x: -10 }}
            animate={{ opacity: 1, width: "auto", x: 0 }}
            exit={{ opacity: 0, width: 0, x: -10 }}
            className="flex items-center gap-2 overflow-hidden"
          >
            <div className="w-px h-6 bg-white/10 mx-2" />
            <div className="flex gap-4 items-center px-2">
              {timeLabels.map((label, i) => (
                <TimelineLabel
                  key={label}
                  label={label}
                  index={i}
                  springX={springX}
                  currentTime={currentTime}
                />
              ))}
            </div>
          </motion.div>
        )}

        {/* Visual Feedback Scrubber */}
        {isHolding && (
          <motion.div
            className="absolute bottom-0 left-1/2 -translate-x-1/2 h-0.5 bg-brand-primary/40 rounded-full"
            style={{ width: scrubberWidth }}
          />
        )}
      </div>

      {!isHolding && (
        <span className="text-[8px] font-mono text-white/20 tracking-[0.4em] uppercase">
          Hold + Slide to Scrub Time
        </span>
      )}
    </div>
  );
}
