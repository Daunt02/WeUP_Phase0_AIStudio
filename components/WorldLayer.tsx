'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import { NightlifeItem, ViewMode, BoundingBox } from '@/types';

const MapCanvas = dynamic(() => import('./MapCanvas'), { 
  ssr: false,
  loading: () => (
    <div className="absolute inset-0 bg-[#020202] flex items-center justify-center z-[1]">
      <div className="text-white/20 font-mono text-[10px] uppercase tracking-[0.2em] animate-pulse">Loading_Radar_System...</div>
    </div>
  )
});

interface WorldLayerProps {
  events: NightlifeItem[];
  onEventSelect: (event: NightlifeItem) => void;
  onBoundsChange: (bounds: BoundingBox) => void;
  onCenterChange: (center: { lat: number, lng: number }) => void;
  onAnchorChange: (point: { x: number, y: number } | null) => void;
  mapCenter: { lat: number, lng: number };
  selectedEventId: string | null;
  interestedEventId: string | null;
  highlightedEventIds?: string[];
  selectedDate: string;
  activeMode: ViewMode;
  ghostEvent: Partial<NightlifeItem> | null;
  onGhostMove: (lat: number, lng: number) => void;
  depth: number;
  onClusterSelect?: (events: NightlifeItem[]) => void;
}

/**
 * WorldLayer: The permanent root map layer.
 * Never unmounts. Recesses (blurs/scales) based on depth.
 */
export default function WorldLayer({
  events,
  onEventSelect,
  onBoundsChange,
  onCenterChange,
  onAnchorChange,
  mapCenter,
  selectedEventId,
  interestedEventId,
  highlightedEventIds,
  selectedDate,
  activeMode,
  ghostEvent,
  onGhostMove,
  depth,
  onClusterSelect
}: WorldLayerProps) {
  // Calculate recession based on depth
  const recessionStyles = {
    0: 'blur-0 scale-100 opacity-100',
    1: 'blur-[2px] scale-[1.02] opacity-80',
    2: 'blur-xl scale-110 opacity-40 grayscale-[0.5]',
    3: 'blur-2xl scale-125 opacity-20 grayscale'
  }[depth as 0 | 1 | 2 | 3] || 'blur-0 scale-100 opacity-100';

  return (
    <div className={`fixed inset-0 transition-all duration-1000 ease-in-out z-[1] ${recessionStyles}`}>
      <MapCanvas 
        events={events} 
        onEventSelect={onEventSelect}
        onBoundsChange={onBoundsChange}
        onCenterChange={onCenterChange}
        onAnchorChange={onAnchorChange}
        mapCenter={mapCenter}
        selectedEventId={selectedEventId}
        interestedEventId={interestedEventId}
        highlightedEventIds={highlightedEventIds}
        selectedDate={selectedDate}
        activeMode={activeMode}
        ghostEvent={ghostEvent}
        onGhostMove={onGhostMove}
        onClusterSelect={onClusterSelect}
      />
      
      {/* Depth Vignette - Intensifies with depth */}
      <div 
        className="pointer-events-none fixed inset-0 z-[50] transition-opacity duration-1000"
        style={{ 
          boxShadow: `inset 0 0 ${150 + depth * 50}px rgba(0,0,0,${0.8 + depth * 0.05})`,
          opacity: 0.5 + depth * 0.15
        }} 
      />
    </div>
  );
}
