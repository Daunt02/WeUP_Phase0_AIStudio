'use client';

import React from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X, User, Settings, MapPin, Heart, Zap, Shield, Activity, ChevronRight, Wallet } from 'lucide-react';

interface ProfilePanelProps {
  isVisible: boolean;
  onClose: () => void;
}

export default function ProfilePanel({ isVisible, onClose }: ProfilePanelProps) {
  const stats = [
    { label: 'Signals_Ingested', value: '124', icon: Zap },
    { label: 'Districts_Explored', value: '12', icon: MapPin },
    { label: 'Radar_Level', value: '42', icon: Activity },
  ];

  const [account, setAccount] = React.useState<string | null>(null);

  React.useEffect(() => {
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
            className="fixed inset-y-0 right-0 z-[300] w-full md:w-[480px] bg-[#050505] border-l border-white/10 shadow-[-50px_0_100px_rgba(0,0,0,0.8)] overflow-hidden flex flex-col"
          >
          {/* Header */}
          <div className="p-12 border-b border-white/5 flex justify-between items-center bg-black/40 backdrop-blur-xl">
            <div className="space-y-1">
              <h2 className="text-4xl font-black tracking-tighter uppercase italic text-white">Identity</h2>
              <p className="text-white/30 font-mono text-[10px] uppercase tracking-[0.4em]">Operator Profile // WEUP_v0</p>
            </div>
            <button 
              onClick={onClose} 
              className="w-12 h-12 flex items-center justify-center rounded-full bg-white/5 hover:bg-white/10 transition-all hover:scale-110 active:scale-90"
            >
              <X className="w-6 h-6 text-white/60" />
            </button>
          </div>

          <div className="flex-1 overflow-y-auto p-12 space-y-16 no-scrollbar">
            {/* User Info */}
            <div className="flex items-center gap-8">
              <div className="relative">
                <div className="w-32 h-32 rounded-full bg-brand-primary/10 border border-brand-primary/20 flex items-center justify-center relative overflow-hidden group">
                  <User className="w-12 h-12 text-brand-primary group-hover:scale-110 transition-transform duration-700" />
                  <div className="absolute inset-0 bg-gradient-to-t from-brand-primary/20 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-700" />
                </div>
                <div className="absolute -bottom-2 -right-2 w-10 h-10 bg-brand-primary rounded-full border-8 border-[#050505] flex items-center justify-center">
                  <Shield className="w-4 h-4 text-black" />
                </div>
              </div>
              <div className="space-y-2">
                <h3 className="text-3xl font-black uppercase italic tracking-tighter text-white">NEON_OPERATOR_01</h3>
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

            {/* Stats Grid */}
            <div className="grid grid-cols-3 gap-4">
              {stats.map((stat, i) => (
                <motion.div
                  key={stat.label}
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: i * 0.1 }}
                  className="bg-white/[0.02] border border-white/5 rounded-3xl p-6 space-y-4 hover:bg-white/[0.05] hover:border-white/10 transition-all duration-500"
                >
                  <stat.icon className="w-5 h-5 text-brand-primary" />
                  <div className="space-y-1">
                    <p className="text-2xl font-black italic text-white leading-none">{stat.value}</p>
                    <p className="text-[8px] font-mono text-white/20 uppercase tracking-widest leading-tight">{stat.label}</p>
                  </div>
                </motion.div>
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

            {/* Recent Activity */}
            <div className="space-y-8">
              <div className="flex items-center gap-3 text-white/20 font-mono text-[10px] uppercase tracking-[0.4em]">
                <Activity className="w-4 h-4" />
                <span>Recent_Telemetry</span>
              </div>
              
              <div className="space-y-4">
                {[
                  { action: 'Signal_Ingested', time: '2h ago', detail: 'HOUSTON_NIGHTLIFE @ MIDTOWN' },
                  { action: 'District_Unlocked', time: '5h ago', detail: 'MONTROSE_SECTOR' },
                ].map((act, i) => (
                  <div key={i} className="flex items-center gap-6 p-6 bg-white/[0.02] rounded-[2rem] border border-white/5 group hover:bg-white/[0.05] transition-all duration-500">
                    <div className="w-12 h-12 rounded-2xl bg-white/5 flex items-center justify-center group-hover:bg-brand-primary/10 transition-colors">
                      <Zap className="w-5 h-5 text-white/20 group-hover:text-brand-primary transition-colors" />
                    </div>
                    <div className="flex-1 space-y-1">
                      <div className="flex items-center justify-between">
                        <div className="text-sm font-black uppercase italic text-white">{act.action}</div>
                        <div className="text-[9px] font-mono text-white/20 uppercase tracking-widest">{act.time}</div>
                      </div>
                      <div className="text-[10px] font-mono text-white/40 uppercase tracking-widest">{act.detail}</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>

          {/* Sign Out */}
          <div className="p-12 bg-black/60 border-t border-white/5">
            <button className="w-full h-20 bg-white/5 hover:bg-white/10 border border-white/10 rounded-full text-[11px] font-mono font-black uppercase tracking-[0.4em] text-white/40 hover:text-white transition-all flex items-center justify-center gap-4 group">
              TERMINATE_SESSION
              <ChevronRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
            </button>
          </div>
        </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
