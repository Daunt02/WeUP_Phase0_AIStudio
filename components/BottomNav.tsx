'use client';

import React, { useState, useRef } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { ViewMode } from '@/types';
import { Radar, Clock, Plus, Bookmark, User } from 'lucide-react';

interface BottomNavProps {
  activeMode: ViewMode;
  onModeChange: (mode: ViewMode) => void;
  onAction: (action: 'WORLD_LONG' | 'TIME_TAP' | 'TIME_HOLD_START' | 'TIME_HOLD_END' | 'ADD' | 'ADD_LONG' | 'SAVED' | 'PROFILE') => void;
}

export default function BottomNav({ activeMode, onModeChange, onAction }: BottomNavProps) {
  const longPressTimer = useRef<NodeJS.Timeout | null>(null);
  const [isHoldingTime, setIsHoldingTime] = useState(false);

  const handleWorldStart = () => {
    longPressTimer.current = setTimeout(() => {
      onAction('WORLD_LONG');
    }, 600);
  };

  const handleWorldEnd = () => {
    if (longPressTimer.current) {
      clearTimeout(longPressTimer.current);
      onModeChange('RADAR');
    }
  };

  const handleTimeStart = () => {
    longPressTimer.current = setTimeout(() => {
      setIsHoldingTime(true);
      onAction('TIME_HOLD_START');
    }, 400);
  };

  const handleTimeEnd = () => {
    if (longPressTimer.current) {
      clearTimeout(longPressTimer.current);
      if (isHoldingTime) {
        setIsHoldingTime(false);
        onAction('TIME_HOLD_END');
      } else {
        onAction('TIME_TAP');
      }
    }
  };

  const handleAddStart = () => {
    longPressTimer.current = setTimeout(() => {
      onAction('ADD_LONG');
      longPressTimer.current = null;
    }, 600);
  };

  const handleAddEnd = () => {
    if (longPressTimer.current) {
      clearTimeout(longPressTimer.current);
      onAction('ADD');
    }
  };

  return (
    <div className="fixed bottom-0 left-0 right-0 z-[200] flex justify-center items-end pb-8 sm:pb-12 pointer-events-none">
      <div className="flex items-center gap-2 bg-black/40 backdrop-blur-3xl rounded-full p-2 border border-white/10 shadow-2xl pointer-events-auto">
        
        {/* WORLD */}
        <NavItem 
          icon={Radar} 
          label="WORLD" 
          isActive={activeMode === 'RADAR'} 
          onPointerDown={handleWorldStart}
          onPointerUp={handleWorldEnd}
          onPointerLeave={() => longPressTimer.current && clearTimeout(longPressTimer.current)}
        />

        {/* TIME */}
        <NavItem 
          icon={Clock} 
          label="TIME" 
          isActive={activeMode === 'CALENDAR'} 
          onPointerDown={handleTimeStart}
          onPointerUp={handleTimeEnd}
          onPointerLeave={() => {
            if (longPressTimer.current) clearTimeout(longPressTimer.current);
            if (isHoldingTime) {
              setIsHoldingTime(false);
              onAction('TIME_HOLD_END');
            }
          }}
        />

        {/* ADD (+) */}
        <motion.button
          whileHover={{ scale: 1.1 }}
          whileTap={{ scale: 0.9 }}
          onPointerDown={handleAddStart}
          onPointerUp={handleAddEnd}
          onPointerLeave={() => longPressTimer.current && clearTimeout(longPressTimer.current)}
          className="relative w-16 h-16 sm:w-20 sm:h-20 bg-white rounded-full flex items-center justify-center shadow-[0_0_30px_rgba(255,255,255,0.3)] border-4 border-black/10 transition-all group pointer-events-auto"
        >
          <div className="absolute inset-0 rounded-full bg-white animate-pulse opacity-20 blur-xl group-hover:opacity-40 transition-opacity" />
          <Plus className="w-8 h-8 sm:w-10 sm:h-10 text-black group-hover:rotate-90 transition-transform duration-500 relative z-10" />
        </motion.button>

        {/* SAVED */}
        <NavItem 
          icon={Bookmark} 
          label="SAVED" 
          isActive={activeMode === 'SAVED'} 
          onClick={() => onAction('SAVED')}
        />

        {/* PROFILE */}
        <NavItem 
          icon={User} 
          label="PROFILE" 
          isActive={activeMode === 'PROFILE'} 
          onClick={() => onAction('PROFILE')}
        />

      </div>
    </div>
  );
}

function NavItem({ 
  icon: Icon, 
  label, 
  isActive, 
  onClick, 
  onPointerDown, 
  onPointerUp,
  onPointerLeave
}: { 
  icon: any, 
  label: string, 
  isActive: boolean, 
  onClick?: () => void,
  onPointerDown?: () => void,
  onPointerUp?: () => void,
  onPointerLeave?: () => void
}) {
  return (
    <button
      onClick={onClick}
      onPointerDown={onPointerDown}
      onPointerUp={onPointerUp}
      onPointerLeave={onPointerLeave}
      className={`
        relative px-4 sm:px-6 py-3 rounded-full flex flex-col items-center gap-1 transition-all duration-500 group select-none touch-none
        ${isActive ? 'text-white' : 'text-white/30 hover:text-white/60'}
      `}
    >
      {isActive && (
        <motion.div
          layoutId="nav-active-bg"
          className="absolute inset-0 bg-white/[0.08] rounded-full"
          transition={{ type: 'spring', bounce: 0.2, duration: 0.6 }}
        />
      )}
      
      <Icon className={`w-5 h-5 transition-all duration-500 ${isActive ? 'scale-110' : 'group-hover:scale-110'}`} />

      <span className={`text-[7px] font-mono font-black tracking-[0.2em] leading-none transition-all duration-500 ${isActive ? 'opacity-100' : 'opacity-0 group-hover:opacity-40'}`}>
        {label}
      </span>
    </button>
  );
}
