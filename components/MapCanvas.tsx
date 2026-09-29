'use client';

import React, { useRef, useEffect, useState, useCallback, useMemo } from 'react';
import { motion } from 'motion/react';
import Map, { MapRef, Marker, NavigationControl } from 'react-map-gl';
import useSupercluster from 'use-supercluster';
import { NightlifeItem, ViewMode, BoundingBox } from '@/types';
import { MapPin, Users, Music, Star, Zap } from 'lucide-react';
import VenuePulse from './VenuePulse';

// Accessing public env var for Mapbox token
const MAPBOX_TOKEN = process.env.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN;

// Resilient Dark Matter basemap style (zero-token raster fallback)
const CARTO_DARK_STYLE: any = {
  version: 8,
  sources: {
    'carto-dark': {
      type: 'raster',
      tiles: [
        'https://a.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png',
        'https://b.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png',
        'https://c.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png',
      ],
      tileSize: 256,
      attribution: '&copy; OpenStreetMap contributors &copy; CARTO',
    },
  },
  layers: [
    {
      id: 'carto-dark-layer',
      type: 'raster',
      source: 'carto-dark',
      minzoom: 0,
      maxzoom: 22,
    },
  ],
};

interface MapCanvasProps {
  events: NightlifeItem[];
  onEventSelect: (event: NightlifeItem) => void;
  onBoundsChange: (bounds: BoundingBox) => void;
  onCenterChange: (center: { lat: number, lng: number }) => void;
  onAnchorChange: (point: { x: number, y: number } | null) => void;
  mapCenter?: { lat: number, lng: number };
  selectedEventId: string | null;
  interestedEventId: string | null;
  highlightedEventIds?: string[];
  selectedDate: string;
  activeMode: ViewMode;
  ghostEvent: Partial<NightlifeItem> | null;
  onGhostMove: (lat: number, lng: number) => void;
  onClusterSelect?: (events: NightlifeItem[]) => void;
}

const CATEGORY_ICONS: Record<string, any> = {
  nightlife: Zap,
  lounge: Music,
  concert: Music,
  private: Users,
  restaurant: Star,
  rooftop: Star,
  startup: Zap,
};

const CATEGORY_COLORS: Record<string, string> = {
  nightlife: '#00FF9C',
  lounge: '#A855F7',
  concert: '#F43F5E',
  private: '#FBBF24',
  restaurant: '#3B82F6',
  rooftop: '#10B981',
  startup: '#6366F1',
};

