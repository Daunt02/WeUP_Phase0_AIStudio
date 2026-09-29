'use client';

import React, { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X, User, Settings, MapPin, Zap, Shield, Activity, ChevronRight, Wallet, Key, QrCode } from 'lucide-react';
import ReceiptDrawer from './ReceiptDrawer';
import { NightlifeItem } from '@/types';
import { MOCK_EVENTS } from '@/constants/mockData';

interface ProfilePanelProps {
  isVisible: boolean;
  onClose: () => void;
}

interface PurchasedKey {
  unlockedAt: string;
  tier: string;
  doorCode: string;
}

export default function ProfilePanel({ isVisible, onClose }: ProfilePanelProps) {
  const stats = [
    { label: 'Signals_Ingested', value: '124', icon: Zap },
    { label: 'Districts_Explored', value: '12', icon: MapPin },
    { label: 'Radar_Level', value: '42', icon: Activity },
  ];

  const [account, setAccount] = useState<string | null>(null);
  const [unlockedKeys, setUnlockedKeys] = useState<Record<string, PurchasedKey>>({});
  const [selectedEventForKey, setSelectedEventForKey] = useState<NightlifeItem | null>(null);

  useEffect(() => {
    const checkConnection = async () => {
      if (typeof window !== 'undefined' && (window as any).ethereum) {
        try {
          const accounts = await (window as any).ethereum.request({ method: 'eth_accounts' });
          if (accounts.length > 0) {
            setAccount(accounts[0]);
          }
        } catch (err) {
          console.error('Error checking connection:', err);
        }
      }
    };
    checkConnection();
  }, []);

  // Hydrate purchased keys from local device storage
  useEffect(() => {
    if (isVisible && typeof window !== 'undefined') {
      const stored = localStorage.getItem('weup_purchased_keys');
      if (stored) {
        try {
          setUnlockedKeys(JSON.parse(stored));
        } catch (e) {
          console.error(e);
        }
      }
    }
  }, [isVisible]);

  return (
    <AnimatePresence>
      {isVisible && (
        <>
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
            className="fixed inset-0 z-[250] bg-black/40 backdrop-blur-sm pointer-events-auto"
          />
          
          <motion.div
            initial={{ x: '100%' }}
            animate={{ x: 0 }}
            exit={{ x: '100%' }}
            transition={{ type: 'spring', damping: 30, stiffness: 300 }}
            className="fixed inset-y-0 right-0 z-[300] w-full md:w-[480px] bg-[#050505] border-l border-white/10 shadow-[-50px_0_100px_rgba(0,0,0,0.8)] overflow-hidden flex flex-col pointer-events-auto"
          >
            {/* Header */}
            <div className="p-12 border-b border-white/5 flex justify-between items-center bg-black/40 backdrop-blur-xl">
              <div className="space-y-1">
                <h2 className="text-4xl font-black tracking-tighter uppercase italic text-white">OPERATOR</h2>
                <p className="text-white/30 font-mono text-[10px] uppercase tracking-[0.4em]">Operator Session Console // WEUP_v0</p>
              </div>
              <button 
                onClick={onClose} 
                aria-label="Close operator panel"
                className="w-12 h-12 flex items-center justify-center rounded-full bg-white/5 hover:bg-white/10 transition-all hover:scale-110 active:scale-90 cursor-pointer"
              >
                <X className="w-6 h-6 text-white/50" />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-12 space-y-16 no-scrollbar">
              {/* User Info (Purely State & Token based. Zero edit fields or bio picture upload to ensure state-over-identity) */}
              <div className="flex items-center gap-8">
                <div className="relative">
                  <div className="w-24 h-24 rounded-3xl bg-brand-primary/10 border border-brand-primary/20 flex items-center justify-center relative overflow-hidden group">
                    <User className="w-10 h-10 text-brand-primary group-hover:scale-110 transition-transform duration-700" />
                  </div>
                  <div className="absolute -bottom-1 -right-1 w-8 h-8 bg-brand-primary rounded-full border-[6px] border-[#050505] flex items-center justify-center">
                    <Shield className="w-3 h-3 text-black" />
                  </div>
                </div>
                <div className="space-y-2">
                  <h3 className="text-2xl font-black uppercase italic tracking-tighter text-white">NEON_OPERATOR_4901</h3>
                  <div className="flex flex-col gap-2">
                    <div className="flex items-center gap-2 px-3 py-1 bg-white/5 border border-white/10 rounded-full w-fit">
                      <div className="w-1.5 h-1.5 rounded-full bg-brand-primary animate-pulse" />
                      <span className="text-[9px] font-mono font-bold text-white/40 uppercase tracking-widest">Status: Active_Duty</span>
                    </div>
                    {account && (
                      <div className="flex items-center gap-2 px-3 py-1 bg-brand-primary/10 border border-brand-primary/20 rounded-full w-fit">
                        <Wallet className="w-3 h-3 text-brand-primary" />
                        <span className="text-[9px] font-mono font-bold text-brand-primary uppercase tracking-widest">
                          {account.slice(0, 6)}...{account.slice(-4)}
                        </span>
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* Cryptographic Sector Keys Registry (Unlocked Passes) */}
              <div className="space-y-8">
                <div className="flex items-center gap-3 text-white/20 font-mono text-[10px] uppercase tracking-[0.4em]">
                  <Key className="w-4 h-4 text-brand-primary" />
                  <span>Sector_Pass_Keys</span>
                </div>

                <div className="space-y-4">
                  {Object.keys(unlockedKeys).length === 0 ? (
                    <div id="no-keys-fallback" className="p-8 border border-dashed border-white/10 rounded-[2rem] text-center space-y-2 bg-white/[0.01]">
                      <Key className="w-8 h-8 text-white/10 mx-auto" />
                      <p className="text-[10px] font-mono text-white/40 uppercase tracking-widest">NO DECENTRALIZED PASS KEYS FOUND</p>
                      <p className="text-[8px] font-mono text-white/20 uppercase tracking-widest max-w-[200px] mx-auto leading-normal">
                        Select a signal on the map and acquire a secure gate code to unfasten locking.
                      </p>
                    </div>
                  ) : (
                    Object.entries(unlockedKeys).map(([eventId, data]) => {
                      // Lookup matching event in mock or discovered list
                      const event = MOCK_EVENTS.find(e => e.id === eventId);
                      if (!event) return null;

                      return (
                        <div
                          key={eventId}
                          className="p-6 bg-white/[0.02] border border-white/10 rounded-[2rem] flex flex-col gap-4 hover:bg-white/[0.04] transition-all duration-300"
                        >
                          <div className="flex justify-between items-start">
                            <div>
                              <h4 className="text-sm font-black text-white uppercase italic leading-tight">{event.title}</h4>
                              <p className="text-[8.5px] font-mono text-white/40 uppercase tracking-widest mt-0.5">{event.venue_name} {"//"} {event.neighborhood || 'DOWNTOWN'}</p>
                            </div>
                            <span className="text-[8px] font-mono text-brand-primary bg-brand-primary/5 border border-brand-primary/10 px-2.5 py-1 rounded-full uppercase tracking-wider shrink-0 font-bold">
                              ACTIVE
                            </span>
                          </div>

                          <div className="flex justify-between items-center p-4 bg-white/5 rounded-2xl border border-white/5 font-mono">
                            <div>
                              <span className="text-[7.5px] text-white/30 uppercase block font-bold">GATE_CHOKEPOINT_CODE</span>
                              <span className="text-base text-[#00FF9C] font-black tracking-widest mt-0.5 block">{data.doorCode}</span>
                            </div>
                            <button
                              onClick={() => setSelectedEventForKey(event)}
                              className="w-10 h-10 bg-white/10 hover:bg-[#00FF9C] text-white hover:text-black rounded-xl flex items-center justify-center transition-all cursor-pointer"
                              title="Display QR Credentials"
                            >
                              <QrCode className="w-5 h-5" />
                            </button>
                          </div>
                        </div>
                      );
                    })
                  )}
                </div>
              </div>

              {/* Stats Grid */}
              <div className="grid grid-cols-3 gap-4">
                {stats.map((stat) => (
                  <div
                    key={stat.label}
                    className="bg-white/[0.02] border border-white/5 rounded-3xl p-6 space-y-4 hover:bg-white/[0.05] hover:border-white/10 transition-all duration-500"
                  >
                    <stat.icon className="w-5 h-5 text-brand-primary" />
                    <div className="space-y-1">
                      <p className="text-2xl font-black italic text-white leading-none">{stat.value}</p>
                      <p className="text-[8px] font-mono text-white/20 uppercase tracking-widest leading-tight">{stat.label}</p>
                    </div>
                  </div>
                ))}
              </div>

              {/* System Preferences */}
              <div className="space-y-8">
                <div className="flex items-center gap-3 text-white/20 font-mono text-[10px] uppercase tracking-[0.4em]">
                  <Settings className="w-4 h-4" />
                  <span>System_Preferences</span>
                </div>
                
                <div className="space-y-4">
                  {[
                    { label: 'High_Contrast_Radar', active: true },
                    { label: 'Signal_Notifications', active: true },
                    { label: 'Biometric_Auth', active: false },
                  ].map((pref) => (
                    <div key={pref.label} className="group p-6 bg-white/[0.02] rounded-[2rem] border border-white/5 flex justify-between items-center hover:bg-white/[0.05] hover:border-white/10 transition-all duration-500">
                      <span className="text-sm font-black uppercase italic text-white/80 group-hover:text-white transition-colors">{pref.label}</span>
                      <div className={`w-12 h-6 rounded-full relative transition-colors duration-500 ${pref.active ? 'bg-brand-primary' : 'bg-white/10'}`}>
                        <div className={`absolute top-1 w-4 h-4 bg-black rounded-full transition-all duration-500 ${pref.active ? 'right-1' : 'left-1'}`} />
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Favorite Districts */}
              <div className="space-y-8">
                <div className="flex items-center gap-3 text-white/20 font-mono text-[10px] uppercase tracking-[0.4em]">
                  <MapPin className="w-4 h-4" />
                  <span>Sector_Affinity</span>
                </div>
                
                <div className="flex flex-wrap gap-3">
                  {['Midtown', 'Downtown', 'Montrose', 'River Oaks', 'Washington Ave'].map(district => (
                    <div key={district} className="px-6 py-3 bg-brand-primary/5 border border-brand-primary/20 rounded-full text-[10px] font-mono font-bold text-brand-primary uppercase tracking-widest hover:bg-brand-primary hover:text-black transition-all cursor-pointer">
                      {district}
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* Sign Out */}
            <div className="p-12 bg-black/60 border-t border-white/5">
              <button 
                onClick={onClose}
                className="w-full h-20 bg-white/5 hover:bg-white/10 border border-white/10 rounded-full text-[11px] font-mono font-black uppercase tracking-[0.4em] text-white/40 hover:text-white transition-all flex items-center justify-center gap-4 group cursor-pointer"
              >
                TERMINATE_SESSION
                <ChevronRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
              </button>
            </div>
          </motion.div>

          {/* Quick Credential viewer ReceiptDrawer within Operator view if a code is clicked */}
          <ReceiptDrawer
            isOpen={selectedEventForKey !== null}
            onClose={() => setSelectedEventForKey(null)}
            event={selectedEventForKey}
            tier={selectedEventForKey ? unlockedKeys[selectedEventForKey.id]?.tier : undefined}
            doorCode={selectedEventForKey ? unlockedKeys[selectedEventForKey.id]?.doorCode : undefined}
          />
        </>
      )}
    </AnimatePresence>
  );
}
