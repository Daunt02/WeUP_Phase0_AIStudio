'use client';

import React, { useMemo, useEffect, useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { NightlifeItem, ModalState } from '@/types';
import { X, MapPin, Clock, Bookmark, Navigation, Share2, ArrowRight, Sparkles, ExternalLink, Search, Loader2, Key, ShieldCheck } from 'lucide-react';
import MediaFlyerCard from './MediaFlyerCard';
import ReceiptDrawer from './ReceiptDrawer';

interface EventSignalModalProps {
  event: NightlifeItem | null;
  state: ModalState;
  onClose: () => void;
  onSave?: (id: string) => void;
  isSaved?: boolean;
  anchorPoint?: { x: number, y: number } | null;
}

const CATEGORY_CONFIG: Record<string, { color: string, glow: string, signalType: string }> = {
  nightlife: { color: 'text-brand-primary', glow: 'shadow-brand-primary/20', signalType: 'bloom' },
  tech: { color: 'text-cyan-400', glow: 'shadow-cyan-400/20', signalType: 'angular' },
  culture: { color: 'text-purple-400', glow: 'shadow-purple-400/20', signalType: 'radiant' },
  wellness: { color: 'text-emerald-400', glow: 'shadow-emerald-400/20', signalType: 'waveform' },
  default: { color: 'text-white', glow: 'shadow-white/20', signalType: 'bloom' }
};

// Signal Background Elements based on category
const SignalBackground = ({ type, color }: { type: string, color: string }) => {
  const glowColor = color.replace('text-', 'bg-');
  
  switch (type) {
    case 'angular':
      return (
        <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-20">
          <div className={`absolute top-0 left-0 w-full h-full border-[0.5px] ${color.replace('text-', 'border-')}/10 rotate-45 translate-x-1/2 -translate-y-1/2`} />
          <div className={`absolute bottom-0 right-0 w-full h-full border-[0.5px] ${color.replace('text-', 'border-')}/10 -rotate-45 -translate-x-1/2 translate-y-1/2`} />
          <div className={`absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-64 h-64 ${glowColor}/5 blur-3xl rounded-full`} />
        </div>
      );
    case 'radiant':
      return (
        <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-20">
          {[...Array(12)].map((_, i) => (
            <div 
              key={i} 
              className={`absolute top-1/2 left-1/2 w-[200%] h-[1px] bg-gradient-to-r from-transparent via-${color.replace('text-', '')}/20 to-transparent`}
              style={{ transform: `translate(-50%, -50%) rotate(${i * 15}deg)` }}
            />
          ))}
          <div className={`absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-80 h-80 ${glowColor}/10 blur-[100px] rounded-full`} />
        </div>
      );
    case 'waveform':
      return (
        <div className="absolute inset-0 overflow-hidden pointer-events-none opacity-10">
          <svg viewBox="0 0 100 20" className={`absolute bottom-0 left-0 w-full h-32 ${color} fill-none stroke-current stroke-[0.5]`}>
            <motion.path 
              d="M0 10 Q 25 0, 50 10 T 100 10" 
              animate={{ d: ["M0 10 Q 25 0, 50 10 T 100 10", "M0 10 Q 25 20, 50 10 T 100 10", "M0 10 Q 25 0, 50 10 T 100 10"] }}
              transition={{ duration: 4, repeat: Infinity, ease: "easeInOut" }}
            />
            <motion.path 
              d="M0 15 Q 25 5, 50 15 T 100 15" 
              animate={{ d: ["M0 15 Q 25 5, 50 15 T 100 15", "M0 15 Q 25 25, 50 15 T 100 15", "M0 15 Q 25 5, 50 15 T 100 15"] }}
              transition={{ duration: 6, repeat: Infinity, ease: "easeInOut", delay: 1 }}
            />
          </svg>
          <div className={`absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-full h-full ${glowColor}/5 blur-[120px] rounded-full`} />
        </div>
      );
    default: // bloom
      return (
        <div className="absolute inset-0 overflow-hidden pointer-events-none">
          <div className={`absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-full h-full rounded-full bg-gradient-radial from-${color.replace('text-', '')}/10 to-transparent blur-3xl`} />
        </div>
      );
  }
};

export default function EventSignalModal({ 
  event, 
  state, 
  onClose, 
  onSave, 
  isSaved,
  anchorPoint 
}: EventSignalModalProps) {
  const config = useMemo(() => {
    if (!event) return CATEGORY_CONFIG.default;
    const cat = event.category?.toLowerCase() || 'default';
    return CATEGORY_CONFIG[cat] || CATEGORY_CONFIG.default;
  }, [event]);

  const [isClient, setIsClient] = useState(false);
  const [isCopied, setIsCopied] = useState(false);
  const [searchQueries, setSearchQueries] = useState<string[]>([]);
  const [isSearching, setIsSearching] = useState(false);

  // Checkout states
  const [isPurchased, setIsPurchased] = useState(false);
  const [checkoutStep, setCheckoutStep] = useState<'INITIAL' | 'PAY'>('INITIAL');
  const [selectedPass, setSelectedPass] = useState<'regular' | 'supporter'>('regular');
  const [isProcessing, setIsProcessing] = useState(false);
  const [isReceiptOpen, setIsReceiptOpen] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setIsClient(true), 0);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    if (event) {
      const purchasedKeys = JSON.parse(localStorage.getItem('weup_purchased_keys') || '{}');
      setIsPurchased(!!purchasedKeys[event.id]);
    }
  }, [event]);

  const handleNativePay = async () => {
    setIsProcessing(true);
    await new Promise(resolve => setTimeout(resolve, 1000));
    setIsProcessing(false);
    
    if (event) {
      const purchasedKeys = JSON.parse(localStorage.getItem('weup_purchased_keys') || '{}');
      purchasedKeys[event.id] = {
        unlockedAt: new Date().toISOString(),
        tier: selectedPass === 'regular' ? 'WEUP_TIER_A // REGULAR_ACCESS' : 'CRITICAL_SUPPORTER // FULL_CAP',
        doorCode: `WEUP-#${Math.floor(1000 + Math.random() * 9000)}X-${Math.floor(10 + Math.random() * 89)}`
      };
      localStorage.setItem('weup_purchased_keys', JSON.stringify(purchasedKeys));
      setIsPurchased(true);
      setIsReceiptOpen(true);
    }
  };

  // Reset search queries when event changes
  useEffect(() => {
    setSearchQueries([]);
  }, [event?.id]);

  const handleFetchSearchTerms = useCallback(async () => {
    if (!event || isSearching) return;
    setIsSearching(true);
    try {
      const response = await fetch('/api/gemini', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          action: 'generateFlyerSearchTerms',
          payload: { event },
        }),
      });
      if (!response.ok) {
        throw new Error('Failed to fetch search terms from proxy');
      }
      const data = await response.json();
      setSearchQueries(data.result || []);
    } catch (err) {
      console.error('Failed to generate search terms:', err);
    } finally {
      setIsSearching(false);
    }
  }, [event, isSearching]);

  const handleShare = () => {
    setIsCopied(true);
    setTimeout(() => setIsCopied(false), 2000);
    if (navigator.share) {
      navigator.share({
        title: event?.title,
        text: event?.description,
        url: window.location.href,
      }).catch(() => {});
    }
  };

  if (!event || !state) return null;

  const isFull = state === 'FULL';

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className={`fixed inset-0 z-[500] pointer-events-none flex items-center justify-center ${isFull ? 'bg-black/40 backdrop-blur-sm' : ''}`}
      >
        {/* Background Overlay for Full */}
        {isFull && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="absolute inset-0 pointer-events-none"
            onClick={onClose}
          />
        )}

        <motion.div
          layoutId={`event-signal-${event.id}`}
          initial={anchorPoint ? { 
            x: anchorPoint.x - (typeof window !== 'undefined' ? window.innerWidth / 2 : 400), 
            y: anchorPoint.y - (typeof window !== 'undefined' ? window.innerHeight / 2 : 350),
            scale: 0.5,
            opacity: 0
          } : { scale: 0.8, opacity: 0 }}
          animate={{ x: 0, y: 0, scale: 1, opacity: 1, width: '100%', maxWidth: '900px', height: 'auto', maxHeight: '90vh' }}
          transition={{ 
            type: 'spring', 
            damping: 25, 
            stiffness: 200,
            mass: 1
          }}
          className={`
            relative pointer-events-auto overflow-hidden
            bg-black/90 backdrop-blur-3xl border border-white/10
            shadow-[0_20px_60px_rgba(0,0,0,0.8)]
            rounded-[3rem] flex flex-col md:flex-row
          `}
        >
          {/* Luminous Edge */}
          <div 
            className="absolute inset-0 pointer-events-none rounded-[inherit] border border-white/5"
            style={{ 
              boxShadow: `inset 0 0 20px ${config.color}15`,
              borderColor: `${config.color}30`
            }} 
          />
          
          <SignalBackground type={config.signalType} color={config.color} />

          {/* FULL DETAIL STATE */}
          {isFull && (
            <>
              {/* Media Section */}
              <div id="media-section-contain" className="w-full md:w-1/2 p-4 md:p-6 flex flex-col justify-center shrink-0">
                <MediaFlyerCard 
                  src={event.image_url} 
                  alt={event.title} 
                  status={event.status} 
                  policyStatus={event.status === 'NEEDS_REVIEW' ? 'PENDING' : 'APPROVED'}
                />
              </div>

              {/* Content Section */}
              <div className="flex-1 p-8 md:p-12 flex flex-col relative">
                <button 
                  onClick={onClose}
                  className="absolute top-8 right-8 w-12 h-12 bg-white/5 rounded-full border border-white/10 flex items-center justify-center hover:bg-white/10 transition-all group"
                >
                  <X className="w-6 h-6 text-white/40 group-hover:text-white" />
                </button>

                <div className="flex-1 flex flex-col justify-center space-y-8">
                  <div className="space-y-4">
                    <h2 className="text-4xl md:text-6xl font-black tracking-tighter uppercase italic leading-[0.85] text-white">
                      {event.title}
                    </h2>
                    
                    <div className="grid grid-cols-2 gap-6">
                      <div className="space-y-1">
                        <p className="text-[8px] font-mono text-white/20 uppercase tracking-widest">LOCATION</p>
                        <div className="flex items-center gap-2">
                          <MapPin size={12} className={config.color} />
                          <p className="text-xs font-black uppercase text-white/80">{event.venue_name}</p>
                          {event.spatial_label && (
                            <span className="text-[7.5px] font-mono font-black border border-brand-primary/40 text-brand-primary px-1.5 py-0.5 rounded italic">
                              {event.spatial_label}
                            </span>
                          )}
                        </div>
                        <p className="text-[10px] text-white/40 ml-5 leading-tight">{event.address || event.neighborhood || 'DOWNTOWN'}</p>
                      </div>
                      <div className="space-y-1">
                        <p className="text-[8px] font-mono text-white/20 uppercase tracking-widest">TEMPORAL</p>
                        <div className="flex items-center gap-2">
                          <Clock size={12} className={config.color} />
                          <p className="text-xs font-black uppercase text-white/80">
                            {isClient && new Date(event.start_time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })}
                          </p>
                        </div>
                        <p className="text-[10px] text-white/40 ml-5">
                          {new Date(event.start_time).toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric' })}
                        </p>
                      </div>
                    </div>
                  </div>

                  <div className="space-y-6">
                    <div className="flex flex-col gap-2">
                      <div className="flex items-center justify-between">
                        <p className="text-[9px] font-mono text-white/20 uppercase tracking-[0.3em] font-bold">SIGNAL_INTENSITY</p>
                        <span className="text-[10px] font-black text-brand-primary italic">EXTREME</span>
                      </div>
                      <div className="flex gap-1.5 h-2">
                        {[...Array(12)].map((_, i) => (
                          <motion.div 
                            key={`intensity-bar-${i}`} 
                            initial={{ scaleY: 0.5, opacity: 0.3 }}
                            animate={{ 
                              scaleY: [0.5, 1, 0.5],
                              opacity: i < (event.energyLevel || 8) ? 1 : 0.1
                            }}
                            transition={{ 
                              duration: 1.5, 
                              repeat: Infinity, 
                              delay: i * 0.1,
                              ease: "easeInOut"
                            }}
                            className={`flex-1 rounded-full ${i < (event.energyLevel || 8) ? config.color.replace('text-', 'bg-') : 'bg-white/10'}`} 
                          />
                        ))}
                      </div>
                    </div>
                    <p className="text-sm md:text-lg text-white/60 leading-relaxed font-medium">
                      {event.description}
                    </p>
                    {event.tags && (
                      <div className="flex flex-wrap gap-2">
                        {event.tags.map((tag, idx) => (
                          <span key={`${tag}-${idx}`} className="px-3 py-1 rounded-full bg-white/5 border border-white/5 text-[8px] font-mono text-white/40 uppercase tracking-widest hover:text-white hover:bg-white/10 transition-colors cursor-default">
                            #{tag}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>

                  {/* Flyer Search Engine */}
                  <div className="p-6 rounded-[2.5rem] bg-brand-primary/[0.03] border border-brand-primary/10 flex flex-col gap-4 relative overflow-hidden group">
                    <div className="absolute top-0 right-0 w-32 h-32 bg-brand-primary/5 blur-[50px] -translate-y-1/2 translate-x-1/2 pointer-events-none" />
                    
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-full bg-brand-primary/20 flex items-center justify-center">
                          <Search className="w-4 h-4 text-brand-primary" />
                        </div>
                        <div>
                          <p className="text-[10px] font-black uppercase italic tracking-tighter text-white">Flyer Signal Support</p>
                          <p className="text-[8px] font-mono text-white/40 uppercase tracking-widest">Verify authenticity via IG/Google</p>
                        </div>
                      </div>
                      
                      {!searchQueries.length ? (
                        <button 
                          onClick={handleFetchSearchTerms}
                          disabled={isSearching}
                          className="px-4 py-2 rounded-full bg-white/5 border border-white/10 text-[8px] font-mono text-white/60 uppercase tracking-widest hover:bg-brand-primary hover:text-black transition-all disabled:opacity-50"
                        >
                          {isSearching ? <Loader2 className="w-3 h-3 animate-spin" /> : 'GENERATE_QUERIES'}
                        </button>
                      ) : (
                        <div className="flex items-center gap-2">
                           <span className="text-[8px] font-mono text-brand-primary/60 uppercase tracking-widest">AI_GENERATED</span>
                           <Sparkles className="w-3 h-3 text-brand-primary" />
                        </div>
                      )}
                    </div>

                    {searchQueries.length > 0 && (
                      <div className="flex flex-col gap-2 animate-in fade-in slide-in-from-bottom-2 duration-500">
                        {searchQueries.map((query, i) => (
                          <a
                            key={`query-${i}`}
                            href={`https://www.google.com/search?q=${encodeURIComponent(query)}&tbm=isch`}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex items-center justify-between p-3 rounded-xl bg-white/5 border border-white/5 hover:border-brand-primary/40 hover:bg-white/10 transition-all group/query"
                          >
                            <span className="text-[10px] text-white/70 font-medium truncate pr-4">{query}</span>
                            <ArrowRight className="w-3 h-3 text-white/20 group-hover/query:text-brand-primary group-hover/query:translate-x-1 transition-all" />
                          </a>
                        ))}
                      </div>
                    )}
                  </div>

                  {/* ACCESS GATEWAY (Zero-Identity Frictionless Checkout - Per Mitigation Prompt 2) */}
                  <div id="access-gateway-container" className="p-6 rounded-[2.5rem] bg-white/[0.01] border border-white/10 flex flex-col gap-4 relative overflow-hidden pointer-events-auto">
                    <div className="absolute top-0 inset-x-0 h-[1.5px] bg-[#00FF9C] opacity-30 group-hover:opacity-100" />
                    
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-full bg-[#00FF9C]/10 border border-[#00FF9C]/20 flex items-center justify-center text-[#00FF9C]">
                          <Key className="w-4 h-4" />
                        </div>
                        <div>
                          <p className="text-[10px] font-black uppercase italic tracking-tighter text-white">ACCESS CHOKEPOINT PASS</p>
                          <p className="text-[8px] font-mono text-white/40 uppercase tracking-widest">STATE-DRIVEN DECENTRALIZED ENTRY KEY</p>
                        </div>
                      </div>
                      
                      <div className="px-3 py-1 rounded-full bg-white/5 border border-white/10 text-[8px] font-mono text-[#00FF9C] tracking-widest">
                        {event.price_tier || 'FREE'}
                      </div>
                    </div>

                    {isPurchased ? (
                      <div className="space-y-4 animate-in fade-in slide-in-from-bottom-2 duration-300">
                        <div className="flex items-center justify-between p-4 rounded-2xl bg-[#00FF9C]/5 border border-[#00FF9C]/20">
                          <div className="flex items-center gap-3">
                            <ShieldCheck className="w-5 h-5 text-[#00FF9C]" />
                            <div>
                              <p className="text-[9px] font-mono uppercase text-[#00FF9C] tracking-tighter">STATE KEY UNLOCKED</p>
                              <p className="text-[18px] font-mono font-black text-white tracking-widest leading-none mt-1">
                                {(() => {
                                  if (typeof window !== 'undefined') {
                                    const keys = JSON.parse(localStorage.getItem('weup_purchased_keys') || '{}');
                                    return keys[event.id]?.doorCode || 'WEUP-#891X-02';
                                  }
                                  return 'WEUP-#891X-02';
                                })()}
                              </p>
                            </div>
                          </div>
                        </div>
                        <button
                          onClick={() => setIsReceiptOpen(true)}
                          className="w-full h-14 bg-[#00FF9C] hover:bg-white text-black rounded-2xl font-black uppercase tracking-[0.25em] text-[10px] transition-all hover:scale-102 active:scale-95 cursor-pointer flex items-center justify-center gap-2"
                        >
                          <Sparkles className="w-4 h-4" />
                          LAUNCH Receipt Credentials
                        </button>
                      </div>
                    ) : checkoutStep === 'PAY' ? (
                      <div className="space-y-4 animate-in fade-in slide-in-from-bottom-2 duration-300 pointer-events-auto">
                        <div className="space-y-2">
                          <p className="text-[8px] font-mono text-white/40 uppercase tracking-widest">SELECT PASS TYPE</p>
                          <div className="grid grid-cols-2 gap-3">
                            <button
                              onClick={() => setSelectedPass('regular')}
                              className={`p-4 rounded-2xl border text-left flex flex-col gap-1.5 transition-all ${
                                selectedPass === 'regular' 
                                  ? 'bg-white/5 border-[#00FF9C]' 
                                  : 'bg-white/[0.01] border-white/5 hover:border-white/20'
                              }`}
                            >
                              <span className="text-[9px] font-black text-white uppercase italic">REGULAR KEY</span>
                              <span className="text-[10px] font-mono font-black text-[#00FF9C]">{event.price_tier || 'FREE'}</span>
                              <span className="text-[7px] font-mono text-white/30 uppercase">Offline door code download</span>
                            </button>
                            <button
                              onClick={() => setSelectedPass('supporter')}
                              className={`p-4 rounded-2xl border text-left flex flex-col gap-1.5 transition-all ${
                                selectedPass === 'supporter' 
                                  ? 'bg-white/5 border-[#00FF9C]' 
                                  : 'bg-white/[0.01] border-white/5 hover:border-white/20'
                              }`}
                            >
                              <span className="text-[9px] font-black text-white uppercase italic">CRITICAL SUPPORTER</span>
                              <span className="text-[10px] font-mono font-black text-[#00FF9C]">$5.00</span>
                              <span className="text-[7px] font-mono text-white/30 uppercase">Secures signal bandwidth</span>
                            </button>
                          </div>
                        </div>

                        <div className="p-4 bg-white/[0.02] border border-white/5 rounded-2xl flex flex-col gap-3">
                          <div className="flex justify-between items-center text-xs">
                            <span className="text-white/40 font-mono text-[9px] uppercase tracking-wider">SECURE TRANSMISSION</span>
                            <span className="text-[#00FF9C] font-mono text-[9px] uppercase tracking-wider">STRIPE PAYLINK INTERPRETER</span>
                          </div>
                          
                          <button
                            onClick={handleNativePay}
                            disabled={isProcessing}
                            className="w-full h-14 bg-white text-black hover:bg-zinc-200 active:scale-95 rounded-2xl font-black text-[11px] uppercase tracking-[0.2em] transition-all flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
                          >
                            {isProcessing ? (
                              <>
                                <Loader2 className="w-4 h-4 animate-spin" />
                                COMMITTING_PASSCODE_DEPOSIT...
                              </>
                            ) : (
                              <>
                                <span className="font-sans font-black tracking-normal lowercase text-xs"></span> PAY WITH APPLE PAY / NATIVE
                              </>
                            )}
                          </button>
                        </div>
                        
                        <div className="flex justify-center">
                          <button
                            onClick={() => setCheckoutStep('INITIAL')}
                            className="text-[8px] font-mono text-white/40 uppercase tracking-widest hover:text-white transition-colors"
                          >
                            [ CANCEL ]
                          </button>
                        </div>
                      </div>
                    ) : (
                      <button
                        onClick={() => setCheckoutStep('PAY')}
                        className="w-full h-16 bg-[#00FF9C] text-black hover:bg-white transition-all transform hover:scale-102 active:scale-95 rounded-2xl font-black uppercase tracking-[0.25em] text-[11px] flex items-center justify-center gap-2 cursor-pointer shadow-[0_0_30px_rgba(0,255,156,0.25)]"
                      >
                        <Key className="w-4 h-4" />
                        ACQUIRE SYSTEM CLEARANCE KEY
                      </button>
                    )}
                  </div>

                  <div className="flex gap-3 pt-4">
                    <button 
                      onClick={() => onSave?.(event.id)}
                      className={`flex-[2] h-16 rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] flex items-center justify-center gap-3 transition-all border ${
                        isSaved 
                          ? 'bg-brand-primary text-black border-brand-primary shadow-[0_0_20px_rgba(0,255,156,0.3)]' 
                          : 'bg-white/5 text-white border-white/10 hover:bg-white/10'
                      }`}
                    >
                      <Bookmark className={`w-4 h-4 ${isSaved ? 'fill-black' : ''}`} />
                      {isSaved ? 'SIGNAL_SAVED' : 'SAVE_SIGNAL'}
                    </button>
                    
                    <button className="flex-[2] h-16 bg-white text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] flex items-center justify-center gap-3 hover:bg-brand-primary transition-all active:scale-95">
                      <Navigation className="w-4 h-4" />
                      NAVIGATE
                    </button>

                    <button className="flex-1 h-16 bg-white/5 border border-white/10 rounded-2xl flex items-center justify-center text-white/40 hover:text-white transition-all">
                      <ExternalLink size={20} />
                    </button>

                    <button 
                      onClick={handleShare}
                      className="relative flex-1 h-16 bg-white/5 border border-white/10 rounded-2xl flex items-center justify-center text-white/40 hover:text-white transition-all overflow-hidden"
                    >
                      <AnimatePresence mode="wait">
                        {isCopied ? (
                          <motion.span
                            key="copied"
                            initial={{ y: 20, opacity: 0 }}
                            animate={{ y: 0, opacity: 1 }}
                            exit={{ y: -20, opacity: 0 }}
                            className="text-[8px] font-mono font-black text-brand-primary"
                          >
                            LINK_COPIED
                          </motion.span>
                        ) : (
                          <motion.div
                            key="share"
                            initial={{ scale: 0.8, opacity: 0 }}
                            animate={{ scale: 1, opacity: 1 }}
                            exit={{ scale: 0.8, opacity: 0 }}
                          >
                            <Share2 size={20} />
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </button>
                  </div>
                </div>
              </div>
            </>
          )}
        </motion.div>

        {/* Sliding Receipt Credentials Drawer */}
        <ReceiptDrawer
          isOpen={isReceiptOpen}
          onClose={() => setIsReceiptOpen(false)}
          event={event}
          tier={selectedPass === 'regular' ? 'WEUP_TIER_A // DECENTRALIZED' : 'SUPPORTER_TIER // HIGH_BANDWIDTH'}
          doorCode={(() => {
            if (typeof window !== 'undefined') {
              const keys = JSON.parse(localStorage.getItem('weup_purchased_keys') || '{}');
              return keys[event.id]?.doorCode || 'WEUP-#891X-02';
            }
            return 'WEUP-#891X-02';
          })()}
        />
      </motion.div>
    </AnimatePresence>
  );
}
