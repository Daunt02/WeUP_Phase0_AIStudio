'use client';

import React, { useState, useMemo, useCallback } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import TopBar from '@/components/TopBar';
import RadarMap from '@/components/RadarMap';
import BottomNav from '@/components/BottomNav';
import CulturalCalendar from '@/components/CulturalCalendar';
import SocialSignalPanel from '@/components/SocialSignalPanel';
import SavedEvents from '@/components/SavedEvents';
import ProfilePanel from '@/components/ProfilePanel';
import TimelineControl from '@/components/TimelineControl';
import EventSignalModal from '@/components/EventSignalModal';
import GeoControls from '@/components/GeoControls';
import AddEventModal from '@/components/AddEventModal';
import { NightlifeItem, ViewMode, UIState, ModalState } from '@/types';
import { MOCK_EVENTS } from '@/constants/mockData';
import { eventService, BoundingBox } from '@/services/eventService';
import { AlertCircle } from 'lucide-react';

// Top-level Error Boundary
class GlobalErrorBoundary extends React.Component<{ children: React.ReactNode }, { hasError: boolean, error: any }> {
  constructor(props: { children: React.ReactNode }) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: any) {
    "use client";

    import WorldCoordinator from '@/features/world/WorldCoordinator';

    export default function Page() {
      return <WorldCoordinator />;
    }
          <div className="max-w-md w-full space-y-6 text-center">