export default function MapCanvas({
  events,
  onEventSelect,
  onBoundsChange,
  onCenterChange,
  onAnchorChange,
  mapCenter,
  selectedEventId,
  interestedEventId,
  highlightedEventIds = [],
  activeMode,
  ghostEvent,
  onGhostMove,
  onClusterSelect
}: MapCanvasProps) {
  const mapRef = useRef<MapRef>(null);
  const [viewState, setViewState] = useState({
    longitude: -95.3698, // Houston default
    latitude: 29.7604,
    zoom: 13,
    pitch: 45,
    bearing: 0
  });

  // Sync with external center Prop
  useEffect(() => {
    if (mapCenter) {
      setViewState(prev => ({
        ...prev,
        latitude: mapCenter.lat,
        longitude: mapCenter.lng,
      }));
    }
  }, [mapCenter]);

  const [mounted, setMounted] = useState(false);
  useEffect(() => {
    setMounted(true);
  }, []);

  // --- Clustering Logic ---
  const points = useMemo(() => events.map(event => ({
    type: 'Feature',
    properties: { cluster: false, eventId: event.id, category: event.category, event },
    geometry: {
      type: 'Point',
      coordinates: [
        event.longitude || event.coordinates?.lng || 0,
        event.latitude || event.coordinates?.lat || 0
      ]
    }
  })), [events]);

  // Group events by venue name to calculate event density (signal density)
  const venueEventCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    events.forEach((item) => {
      const vName = item.venue_name || '';
      if (vName) {
        counts[vName] = (counts[vName] || 0) + 1;
      }
    });
    return counts;
  }, [events]);

  const [bounds, setBounds] = useState<any>(null);

  const { clusters, supercluster } = useSupercluster({
    points,
    bounds,
    zoom: viewState.zoom,
    options: { radius: 75, maxZoom: 20 }
  });

  const hasMapboxToken = Boolean(MAPBOX_TOKEN && MAPBOX_TOKEN.trim() !== '' && MAPBOX_TOKEN !== 'undefined');
  const [useFallbackStyle, setUseFallbackStyle] = useState<boolean>(!hasMapboxToken);

  const handleMove = useCallback((evt: any) => {
    setViewState(evt.viewState);
    const center = { 
      lat: evt.viewState.latitude ?? 29.7604, 
      lng: evt.viewState.longitude ?? -95.3698 
    };
    onCenterChange(center);
    
    if (mapRef.current) {
      const b = mapRef.current.getBounds();
      if (b) {
        const boundsArray = [b.getWest(), b.getSouth(), b.getEast(), b.getNorth()];
        setBounds(boundsArray);
        onBoundsChange({
          north: b.getNorth(),
          south: b.getSouth(),
          east: b.getEast(),
          west: b.getWest()
        });
      }
    }
  }, [onBoundsChange, onCenterChange]);

  const handleMapError = useCallback(() => {
    // Gracefully fallback to Carto Dark tiles when Mapbox tile fetching fails (401/403/invalid token/network)
    setUseFallbackStyle(true);
  }, []);

  if (!mounted) return <div className="absolute inset-0 bg-black" />;

  const activeMapStyle = useFallbackStyle ? CARTO_DARK_STYLE : "mapbox://styles/mapbox/dark-v11";
  const activeToken = useFallbackStyle ? undefined : MAPBOX_TOKEN;

  return (
    <div className="absolute inset-0 z-0">
      <Map
        {...viewState}
        ref={mapRef}
        onMove={handleMove}
        onError={handleMapError}
        mapStyle={activeMapStyle}
        mapboxAccessToken={activeToken}
        style={{ width: '100%', height: '100%' }}
        cursor={activeMode === 'CREATE' ? 'crosshair' : 'grab'}
        onClick={(e) => {
          if (activeMode === 'CREATE') {
            onGhostMove(e.lngLat.lat, e.lngLat.lng);
          } else {
            onAnchorChange(null);
          }
        }}
      >
        {clusters.map((cluster) => {
          const [longitude, latitude] = cluster.geometry.coordinates;
          const { cluster: isCluster, point_count: pointCount } = cluster.properties;

          if (isCluster) {
            return (
              <Marker
                key={`cluster-${cluster.id}`}
                longitude={longitude}
                latitude={latitude}
              >
                <motion.div
                  initial={{ scale: 0.8, opacity: 0 }}
                  animate={{ scale: 1, opacity: 1 }}
                  whileHover={{ scale: 1.1 }}
                  className="relative cursor-pointer group"
                  onClick={(e) => {
                    e.stopPropagation();
                    if (onClusterSelect) {
                      try {
                        const leaves = supercluster.getLeaves(cluster.id, Infinity);
                        const clusterEvents = leaves.map((leaf: any) => leaf.properties.event as NightlifeItem);
                        onClusterSelect(clusterEvents);
                      } catch (err) {
                        console.error('Failed to resolve cluster leaves:', err);
                        // fallback zoom
                        const expansionZoom = Math.min(
                          supercluster.getClusterExpansionZoom(cluster.id),
                          20
                        );
                        setViewState({
                          ...viewState,
                          latitude,
                          longitude,
                          zoom: expansionZoom,
                        });
                      }
                    } else {
                      const expansionZoom = Math.min(
                        supercluster.getClusterExpansionZoom(cluster.id),
                        20
                      );
                      setViewState({
                        ...viewState,
                        latitude,
                        longitude,
                        zoom: expansionZoom,
                      });
                    }
                  }}
                >
                  {/* Intensity Glow */}
                  <div className={`absolute inset-0 rounded-full bg-[#00FF9C]/20 blur-xl animate-pulse group-hover:bg-[#00FF9C]/40 transition-colors`} />
                  
                  <div className="relative w-12 h-12 rounded-2xl bg-black/60 backdrop-blur-2xl border border-white/20 flex flex-col items-center justify-center shadow-2xl overflow-hidden">
                    <div className="absolute top-0 inset-x-0 h-[1px] bg-gradient-to-r from-transparent via-[#00FF9C] to-transparent opacity-40" />
                    <span className="text-[12px] font-black font-mono text-[#00FF9C]">{pointCount}</span>
                    <span className="text-[6px] font-black text-white/40 tracking-widest uppercase">Signals</span>
                  </div>
                </motion.div>
              </Marker>
            );
          }

          const event = cluster.properties.event as NightlifeItem;
          const isSelected = event.id === selectedEventId;
          const isHighlighted = highlightedEventIds.includes(event.id);
          const isInterested = event.id === interestedEventId;
          const CategoryIcon = CATEGORY_ICONS[event.category] || MapPin;
          const categoryColor = CATEGORY_COLORS[event.category] || '#ffffff';

          // Calculate dynamic signal strength based on venue event count density, custom energy rating & recency
          const venueName = event.venue_name || '';
          const count = venueEventCounts[venueName] || 1;
          const energy = event.energyLevel || 5; 
          
          let recencyScore = 1.0;
          if (event.start_time) {
            const eventDate = new Date(event.start_time).getTime();
            const currentDate = new Date().getTime(); // Current platform local time
            const diffMs = Math.abs(currentDate - eventDate);
            const oneDayMs = 24 * 60 * 60 * 1000;
            recencyScore = Math.max(0.1, 1 - (diffMs / (oneDayMs * 7))); // Decays over 7 days down to 0.1 minimum
          }

          const rawStrength = (count * 0.25) + (energy * 0.4) + (recencyScore * 0.35);
          const strengthMetric = Math.min(1.0, Math.max(0.1, rawStrength));

          return (
            <Marker
              key={`event-${event.id}`}
              longitude={longitude}
              latitude={latitude}
              anchor="bottom"
              onClick={(e) => {
                e.originalEvent?.stopPropagation?.();
                onEventSelect(event);
                if (e.originalEvent) {
                  onAnchorChange({ x: e.originalEvent.clientX, y: e.originalEvent.clientY });
                } else {
                  onAnchorChange({ x: window.innerWidth / 2, y: window.innerHeight / 2 });
                }
              }}
            >
              <div 
                className={`cursor-pointer transition-all duration-300 relative ${
                  isSelected ? 'scale-125 z-50' : 
                  isInterested ? 'scale-110 z-40' : 
                  isHighlighted ? 'scale-105 z-30' : 'scale-100 z-20'
                }`}
              >
                {/* Visual Glow Wave / Pulse animation reflecting specific Venue hotspot strength */}
                <VenuePulse
                  color={categoryColor}
                  strength={strengthMetric}
                  isSelected={isSelected}
                  isInterested={isInterested}
                />

                <div className={`
                  w-8 h-8 rounded-full border-2 flex items-center justify-center overflow-hidden
                  ${isSelected ? 'bg-white border-white shadow-[0_0_20px_rgba(255,255,255,0.4)]' : 
                    isInterested ? 'bg-black/90 border-[#00FF9C]' :
                    isHighlighted ? 'bg-white/90 border-white' : 'bg-black/90 border-white/20'}
                `}
                style={{ borderColor: isSelected ? 'white' : categoryColor }}
                >
                  <CategoryIcon className={`w-4 h-4 ${isSelected ? 'text-black' : ''}`} style={{ color: isSelected ? 'black' : categoryColor }} />
                </div>
                
                {isSelected && (
                  <div className="absolute top-full left-1/2 -translate-x-1/2 pt-2 pointer-events-none">
                    <div className="bg-black/90 backdrop-blur-xl border border-white/20 px-3 py-1.5 rounded-full text-[9px] font-black whitespace-nowrap uppercase tracking-[0.2em] shadow-2xl">
                      {event.title}
                    </div>
                  </div>
                )}
              </div>
            </Marker>
          );
        })}

        {/* Ghost Marker for Creation/Upload */}
        {activeMode === 'CREATE' && ghostEvent && (ghostEvent.latitude || ghostEvent.coordinates?.lat) && (
          <Marker
            longitude={ghostEvent.longitude || ghostEvent.coordinates?.lng || 0}
            latitude={ghostEvent.latitude || ghostEvent.coordinates?.lat || 0}
            anchor="bottom"
            draggable
            onDrag={(e) => onGhostMove(e.lngLat.lat, e.lngLat.lng)}
          >
            <div className="w-12 h-12 rounded-full bg-white/10 border-2 border-white border-dashed flex items-center justify-center animate-pulse">
              <Zap className="w-5 h-5 text-white" />
            </div>
          </Marker>
        )}

        <div className="absolute bottom-32 right-6">
          <NavigationControl showCompass={false} />
        </div>
      </Map>
    </div>
  );
}
