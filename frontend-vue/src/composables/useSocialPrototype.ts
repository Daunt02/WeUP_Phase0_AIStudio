/**
 * WEUP-SYNTH:
 * source=components/SocialSignalPanel.tsx (MOCK_TIERS, unlockedTiers)
 * destination=frontend-vue/src/composables/useSocialPrototype.ts
 * mission=WEUP-PHASE0-VUE-FINAL-SYNTHESIS-001
 * notes=Prototype-only social state, explicitly NON-persistent and
 *   NON-networked. The AI Studio source's social graph is prototype-only:
 *   tiers, unlock buttons, trust meter, and "interested" flags have no
 *   backend counterpart (BLOCKED_WITH_REASON: no social-graph API observed).
 *   This store exists solely to preserve the source UI contract in a visibly
 *   labeled prototype surface. It MUST NEVER:
 *   - touch localStorage / sessionStorage / IndexedDB,
 *   - issue network requests,
 *   - merge with the canonical saved state (useSavedEventState),
 *   - or be rendered without the "Prototype — not real" label.
 *   State resets on reload by design.
 */

import { computed, reactive, readonly, type DeepReadonly } from "vue";

/**
 * Social invite tier UI contract, translated 1:1 from the source
 * SocialInviteTier (types/index.ts) observable behavior. unlock_status is
 * derived per-session from unlockedTierLevels; it is not persisted.
 */
export interface SocialInviteTierUi {
  readonly id: string;
  readonly tierLevel: 1 | 2 | 3;
  readonly title: string;
  readonly visibility: "visible" | "hidden";
  readonly trustRequirement: number;
  readonly proximityRadiusMeters: number;
  readonly inviteType: string;
}

/**
 * Source MOCK_TIERS (components/SocialSignalPanel.tsx), preserved verbatim
 * as prototype data. These are demo tiers, never presented as real invites.
 */
export const SOCIAL_PROTOTYPE_TIERS: readonly SocialInviteTierUi[] = [
  {
    id: "tier-1",
    tierLevel: 1,
    title: "Nearby Pre-Event Gathering",
    visibility: "visible",
    trustRequirement: 0,
    proximityRadiusMeters: 500,
    inviteType: "Public-Adjacent",
  },
  {
    id: "tier-2",
    tierLevel: 2,
    title: "Venue-Adjacent Circle",
    visibility: "visible",
    trustRequirement: 50,
    proximityRadiusMeters: 200,
    inviteType: "Semi-Private",
  },
  {
    id: "tier-3",
    tierLevel: 3,
    title: "Hidden Creative Meetup",
    visibility: "hidden",
    trustRequirement: 100,
    proximityRadiusMeters: 100,
    inviteType: "Private",
  },
] as const;

interface SocialPrototypeSession {
  unlockedTierLevels: number[];
  interestedEventIds: string[];
}

const _session = reactive<SocialPrototypeSession>({
  unlockedTierLevels: [],
  interestedEventIds: [],
});

/**
 * Prototype-only social store. Session-scoped: every value lives in module
 * reactive state and is lost on reload. No storage writes, no network calls.
 */
export function useSocialPrototype() {
  const tiers = computed(() => SOCIAL_PROTOTYPE_TIERS);

  const unlockedTierLevels: DeepReadonly<typeof _session.unlockedTierLevels> =
    readonly(_session.unlockedTierLevels);

  function isTierUnlocked(tierLevel: number): boolean {
    return _session.unlockedTierLevels.includes(tierLevel);
  }

  function isTierVisible(tier: SocialInviteTierUi): boolean {
    // Source behavior: hidden tiers render as an "EyeOff" placeholder unless
    // unlocked this session.
    return tier.visibility === "visible" || isTierUnlocked(tier.tierLevel);
  }

  /**
   * Demo-only tier toggle for the prototype UI contract. Does not persist
   * and does not call any backend — it exists so the tier card affordance
   * renders its observable states, labeled as prototype.
   */
  function toggleTierUnlock(tierLevel: number): void {
    const index = _session.unlockedTierLevels.indexOf(tierLevel);
    if (index >= 0) {
      _session.unlockedTierLevels.splice(index, 1);
    } else {
      _session.unlockedTierLevels.push(tierLevel);
    }
  }

  /**
   * Prototype "Keep Signal Active" flag for an event. Session-only, never
   * merged into the canonical saved state.
   */
  function isEventMarkedInterested(eventId: string): boolean {
    return _session.interestedEventIds.includes(eventId);
  }

  function setEventInterested(eventId: string, interested: boolean): void {
    const index = _session.interestedEventIds.indexOf(eventId);
    if (interested && index < 0) {
      _session.interestedEventIds.push(eventId);
    } else if (!interested && index >= 0) {
      _session.interestedEventIds.splice(index, 1);
    }
  }

  return {
    tiers,
    unlockedTierLevels,
    isTierUnlocked,
    isTierVisible,
    toggleTierUnlock,
    isEventMarkedInterested,
    setEventInterested,
  };
}

/**
 * Test-only session reset. In production, the store resets on reload by
 * construction (no persistence layer exists).
 */
export function resetSocialPrototypeForTests(): void {
  _session.unlockedTierLevels = [];
  _session.interestedEventIds = [];
}
