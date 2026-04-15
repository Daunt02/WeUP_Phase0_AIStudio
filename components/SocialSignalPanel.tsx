"use client";

import React from "react";
import { motion, AnimatePresence } from "motion/react";
import Image from "next/image";
import { SocialInviteTier } from "@/types";
import type { RuntimeEventProjection } from "@/features/world/runtimeTypes";
import {
  X,
  Zap,
  Users,
  Lock,
  EyeOff,
  MapPin,
  Calendar,
  ArrowRight,
  ShieldCheck,
} from "lucide-react";

interface SocialSignalPanelProps {
  isOpen: boolean;
  event: RuntimeEventProjection | null;
  onClose: () => void;
  unlockedTiers: number[];
}

const MOCK_TIERS: SocialInviteTier[] = [
  {
    id: "tier-1",
    event_id: "any",
    tier_level: 1,
    title: "Nearby Pre-Event Gathering",
    visibility: "visible",
    unlock_status: "unlocked",
    trust_requirement: 0,
    proximity_radius_meters: 500,
    invite_type: "Public-Adjacent",
  },
  {
    id: "tier-2",
    event_id: "any",
    tier_level: 2,
    title: "Venue-Adjacent Circle",
    visibility: "visible",
    unlock_status: "locked",
    trust_requirement: 50,
    proximity_radius_meters: 200,
    invite_type: "Semi-Private",
  },
  {
    id: "tier-3",
    event_id: "any",
    tier_level: 3,
    title: "Hidden Creative Meetup",
    visibility: "hidden",
    unlock_status: "locked",
    trust_requirement: 100,
    proximity_radius_meters: 100,
    invite_type: "Private",
  },
];

