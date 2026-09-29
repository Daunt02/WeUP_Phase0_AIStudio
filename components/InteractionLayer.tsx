'use client';

import React from 'react';
import { AnimatePresence, motion } from 'motion/react';
import TopBar from './TopBar';
import BottomNav from './BottomNav';
import TimelineControl from './TimelineControl';
import { ViewMode } from '@/types';
import { Search, Filter, SlidersHorizontal, MapPin, Bot } from 'lucide-react';

interface InteractionLayerProps {
  isIngesting: boolean;
  isDiscovering?: boolean;
  onDiscover?: () => void;
  activeMode: ViewMode;
  onModeChange: (mode: ViewMode) => void;
  onAction: (action: any) => void;
  isQuickScrubbing: boolean;
  onTimeChange: (time: string) => void;
  selectedTime?: string;
  depth: number;
  onRecenter: () => void;
  onOpenAssistant: () => void;
  filters?: {
    categories: string[];
    signalsOnly?: boolean;
    timeframe?: string;
    radius?: number;
  };
  searchQuery?: string;
  onSearchChange?: (query: string) => void;
}

/**
 * InteractionLayer: Handles global UI controls and gesture intercepts.
 * Always visible at Depth 1 (z-index 100-299).
 */
export default function InteractionLayer({
  isIngesting,
  isDiscovering,
  onDiscover,
  activeMode,
  onModeChange,
  onAction,
  isQuickScrubbing,
  onTimeChange,
  selectedTime = 'NOW',
  depth,
  onRecenter,
  onOpenAssistant,
  filters = { categories: [], signalsOnly: false },
  searchQuery = '',
  onSearchChange
}: InteractionLayerProps) {
  // Hide controls if we are deep in a modal (Depth 2+)
  const isControlsVisible = depth < 2;
  const CATEGORIES = ['nightlife', 'lounge', 'concert', 'private', 'restaurant', 'rooftop', 'startup'];
  const hasActiveFilters = (filters.categories && filters.categories.length > 0) || filters.signalsOnly || searchQuery.trim().length > 0;

  return (
    <div className="fixed inset-0 z-[100] pointer-events-none">
      {/* Top Controls Overlay */}
      <motion.div 
        animate={{ y: isControlsVisible ? 0 : -100, opacity: isControlsVisible ? 1 : 0 }}
        className="fixed top-0 inset-x-0 pointer-events-none flex flex-col pt-safe"
      >
        <TopBar isIngesting={isIngesting} isDiscovering={isDiscovering} onDiscover={onDiscover} />
        
        {/* Search & Filters */}
        {activeMode === 'DISCOVER' && depth === 0 && (
          <motion.div 
            initial={{ opacity: 0, y: -20 }}
            animate={{ opacity: 1, y: 0 }}
            className="px-6 flex flex-col gap-3 mt-2"
          >
            {/* Search Input Area */}
            <div className="flex items-center gap-3 bg-black/70 backdrop-blur-2xl border border-white/10 rounded-2xl px-4 h-13 pointer-events-auto shadow-2xl transition-all hover:border-white/20 focus-within:border-[#00FF9C]">
              <Search className="w-4 h-4 text-white/40 shrink-0" />
              <input 
                type="text" 
                value={searchQuery}
                onChange={(e) => onSearchChange?.(e.target.value)}
                placeholder="Find signals, venues or vibe..." 
                className="bg-transparent border-none focus:outline-none focus:ring-0 text-white font-mono text-[10px] uppercase tracking-widest w-full placeholder:text-white/25"
              />
              {searchQuery && (
                <button 
                  onClick={() => onSearchChange?.('')}
                  className="p-1 hover:bg-white/10 rounded-full text-white/40 hover:text-white transition-colors"
                >
                  <span className="text-[10px] font-bold">✕</span>
                </button>
              )}
              <button 
                onClick={onOpenAssistant}
                title="Open Assistant Filters"
                className="p-2 hover:bg-white/10 rounded-xl text-white/40 hover:text-[#00FF9C] transition-colors"
              >
                <SlidersHorizontal className="w-4 h-4" />
              </button>
            </div>

            {/* Filter Chips */}
            <div className="flex items-center gap-2 overflow-x-auto no-scrollbar pb-1 pointer-events-auto select-none">
              {hasActiveFilters && (
                <button 
                  onClick={() => onAction({ type: 'CLEAR_FILTERS' })}
                  className="flex-shrink-0 bg-red-500/20 border border-red-500/30 text-red-400 px-3 h-8 rounded-full flex items-center gap-1.5 text-[8px] font-black tracking-widest uppercase hover:bg-red-500/30 transition-all cursor-pointer"
                >
                  CLEAR_ALL
                </button>
              )}

              <button 
                onClick={() => onAction({ type: 'TOGGLE_SIGNALS_ONLY' })}
                className={`flex-shrink-0 px-3.5 h-8 rounded-full flex items-center gap-1.5 text-[8px] font-black tracking-widest uppercase transition-all duration-300 cursor-pointer ${
                  filters.signalsOnly 
                    ? 'bg-[#00FF9C] text-black shadow-[0_0_16px_rgba(0,255,156,0.6)] font-extrabold' 
                    : 'bg-white/5 backdrop-blur-md border border-white/10 text-white/60 hover:text-white hover:bg-white/10'
                }`}
              >
                <Filter className="w-3 h-3" />
                SIGNALS_ONLY
              </button>

              {CATEGORIES.map(cat => {
                const isSelected = filters.categories?.some(c => c.toLowerCase() === cat.toLowerCase());
                return (
                  <button 
                    key={cat}
                    onClick={() => onAction({ type: 'TOGGLE_FILTER', category: cat })}
                    className={`flex-shrink-0 px-3.5 h-8 rounded-full text-[8px] font-black tracking-widest uppercase transition-all duration-300 cursor-pointer ${
                      isSelected
                        ? 'bg-[#00FF9C] text-black shadow-[0_0_16px_rgba(0,255,156,0.5)] border border-[#00FF9C]'
                        : 'bg-white/5 backdrop-blur-md border border-white/10 text-white/60 hover:text-white hover:bg-white/10'
                    }`}
                  >
                    {cat}
                  </button>
                );
              })}
            </div>
          </motion.div>
        )}
      </motion.div>

      {/* Bottom Controls */}
      <div className="fixed inset-x-0 bottom-0 pointer-events-none flex flex-col items-center">
      <AnimatePresence mode="wait">
        {isQuickScrubbing && (
          <motion.div
            key="quick-scrubber"
            initial={{ opacity: 0, scale: 0.9, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.9, y: 20 }}
            className="mb-12 pointer-events-auto"
          >
            <TimelineControl initialTime={selectedTime} onTimeChange={onTimeChange} />
          </motion.div>
        )}
      </AnimatePresence>

        <motion.div
          animate={{ y: isControlsVisible ? 0 : 150, opacity: isControlsVisible ? 1 : 0 }}
          className="w-full pointer-events-auto"
        >
          <div className="flex flex-col items-end gap-3 px-6 mb-4 w-full max-w-lg mx-auto">
            {/* Spike AI Co-pilot Floating Trigger Button */}
            <motion.button
              whileHover={{ scale: 1.1 }}
              whileTap={{ scale: 0.9 }}
              onClick={onOpenAssistant}
              className="relative w-12 h-12 bg-black/80 backdrop-blur-xl border border-[#00FF9C]/30 hover:border-[#00FF9C] rounded-2xl flex items-center justify-center text-[#00FF9C] transition-all shadow-[0_0_20px_rgba(0,255,156,0.2)]"
              title="Spike Co-Pilot"
            >
              <div className="absolute -top-1 -right-1 w-3 h-3 bg-[#00FF9C] rounded-full border-2 border-black flex items-center justify-center">
                <span className="w-1 h-1 bg-black rounded-full animate-ping" />
              </div>
              <Bot className="w-5 h-5 animate-pulse" />
            </motion.button>

            <motion.button
              whileHover={{ scale: 1.1 }}
              whileTap={{ scale: 0.9 }}
              onClick={onRecenter}
              className="w-12 h-12 bg-black/60 backdrop-blur-xl border border-white/10 rounded-2xl flex items-center justify-center text-white/60 hover:text-[#00FF9C] transition-colors shadow-2xl"
            >
              <MapPin className="w-5 h-5" />
            </motion.button>
          </div>
          <BottomNav 
            activeMode={activeMode} 
            onModeChange={onModeChange}
            onAction={onAction}
          />
        </motion.div>
      </div>
      
      {/* Global Gesture Intercepts can be added here */}
    </div>
  );
}
