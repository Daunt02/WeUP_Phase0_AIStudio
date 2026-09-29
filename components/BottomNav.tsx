'use client';

import React from 'react';
import { motion } from 'motion/react';
import { ViewMode } from '@/types';
import { Compass, Activity, Plus, Bookmark, Key } from 'lucide-react';

interface BottomNavProps {
  activeMode: ViewMode;
  onModeChange: (mode: ViewMode) => void;
  onAction: (action: any) => void;
}

export default function BottomNav({ activeMode, onModeChange }: BottomNavProps) {
  return (
    <div className="fixed bottom-0 left-0 right-0 z-[200] flex justify-center items-end pb-6 sm:pb-10 pointer-events-none">
      <div className="flex items-center gap-1 bg-black/80 backdrop-blur-3xl rounded-3xl p-1.5 border border-white/10 shadow-2xl pointer-events-auto">
        
        <NavItem 
          icon={Compass} 
          label="EXPLORE" 
          isActive={activeMode === 'DISCOVER'} 
          onClick={() => onModeChange('DISCOVER')}
        />

        <NavItem 
          icon={Activity} 
          label="ACTIVITY" 
          isActive={activeMode === 'ACTIVITY'} 
          onClick={() => onModeChange('ACTIVITY')}
        />

        {/* PRIMARY ACTION (CREATE) */}
        <motion.button
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          onClick={() => onModeChange('CREATE')}
          className={`
            relative w-14 h-14 sm:w-16 sm:h-16 rounded-2xl flex items-center justify-center transition-all duration-300 mx-2
            ${activeMode === 'CREATE' ? 'bg-[#00FF9C] text-black shadow-[0_0_30px_rgba(0,255,156,0.5)]' : 'bg-white text-black shadow-xl'}
          `}
        >
          <Plus className={`w-7 h-7 sm:w-8 sm:h-8 transition-transform duration-500 ${activeMode === 'CREATE' ? 'rotate-45' : ''}`} />
          <div className="absolute -bottom-1.5 left-1/2 -translate-x-1/2 bg-black px-1.5 py-0.5 rounded text-[6px] font-black tracking-widest text-white border border-white/10">
            CREATE
          </div>
        </motion.button>

        <NavItem 
          icon={Bookmark} 
          label="SAVED" 
          isActive={activeMode === 'SAVED'} 
          onClick={() => onModeChange('SAVED')}
        />

        <NavItem 
          icon={Key} 
          label="KEYS" 
          isActive={activeMode === 'PROFILE'} 
          onClick={() => onModeChange('PROFILE')}
        />

      </div>
    </div>
  );
}

function NavItem({ 
  icon: Icon, 
  label, 
  isActive, 
  onClick
}: { 
  icon: any, 
  label: string, 
  isActive: boolean, 
  onClick: () => void 
}) {
  return (
    <button
      onClick={onClick}
      className={`
        relative px-3 sm:px-5 py-3 rounded-2xl flex flex-col items-center gap-1.5 transition-all duration-500 group select-none
        ${isActive ? 'text-[#00FF9C]' : 'text-white/40 hover:text-white/70'}
      `}
    >
      {isActive && (
        <motion.div
          layoutId="nav-active-glow"
          className="absolute inset-0 bg-[#00FF9C]/5 rounded-2xl"
          transition={{ type: 'spring', bounce: 0.2, duration: 0.6 }}
        />
      )}
      
      <Icon className={`w-5 h-5 sm:w-6 sm:h-6 transition-all duration-500 ${isActive ? 'scale-110 text-[#00FF9C]' : 'group-hover:scale-110 opacity-40'}`} />

      <span className={`text-[9px] font-black tracking-[0.15em] leading-none transition-all duration-500 ${isActive ? 'opacity-100 text-[#00FF9C]' : 'opacity-30 group-hover:opacity-100'}`}>
        {label}
      </span>
    </button>
  );
}
