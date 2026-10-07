<template>
  <!--
    WEUP-SYNTH:
    source=components/VenuePulse.tsx
    destination=frontend-vue/src/components/VenuePulseLayer.vue
    mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
    notes=Venue activity pulses as a non-interactive map layer inside MapSurface.
    Pulse centers/intensity derive from canonical markers only (no invented
    energy scores). Motion is terminating (single expansion+fade, <=2.5s);
    reduced-motion renders a static halo. This is state-bearing telemetry,
    not decoration: pulses re-trigger only when the canonical venue set changes.
  -->
  <div class="weup-pulse-layer-host" aria-hidden="true" />
</template>

<script setup lang="ts">
import { onBeforeUnmount, watch } from "vue";
import mapboxgl from "mapbox-gl";
import {
  derivePulseVenues,
  type EventMapMarkerViewModel,
  type PulseVenue,
} from "./mapWorld";

const props = withDefaults(
  defineProps<{
    /** Live mapboxgl.Map instance; pulses attach once it exists. */
    map?: unknown;
    /** Canonical markers (from useMapEvents). Never mutated here. */
    markers?: EventMapMarkerViewModel[];
    /** Maximum pulse venues (DOM-marker bound). Default 12. */
    maxVenues?: number;
  }>(),
  {
    map: undefined,
    markers: () => [],
    maxVenues: 12,
  },
);

const RING_SIZE_PX = 44;

let pulseMarkers: mapboxgl.Marker[] = [];
let lastSignature = "";

function prefersReducedMotion(): boolean {
  return (
    typeof window !== "undefined" &&
    typeof window.matchMedia === "function" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches
  );
}

function venueSignature(venues: readonly PulseVenue[]): string {
  return venues
    .map(
      (venue) =>
        `${venue.venueName}:${venue.eventCount}:${venue.anySaved ? 1 : 0}:${venue.anySelected ? 1 : 0}`,
    )
    .join("|");
}

function clearPulses(): void {
  for (const marker of pulseMarkers) {
    marker.remove();
  }
  pulseMarkers = [];
}

function buildPulseElement(venue: PulseVenue): HTMLElement {
  const host = document.createElement("div");
  /* WEUP-2.5D (D07): cluster state hooks. Deterministic: anySelected is the
     single canonical selection signal, so at most one pulse is "selected". */
  host.className = "weup-pulse weup-animatable";
  host.setAttribute("data-plane", "z2");
  host.setAttribute("data-state", venue.anySelected ? "selected" : "visible");
  host.style.setProperty("--weup-pulse-peak", venue.intensity.toFixed(2));
  host.style.width = `${RING_SIZE_PX}px`;
  host.style.height = `${RING_SIZE_PX}px`;

  // Steady state: faint accent halo. Always present, never animated.
  const halo = document.createElement("div");
  halo.className = "weup-pulse-halo";
  host.appendChild(halo);

  // Terminating expansion: a single wave that fades out. Re-triggered only
  // when the canonical venue set changes (state-bearing), never looped.
  if (!prefersReducedMotion()) {
    const wave = document.createElement("div");
    wave.className = "weup-pulse-wave";
    host.appendChild(wave);

    if (venue.intensity > 0.5) {
      const echo = document.createElement("div");
      echo.className = "weup-pulse-wave weup-pulse-wave-echo";
      host.appendChild(echo);
    }
  }

  return host;
}

function renderPulses(): void {
  clearPulses();

  const currentMap = props.map as mapboxgl.Map | undefined;
  if (!currentMap || typeof currentMap.getContainer !== "function") {
    return;
  }

  const venues = derivePulseVenues(props.markers, {
    maxVenues: props.maxVenues,
  });
  const signature = venueSignature(venues);
  if (signature === lastSignature) {
    // Same canonical venue set — do not re-trigger the expansion.
    return;
  }
  lastSignature = signature;

  for (const venue of venues) {
    const element = buildPulseElement(venue);
    const marker = new mapboxgl.Marker({ element, anchor: "center" })
      .setLngLat([venue.centerLng, venue.centerLat])
      .addTo(currentMap);
    pulseMarkers.push(marker);
  }
}

watch(
  [() => props.map, () => props.markers, () => props.maxVenues],
  () => {
    renderPulses();
  },
  { deep: true },
);

onBeforeUnmount(() => {
  clearPulses();
  lastSignature = "";
});
</script>

<style scoped>
.weup-pulse-layer-host {
  display: none;
}
</style>

<style>
/*
 * Pulse visual grammar (G4 "pulse" primitive): concentric expanding rings,
 * #00FF9C glow, fade 60%->0, terminating motion only. These classes are
 * applied to elements created programmatically and attached to the mapbox
 * canvas, so they live outside the scoped template.
 */
.weup-pulse {
  position: relative;
  pointer-events: none;
}

.weup-pulse-halo {
  position: absolute;
  inset: 6px;
  border-radius: var(--weup-radius-pill);
  border: 1.5px solid var(--weup-glow-signal-soft);
  /* WEUP-2.5D (D06): signal illumination intensity is state-bearing —
     driven by --weup-pulse-peak (canonical marker data), glow recipe from
     the token substrate. */
  box-shadow:
    inset 0 0 10px var(--weup-glow-signal-soft),
    var(--weup-glow-signal);
  opacity: calc(0.35 + 0.65 * var(--weup-pulse-peak, 0.35));
}

.weup-pulse-wave {
  position: absolute;
  inset: 6px;
  border-radius: var(--weup-radius-pill);
  border: 2px solid var(--weup-glow-signal-color);
  box-shadow: var(--weup-glow-signal);
  opacity: 0;
  animation: weup-pulse-expand 2.4s ease-out 1 forwards;
}

.weup-pulse-wave-echo {
  border-style: dashed;
  animation-delay: 1.2s;
}

@keyframes weup-pulse-expand {
  0% {
    transform: scale(0.55);
    opacity: calc(0.6 * var(--weup-pulse-peak, 0.35));
  }
  100% {
    transform: scale(2.6);
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .weup-pulse-wave {
    animation: none;
    display: none;
  }
}
</style>