export default function SocialSignalPanel({
  isOpen,
  event,
  onClose,
  unlockedTiers,
}: SocialSignalPanelProps) {
  if (!event) return null;

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          initial={{ x: "100%", opacity: 0 }}
          animate={{ x: 0, opacity: 1 }}
          exit={{ x: "100%", opacity: 0 }}
          transition={{ type: "spring", damping: 25, stiffness: 200 }}
          className="fixed right-6 top-24 bottom-32 z-[160] w-[calc(100vw-48px)] sm:w-[400px] bg-black/40 backdrop-blur-[40px] border border-white/10 rounded-[3rem] overflow-hidden flex flex-col shadow-[0_50px_100px_rgba(0,0,0,0.5)]"
        >
          {/* Header */}
          <div className="p-8 pb-4 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-brand-primary/20 border border-brand-primary/40 flex items-center justify-center">
                <Zap className="w-5 h-5 text-brand-primary fill-brand-primary" />
              </div>
              <div>
                <h3 className="text-xl font-black uppercase italic text-white leading-none">
                  Interest Saved
                </h3>
                <p className="text-[9px] font-mono text-brand-primary/60 tracking-[0.3em] uppercase font-bold">
                  Social Signal Active
                </p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="w-10 h-10 rounded-full bg-white/5 border border-white/10 flex items-center justify-center hover:bg-white/10 transition-all"
            >
              <X className="w-5 h-5 text-white/40" />
            </button>
          </div>

          {/* Event Summary Card */}
          <div className="px-8 py-4">
            <div className="relative aspect-[16/9] rounded-2xl overflow-hidden border border-white/10 group">
              <Image
                src={event.imageUrl}
                alt={event.title}
                fill
                className="object-cover opacity-60 group-hover:scale-110 transition-transform duration-1000"
                referrerPolicy="no-referrer"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-black via-transparent to-transparent" />
              <div className="absolute bottom-4 left-4 right-4">
                <h4 className="text-lg font-black uppercase italic text-white leading-tight truncate">
                  {event.title}
                </h4>
                <div className="flex items-center gap-3 mt-1">
                  <div className="flex items-center gap-1 text-[8px] font-mono text-white/40 uppercase tracking-widest">
                    <MapPin className="w-2 h-2" />
                    {event.venueName}
                  </div>
                  <div className="flex items-center gap-1 text-[8px] font-mono text-white/40 uppercase tracking-widest">
                    <Calendar className="w-2 h-2" />
                    {new Date(event.startTime).toLocaleDateString([], {
                      month: "short",
                      day: "numeric",
                    })}
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Social Layer Section */}
          <div className="flex-1 px-8 py-4 overflow-y-auto no-scrollbar space-y-6">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2 text-[9px] font-mono text-white/20 uppercase tracking-[0.4em] font-bold">
                <Users className="w-3 h-3" />
                <span>Nearby Social Layer</span>
              </div>
              <div className="px-2 py-1 rounded bg-brand-primary/10 border border-brand-primary/20 text-[7px] font-mono text-brand-primary uppercase tracking-widest">
                {unlockedTiers.length} Tiers Unlocked
              </div>
            </div>

            <div className="space-y-4">
              {MOCK_TIERS.map((tier) => {
                const isUnlocked = unlockedTiers.includes(tier.tier_level);
                const isHidden = tier.visibility === "hidden" && !isUnlocked;

                if (isHidden) {
                  return (
                    <div
                      key={tier.id}
                      className="p-6 rounded-3xl bg-white/[0.02] border border-dashed border-white/5 flex items-center justify-between opacity-40"
                    >
                      <div className="flex items-center gap-4">
                        <div className="w-10 h-10 rounded-full bg-white/5 flex items-center justify-center">
                          <EyeOff className="w-5 h-5 text-white/20" />
                        </div>
                        <div>
                          <p className="text-[10px] font-black uppercase italic text-white/40 tracking-tighter">
                            Tier {tier.tier_level} Invites
                          </p>
                          <p className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                            Hidden Layer
                          </p>
                        </div>
                      </div>
                    </div>
                  );
                }

                return (
                  <div
                    key={tier.id}
                    className={`p-6 rounded-3xl border transition-all duration-500 ${
                      isUnlocked
                        ? "bg-brand-primary/5 border-brand-primary/20 shadow-[0_0_30px_rgba(0,255,156,0.05)]"
                        : "bg-white/5 border-white/10 opacity-60"
                    }`}
                  >
                    <div className="flex items-center justify-between mb-4">
                      <div className="flex items-center gap-3">
                        <div
                          className={`w-10 h-10 rounded-full flex items-center justify-center ${isUnlocked ? "bg-brand-primary/20 text-brand-primary" : "bg-white/10 text-white/40"}`}
                        >
                          {isUnlocked ? (
                            <ShieldCheck className="w-5 h-5" />
                          ) : (
                            <Lock className="w-5 h-5" />
                          )}
                        </div>
                        <div>
                          <p
                            className={`text-[10px] font-black uppercase italic tracking-tighter ${isUnlocked ? "text-brand-primary" : "text-white/40"}`}
                          >
                            Tier {tier.tier_level}: {tier.invite_type}
                          </p>
                          <h5 className="text-sm font-black uppercase italic text-white leading-none mt-1">
                            {tier.title}
                          </h5>
                        </div>
                      </div>
                    </div>

                    {isUnlocked ? (
                      <button className="w-full h-12 bg-white text-black rounded-xl font-black uppercase tracking-[0.2em] text-[9px] flex items-center justify-center gap-2 hover:bg-brand-primary transition-all">
                        Explore Invite
                        <ArrowRight className="w-3 h-3" />
                      </button>
                    ) : (
                      <div className="flex items-center gap-2 text-[8px] font-mono text-white/20 uppercase tracking-widest">
                        <Lock className="w-2 h-2" />
                        Trust Requirement: {tier.trust_requirement}%
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          {/* Footer Actions */}
          <div className="p-8 pt-4 space-y-3">
            <button className="w-full h-14 bg-brand-primary text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] flex items-center justify-center gap-2">
              View Event Details
            </button>
            <button className="w-full h-14 bg-white/5 border border-white/10 text-white/60 rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] hover:bg-white/10 transition-all">
              Keep Signal Active
            </button>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
