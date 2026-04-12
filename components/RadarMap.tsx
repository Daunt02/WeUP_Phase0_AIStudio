"use client";

import React, {
  useEffect,
  useState,
  useCallback,
  useRef,
  useMemo,
} from "react";
import Map, { MapRef, Marker } from "react-map-gl";
import "mapbox-gl/dist/mapbox-gl.css";
import { motion, AnimatePresence } from "motion/react";
import Image from "next/image";
import {
  Activity,
  AlertCircle,
  Zap,
  Clock,
  MapPin,
  ArrowRight,
  ChevronRight,
  X,
} from "lucide-react";
import useSupercluster from "use-supercluster";
import { publicEnv } from "../lib/env/public";
import type {
  RuntimeEventProjection,
  GhostEventDraft,
} from "@/features/world/runtimeTypes";
import type { ViewStateChangeEvent } from "react-map-gl";

// Error Boundary for individual markers to prevent map crashes
class MarkerErrorBoundary extends React.Component<
  { children: React.ReactNode },
  { hasError: boolean }
> {
  constructor(props: { children: React.ReactNode }) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error: unknown, errorInfo: unknown) {
    console.error("Marker Error:", error, errorInfo);
  }

  render() {
    if (this.state.hasError) return null; // Silently fail for markers
    return this.props.children;
  }
}

interface RadarMapProps {
  events: RuntimeEventProjection[];
  onEventSelect: (event: RuntimeEventProjection) => void;
  onBoundsChange?: (bounds: {
    minLat: number;
    maxLat: number;
    minLng: number;
    maxLng: number;
  }) => void;
  onCenterChange?: (center: { lat: number; lng: number }) => void;
  onAnchorChange?: (point: { x: number; y: number } | null) => void;
  selectedEventId?: string | null;
  interestedEventId?: string | null;
  ghostEvent?: GhostEventDraft | null;
  onGhostMove?: (lat: number, lng: number) => void;
  selectedDate: string;
  activeMode?: string;
}

type ClusterProperties =
  | {
      cluster: true;
      point_count: number;
      cluster_id: number;
    }
  | {
      cluster: false;
      eventId: string;
      category: string;
      event: RuntimeEventProjection;
    };

const CATEGORY_COLORS: Record<string, string> = {
  nightlife: "text-brand-primary",
  tech: "text-cyan-400",
  culture: "text-purple-400",
  wellness: "text-emerald-400",
  default: "text-white",
};

const CATEGORY_GLOW: Record<string, string> = {
  nightlife: "shadow-[0_0_15px_rgba(var(--brand-primary-rgb),0.5)]",
  tech: "shadow-[0_0_15px_rgba(34,211,238,0.5)]",
  culture: "shadow-[0_0_15px_rgba(168,85,247,0.5)]",
  wellness: "shadow-[0_0_15px_rgba(52,211,153,0.5)]",
  default: "shadow-[0_0_15px_rgba(255,255,255,0.3)]",
};

// Light Language Glyph Marker Component
const LightLanguageMarker = ({
  event,
  isSelected,
  isInterested,
  isRecessed,
  zoom,
  onClick,
}: {
  event: RuntimeEventProjection;
  isSelected: boolean;
  isInterested: boolean;
  isRecessed: boolean;
  zoom: number;
  onClick: () => void;
}) => {
  const lat = event.latitude;
  const lng = event.longitude;

  if (!lat || !lng) return null;

  const category = event.category?.toLowerCase() || "default";
  const colorClass = CATEGORY_COLORS[category] || CATEGORY_COLORS.default;

  // Calculate brightness based on time proximity AND zoom level
  const now = new Date().getTime();
  const eventTime = new Date(event.startTime).getTime();
  const hoursUntil = (eventTime - now) / (1000 * 60 * 60);
  const timeBrightness = Math.max(0.3, Math.min(1, 1 - hoursUntil / 48));

  // Zoom factor: 12.5 is default, 18 is close
  const zoomFactor = Math.max(0.5, Math.min(1.5, (zoom - 10) / 5));
  const finalBrightness = Math.min(1, timeBrightness * zoomFactor);
  const pulseIntensity = Math.max(1, 1.4 * zoomFactor);

  // Signal Glyphs based on category
  const getSignalGlyph = () => {
    switch (category) {
      case "nightlife": // Angular, sharp
        return (
          <motion.div
            className="absolute inset-0 flex items-center justify-center"
            animate={{ rotate: [0, 90, 180, 270, 360] }}
            transition={{ duration: 10, repeat: Infinity, ease: "linear" }}
          >
            <div
              className={`w-full h-full border-2 ${colorClass.replace("text-", "border-")}/40 rounded-sm rotate-45`}
            />
          </motion.div>
        );
      case "tech": // Bloom, radiant
        return (
          <div className="absolute inset-0 flex items-center justify-center">
            <div
              className={`w-1 h-full ${colorClass.replace("text-", "bg-")}/40 animate-pulse`}
            />
            <div
              className={`w-full h-1 ${colorClass.replace("text-", "bg-")}/40 animate-pulse`}
            />
          </div>
        );
      case "culture": // Radiant, layered
        return (
          <div className="absolute inset-0 flex items-center justify-center">
            <div
              className={`w-full h-full border-2 ${colorClass.replace("text-", "border-")}/40 rounded-full scale-75`}
            />
            <div
              className={`w-full h-full border ${colorClass.replace("text-", "border-")}/20 rounded-full scale-110`}
            />
          </div>
        );
      case "wellness": // Waveform, soft
        return (
          <div className="absolute inset-0 flex items-center justify-center">
            <motion.div
              className={`w-full h-full border-2 ${colorClass.replace("text-", "border-")}/40 rounded-full`}
              animate={{ scale: [0.8, 1.2, 0.8], opacity: [0.4, 0.8, 0.4] }}
              transition={{ duration: 3, repeat: Infinity, ease: "easeInOut" }}
            />
          </div>
        );
      default:
        return (
          <div
            className={`w-2 h-2 ${colorClass.replace("text-", "bg-")}/40 rounded-full`}
          />
        );
    }
  };

  return (
    <Marker
      longitude={lng}
      latitude={lat}
      anchor="center"
      onClick={(e) => {
        e.originalEvent.stopPropagation();
        onClick();
      }}
    >
      <div className="relative flex flex-col items-center">
        <motion.div
          initial={{ opacity: 0, scale: 0 }}
          animate={{
            opacity: isRecessed ? 0.3 : finalBrightness,
            scale: isSelected ? 1.4 : isRecessed ? 0.8 : 1,
          }}
          whileHover={{ scale: 1.2, opacity: 1 }}
          className="relative group cursor-pointer"
        >
          {/* Selected Halo */}
          <AnimatePresence>
            {isSelected && (
              <motion.div
                initial={{ scale: 0.5, opacity: 0 }}
                animate={{ scale: [1, 2.5, 1], opacity: [0.2, 0.5, 0.2] }}
                exit={{ opacity: 0 }}
                transition={{
                  duration: 3,
                  repeat: Infinity,
                  ease: "easeInOut",
                }}
                className={`absolute inset-0 rounded-full ${colorClass.replace("text-", "bg-")} blur-2xl`}
              />
            )}
          </AnimatePresence>

          {/* Glyph Container */}
          <div
            className={`
            relative w-6 h-6 flex items-center justify-center transition-all duration-700
            ${isSelected ? "scale-125" : ""}
          `}
          >
            {/* Confidence Warning */}
            {event.status === "NEEDS_REVIEW" && (
              <div className="absolute inset-0 bg-amber-500/20 flex items-center justify-center rounded-full">
                <div className="w-full h-full border border-amber-500/40 rounded-full animate-pulse" />
              </div>
            )}
            {/* Outer Glow */}
            <motion.div
              className={`absolute inset-0 rounded-full ${colorClass.replace("text-", "bg-")}/10 blur-md`}
              animate={{ scale: [1, 1.2, 1], opacity: [0.3, 0.6, 0.3] }}
              transition={{ duration: 2, repeat: Infinity }}
            />

            {/* Glyph Core */}
            <div className="relative w-4 h-4 flex items-center justify-center">
              {getSignalGlyph()}
              <div
                className={`w-1.5 h-1.5 rounded-full ${colorClass.replace("text-", "bg-")} shadow-[0_0_10px_rgba(var(--brand-primary-rgb),0.8)] ${isSelected ? "scale-150" : ""} transition-transform duration-300`}
              />
            </div>

            {/* Soft Pulse */}
            <motion.div
              animate={{
                scale: [1, pulseIntensity, 1],
                opacity: [0.3, 0.7, 0.3],
              }}
              transition={{ duration: 2, repeat: Infinity }}
              className={`absolute inset-0 rounded-full border border-current opacity-20 ${colorClass}`}
            />
          </div>
        </motion.div>

        {/* Label (Visible on zoom or hover) */}
        <AnimatePresence>
          {(zoom > 14 || isSelected) && (
            <motion.div
              initial={{ opacity: 0, y: 5 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: 5 }}
              className="absolute top-full mt-2 whitespace-nowrap pointer-events-none"
            >
              <span className="text-[9px] font-black uppercase italic tracking-tighter text-white bg-black/60 backdrop-blur-md px-2 py-0.5 rounded-full border border-white/10">
                {event.title}
              </span>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </Marker>
  );
};

// Ghost Marker for Ingestion Flow
const GhostMarker = ({
  event,
  onDrag,
}: {
  event: GhostEventDraft;
  onDrag: (lat: number, lng: number) => void;
}) => {
  const lat = event?.latitude;
  const lng = event?.longitude;

  if (!lat || !lng) return null;

  return (
    <Marker
      longitude={lng}
      latitude={lat}
      anchor="center"
      draggable
      onDragEnd={(e) => onDrag(e.lngLat.lat, e.lngLat.lng)}
    >
      <div className="relative flex flex-col items-center group cursor-grab active:cursor-grabbing">
        <div className="relative w-12 h-12 flex items-center justify-center">
          {/* Pulsing Ghost Aura */}
          <motion.div
            animate={{ scale: [1, 1.5, 1], opacity: [0.2, 0.4, 0.2] }}
            transition={{ duration: 2, repeat: Infinity }}
            className="absolute inset-0 rounded-full bg-white/20 blur-xl"
          />

          {/* Ghost Signal Core */}
          <div className="relative w-8 h-8 rounded-full border-2 border-dashed border-white/40 flex items-center justify-center">
            <div className="w-2 h-2 rounded-full bg-white shadow-[0_0_15px_rgba(255,255,255,0.8)] animate-pulse" />
          </div>

          {/* Guidance Label */}
          <div className="absolute top-full mt-4 whitespace-nowrap pointer-events-none">
            <span className="text-[8px] font-mono font-black uppercase tracking-[0.2em] text-white/60 bg-black/80 backdrop-blur-md px-3 py-1 rounded-full border border-white/10">
              Drag to Position Signal
            </span>
          </div>
        </div>
      </div>
    </Marker>
  );
};

export default function RadarMap({
  events,
  onEventSelect,
  onBoundsChange,
  onCenterChange,
  onAnchorChange,
  selectedEventId,
  interestedEventId,
  ghostEvent,
  onGhostMove,
  selectedDate,
  activeMode,
}: RadarMapProps) {
  const mapRef = useRef<MapRef>(null);
  const mapboxToken = publicEnv.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN;

  const [isClient, setIsClient] = useState(false);
  const [styleLoaded, setStyleLoaded] = useState(false);
  const [mapError, setMapError] = useState<string | null>(null);
  const [viewState, setViewState] = useState({
    longitude: -95.3698,
    latitude: 29.7604,
    zoom: 12.5,
    pitch: 55,
    bearing: 0,
  });

  const [isSweeping, setIsSweeping] = useState(false);
  const [prevDate, setPrevDate] = useState(selectedDate);
  const lastAnchorPoint = useRef<{ x: number; y: number } | null>(null);

  // Trigger sweep when date changes — avoid state updates during render
  // Move logic into an effect so updates happen in lifecycle, not render.
  useEffect(() => {
    if (selectedDate !== prevDate) {
      // Defer state updates to avoid synchronous setState inside an effect
      requestAnimationFrame(() => {
        setPrevDate(selectedDate);
        setIsSweeping(true);
      });
    }
  }, [selectedDate, prevDate]);

  // Handle Anchor Point Calculation
  useEffect(() => {
    if (selectedEventId && mapRef.current) {
      const event = events.find((e) => e.id === selectedEventId);
      if (event) {
        const map = mapRef.current.getMap();
        const point = map.project([event.longitude, event.latitude]);

        // Only update if the point has actually moved significantly
        const hasMoved =
          !lastAnchorPoint.current ||
          Math.abs(lastAnchorPoint.current.x - point.x) > 0.5 ||
          Math.abs(lastAnchorPoint.current.y - point.y) > 0.5;

        if (hasMoved) {
          lastAnchorPoint.current = { x: point.x, y: point.y };
          if (onAnchorChange) onAnchorChange(point);
        }
      }
    } else {
      if (lastAnchorPoint.current !== null) {
        lastAnchorPoint.current = null;
        if (onAnchorChange) onAnchorChange(null);
      }
    }
  }, [selectedEventId, events, viewState, onAnchorChange]);

  // Handle Interest Camera Animation
  useEffect(() => {
    if (interestedEventId && mapRef.current) {
      const event = events.find((e) => e.id === interestedEventId);
      if (event) {
        mapRef.current.flyTo({
          center: [event.longitude, event.latitude],
          zoom: 15,
          pitch: 65,
          bearing: 15,
          duration: 3500,
          essential: true,
        });
      }
    }
  }, [interestedEventId, events]);

  // Reset Signal Sweep
  useEffect(() => {
    if (isSweeping) {
      const timer = setTimeout(() => setIsSweeping(false), 2000);
      return () => clearTimeout(timer);
    }
  }, [isSweeping]);

  const containerRef = useRef<HTMLDivElement>(null);

  const [mapBounds, setMapBounds] = useState<
    [number, number, number, number] | null
  >(null);

  // Floating Flyers logic: Pick 1-3 events in the current bounds
  const floatingFlyers = useMemo(() => {
    if (!mapBounds) return [];
    // Filter events in bounds and not selected
    const inBounds = events.filter((e) => {
      const lat = e.latitude;
      const lng = e.longitude;
      return (
        lat >= mapBounds[1] &&
        lat <= mapBounds[3] &&
        lng >= mapBounds[0] &&
        lng <= mapBounds[2] &&
        e.id !== selectedEventId
      );
    });
    // Pick up to 3
    return inBounds.slice(0, 3);
  }, [events, mapBounds, selectedEventId]);

  // Clustering Logic
  const points = useMemo(
    () =>
      events.map((event) => ({
        type: "Feature" as const,
        properties: {
          cluster: false,
          eventId: event.id,
          category: event.category,
          event,
        },
        geometry: {
          type: "Point" as const,
          coordinates: [event.longitude, event.latitude],
        },
      })),
    [events],
  );

  const { clusters, supercluster } = useSupercluster({
    points,
    bounds: mapBounds || [-95.6, 29.5, -95.1, 30.0],
    zoom: viewState.zoom,
    options: { radius: 75, maxZoom: 20 },
  });

  useEffect(() => {
    const mountTimer = setTimeout(() => setIsClient(true), 0);
    return () => clearTimeout(mountTimer);
  }, []);

  const handleMove = useCallback(
    (evt: ViewStateChangeEvent) => {
      setViewState(evt.viewState);
      if (onCenterChange) {
        onCenterChange({
          lat: evt.viewState.latitude,
          lng: evt.viewState.longitude,
        });
      }
      if (mapRef.current) {
        const bounds = mapRef.current.getBounds();
        if (bounds) {
          setMapBounds(
            bounds.toArray().flat() as [number, number, number, number],
          );
          if (onBoundsChange) {
            onBoundsChange({
              minLat: bounds.getSouth(),
              maxLat: bounds.getNorth(),
              minLng: bounds.getWest(),
              maxLng: bounds.getEast(),
            });
          }
        }
      }
    },
    [onBoundsChange, onCenterChange],
  );

  const handleLoad = useCallback(() => {
    setStyleLoaded(true);
    const map = mapRef.current?.getMap();
    if (map) {
      // Subdue the map to a "shadow" layer but keep it readable
      const layers = map.getStyle().layers;
      if (layers) {
        layers.forEach((layer) => {
          // Hide labels and symbols to reduce clutter
          if (layer.type === "symbol" || layer.id.includes("label")) {
            map.setLayoutProperty(layer.id, "visibility", "none");
          }
          // Dim roads, buildings, and water - but keep them sharp
          if (["line", "fill", "fill-extrusion"].includes(layer.type)) {
            try {
              map.setPaintProperty(layer.id, `${layer.type}-opacity`, 0.45); // Increased opacity for readability
            } catch (e) {
              // Some layers might not support opacity
            }
          }
        });
      }

      const bounds = map.getBounds();
      if (bounds) {
        setMapBounds(
          bounds.toArray().flat() as [number, number, number, number],
        );
        if (onBoundsChange) {
          onBoundsChange({
            minLat: bounds.getSouth(),
            maxLat: bounds.getNorth(),
            minLng: bounds.getWest(),
            maxLng: bounds.getEast(),
          });
        }
      }
      try {
        // Subtle Fog - Not heavy
        map.setFog({
          range: [1, 20], // Relaxed range
          color: "#050505",
          "high-color": "#080808",
          "space-color": "#000000",
          "horizon-blend": 0.01,
        });
      } catch (err) {
        console.error("Error adding 3D layers:", err);
      }
      map.resize();
    }
  }, [onBoundsChange]);

  const handleError = useCallback(
    (e: { error?: { message?: string }; message?: string }) => {
      console.error("Minimal Map Error:", e);
      setMapError(e?.error?.message || e?.message || JSON.stringify(e));
    },
    [],
  );

  return (
    <div
      ref={containerRef}
      className="absolute inset-0 bg-[#020202] overflow-hidden z-[1]"
    >
      {/* TONIGHT / NOW CHIP */}
      <div className="absolute top-6 left-1/2 -translate-x-1/2 z-50 pointer-events-none">
        <motion.div
          initial={{ y: -20, opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          className="flex items-center gap-3 px-4 py-2 bg-black/60 backdrop-blur-xl border border-white/10 rounded-full shadow-2xl"
        >
          <div className="flex items-center gap-2">
            <div className="w-1.5 h-1.5 rounded-full bg-brand-primary animate-pulse shadow-[0_0_8px_rgba(var(--brand-primary-rgb),0.8)]" />
            <span className="text-[10px] font-black uppercase italic tracking-tighter text-white">
              Live Radar
            </span>
          </div>
          <div className="w-px h-3 bg-white/10" />
          <span className="text-[10px] font-mono text-white/60 uppercase tracking-widest">
            {isClient &&
              new Date().toLocaleTimeString([], {
                hour: "2-digit",
                minute: "2-digit",
                hour12: false,
              })}
          </span>
        </motion.div>
      </div>

      {mapboxToken ? (
        <div className="w-full h-full">
          {/* Signal Sweep Overlay */}
          <AnimatePresence>
            {isSweeping && (
              <motion.div
                initial={{ scale: 0, opacity: 0 }}
                animate={{ scale: 4, opacity: [0, 0.1, 0] }}
                exit={{ opacity: 0 }}
                transition={{ duration: 2, ease: "easeOut" }}
                className="fixed inset-0 z-[50] pointer-events-none flex items-center justify-center"
              >
                <div className="w-64 h-64 rounded-full border-2 border-brand-primary/20 blur-xl" />
              </motion.div>
            )}
          </AnimatePresence>

          <Map
            ref={mapRef}
            {...viewState}
            onMove={handleMove}
            mapStyle="mapbox://styles/mapbox/dark-v11"
            mapboxAccessToken={mapboxToken}
            style={{ width: "100%", height: "100%" }}
            onLoad={handleLoad}
            onError={handleError}
          >
            {clusters.map((cluster) => {
              const [longitude, latitude] = cluster.geometry.coordinates;
              const props = cluster.properties as ClusterProperties;
              const isCluster = props.cluster;

              if (isCluster) {
                return (
                  <Marker
                    key={`cluster-${cluster.id}`}
                    latitude={latitude}
                    longitude={longitude}
                  >
                    <div
                      className="flex items-center justify-center w-10 h-10 rounded-full bg-white/5 border border-white/10 backdrop-blur-md cursor-pointer group hover:scale-110 transition-transform"
                      onClick={() => {
                        if (!supercluster || !mapRef.current) return;
                        const expansionZoom = Math.min(
                          supercluster.getClusterExpansionZoom(
                            props.cluster_id,
                          ),
                          20,
                        );

                        mapRef.current.flyTo({
                          center: [longitude, latitude],
                          zoom: expansionZoom,
                          duration: 1000,
                          essential: true,
                        });
                      }}
                    >
                      <div className="w-8 h-8 rounded-full bg-white/10 flex items-center justify-center text-white font-black text-xs">
                        {props.point_count}
                      </div>
                    </div>
                  </Marker>
                );
              }

              const event = props.event;
              const eventDate = new Date(event.startTime);
              const eventDay = eventDate.getDate();
              const selectedDay = parseInt(selectedDate.split(" ")[1]);
              const isRecessed = eventDay !== selectedDay;

              return (
                <MarkerErrorBoundary key={props.eventId}>
                  <LightLanguageMarker
                    event={event}
                    isSelected={selectedEventId === props.eventId}
                    isInterested={interestedEventId === props.eventId}
                    isRecessed={isRecessed}
                    zoom={viewState.zoom}
                    onClick={() => onEventSelect(event)}
                  />
                </MarkerErrorBoundary>
              );
            })}

            {/* Ghost Marker */}
            {ghostEvent && onGhostMove && (
              <GhostMarker event={ghostEvent} onDrag={onGhostMove} />
            )}
          </Map>
        </div>
      ) : (
        <div className="absolute inset-0 flex items-center justify-center bg-black">
          <div className="text-red-500 font-mono">MISSING TOKEN</div>
        </div>
      )}

      {/* Error Overlay */}
      {mapError && (
        <div className="absolute inset-0 z-[150] flex items-center justify-center bg-black/80 backdrop-blur-md">
          <div className="p-8 border border-red-500/50 bg-black rounded-3xl text-center max-w-lg">
            <h2 className="text-red-500 text-2xl font-black mb-4 uppercase italic tracking-tighter">
              Map Render Failed
            </h2>
            <p className="text-white/70 font-mono text-sm mb-6">{mapError}</p>
            <button
              onClick={() => window.location.reload()}
              className="px-6 py-3 bg-white/10 hover:bg-white/20 border border-white/20 rounded-full text-xs font-mono uppercase tracking-widest transition-all"
            >
              Force Reload
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
