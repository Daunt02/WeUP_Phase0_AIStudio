'use client';

import React, { useState, useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { 
  X, Bot, Bone, Sliders, Cpu, Wifi, Compass, Sparkles, Send, RefreshCw, Orbit, Zap
} from 'lucide-react';

interface SpikeAssistantPanelProps {
  isVisible: boolean;
  onClose: () => void;
  mapCenter: { lat: number, lng: number };
  radius: number;
  onRadiusChange: (radius: number) => void;
  activeCategories: string[];
  onToggleCategory: (category: string) => void;
  onPublishSignal: (event: any) => void;
}

export default function SpikeAssistantPanel({
  isVisible,
  onClose,
  mapCenter,
  radius,
  onRadiusChange,
  activeCategories,
  onToggleCategory,
  onPublishSignal
}: SpikeAssistantPanelProps) {
  // Modes: 
  // 'auto' (miniAuto: diagnostic vehicle systems, calibrations)
  // 'concierge' (miniConcierge: chat, POIs, recommendations)
  // 'animal' (miniAnimal: Unified Spike! Combines Auto & Concierge with bulldog avatar!)
  const [activePersona, setActivePersona] = useState<'auto' | 'concierge' | 'animal'>('animal');
  
  // Tab for unified 'animal' mode: 'CONSOLE' or 'DIALOGUE'
  const [animalSubTab, setAnimalSubTab] = useState<'CONSOLE' | 'DIALOGUE'>('DIALOGUE');

  // Chat State
  const [inputText, setInputText] = useState('');
  const [chatLog, setChatLog] = useState<Array<{ sender: 'user' | 'assistant' | 'system'; text: string; timestamp: string }>>([
    { 
      sender: 'assistant', 
      text: "RUFF! Active Duty Operator! Spike online and connected to WeUP Signal Grid. State coordinates calibrated. Ask me about local hot spots or calibrate the solar radar directly!", 
      timestamp: '09:44' 
    }
  ]);
  const [isThinking, setIsThinking] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);
  const [isBlinking, setIsBlinking] = useState(false);

  // Calibration stats
  const [signalIntensity, setSignalIntensity] = useState(82);
  const [pulseRate, setPulseRate] = useState(5.7); // Hz
  const [coreTemp, setCoreTemp] = useState(41.2); // Celsius
  const [calibrationActive, setCalibrationActive] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  // Blink effect loop for Spike
  useEffect(() => {
    const blinkInterval = setInterval(() => {
      setIsBlinking(true);
      setTimeout(() => setIsBlinking(false), 200);
    }, 4000);
    return () => clearInterval(blinkInterval);
  }, []);

  // Sync scroll on chat addition
  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [chatLog, isThinking]);

  // Handle auto recalibration simulation
  const handleCalibrate = () => {
    setCalibrationActive(true);
    setSignalIntensity(100);
    setPulseRate(9.8);
    setCoreTemp(38.4);
    const logMsg = {
      sender: 'system' as const,
      text: `SYSTEM_ALERT: Core radar recalibrated. Frequency locked at 9.8Hz. Range index: ${radius}km.`,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    };
    setChatLog(prev => [...prev, logMsg]);
    setTimeout(() => {
      setCalibrationActive(false);
    }, 1500);
  };

  // Helper trigger action for prompt selections
  const handleSuggestion = (prompt: string) => {
    setInputText(prompt);
  };

  // Chat Submission
  const handleSend = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!inputText.trim() || isThinking) return;

    const userMessage = inputText;
    setInputText('');
    
    // Add user log
    const userLog = {
      sender: 'user' as const,
      text: userMessage,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    };
    setChatLog(prev => [...prev, userLog]);
    setIsThinking(true);
    setIsSpeaking(false);

    try {
      const response = await fetch('/api/gemini', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          action: 'assistantChat',
          payload: {
            message: userMessage,
            persona: activePersona,
            center: mapCenter,
            radius,
            history: chatLog.slice(-5).map(c => ({ role: c.sender === 'user' ? 'user' : 'model', parts: [{ text: c.text }] }))
          }
        })
      });

      const data = await response.json();
      if (data.error) {
        throw new Error(data.error);
      }

      setIsThinking(false);
      setIsSpeaking(true);

      const assistantMsg = {
        sender: 'assistant' as const,
        text: data.result?.text || "Bark! Connection interrupted, re-scannng...",
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      };
      
      setChatLog(prev => [...prev, assistantMsg]);

      // If Gemini suggested a physical event, offer to inject it
      if (data.result?.suggestedEvent) {
        const event = data.result.suggestedEvent;
        const systemLog = {
          sender: 'system' as const,
          text: `SIGNAL_FOUND: Detected dynamic event "${event.title}" at ${event.venue_name}. Injecting coordinates onto current map visual plane.`,
          timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
        };
        setChatLog(prev => [...prev, systemLog]);
        onPublishSignal({
          id: `spike-ai-${Date.now()}`,
          title: event.title,
          description: event.description || "Synthesized event by Spike AI Concierge.",
          venue_name: event.venue_name,
          address: event.address || `${event.venue_name} Metro Area`,
          latitude: mapCenter.lat + (Math.random() - 0.5) * 0.015,
          longitude: mapCenter.lng + (Math.random() - 0.5) * 0.015,
          start_time: new Date(Date.now() + 4 * 3600 * 1000).toISOString(),
          end_time: new Date(Date.now() + 8 * 3600 * 1000).toISOString(),
          category: event.category || 'concert',
          price_tier: '$$',
          source: 'api'
        });
      }

      setTimeout(() => setIsSpeaking(false), 3000);
    } catch (err: any) {
      console.error(err);
      setIsThinking(false);
      setChatLog(prev => [...prev, {
        sender: 'system',
        text: `ROUTING_RETRY: Prompt node failed: ${err.message || "Timeout"}. Offline cache returned fallback scan diagnostics.`,
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      }]);
    }
  };

  const CATEGORIES = ['nightlife', 'lounge', 'concert', 'private', 'restaurant', 'rooftop', 'startup'];

  return (
    <AnimatePresence>
      {isVisible && (
        <>
          {/* Ambient Blurred Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
            className="fixed inset-0 z-[250] bg-black/60 backdrop-blur-md pointer-events-auto"
          />

          {/* Core Panel: Completely Viewport-Locked & No Scroll */}
          <motion.div
            initial={{ y: '100%', opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            exit={{ y: '100%', opacity: 0 }}
            transition={{ type: 'spring', damping: 26, stiffness: 220 }}
            className="fixed inset-x-0 bottom-0 top-[8%] z-[300] bg-[#050505]/95 border-t border-white/10 rounded-t-[3rem] shadow-[0_-20px_100px_rgba(0,0,0,0.9)] overflow-hidden flex flex-col pointer-events-auto selection:bg-[#00FF9C]/20 selection:text-[#00FF9C]"
          >
            {/* Upper Glow Border Line */}
            <div className="absolute top-0 inset-x-0 h-[2px] bg-gradient-to-r from-transparent via-[#00FF9C] to-transparent opacity-45" />

            {/* HEADER BANNER */}
            <div className="px-8 py-6 border-b border-white/5 flex flex-col md:flex-row md:items-center justify-between gap-4 bg-black/40 backdrop-blur-xl shrink-0">
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 bg-[#00FF9C]/10 border border-[#00FF9C]/20 rounded-2xl flex items-center justify-center shadow-[0_0_15px_rgba(0,255,156,0.1)]">
                  <Bot className="w-6 h-6 text-[#00FF9C]" />
                </div>
                <div>
                  <h2 className="text-xl font-black italic tracking-tighter uppercase text-white flex items-center gap-2">
                    WEUP CO-PILOT ASSISTANT
                    <span className="text-[8px] tracking-normal font-mono px-2 py-0.5 rounded bg-[#00FF9C]/10 text-[#00FF9C] border border-[#00FF9C]/20 uppercase">
                      Spike_V0.9
                    </span>
                  </h2>
                  <p className="text-[8px] font-mono text-white/30 tracking-[0.2em] uppercase">
                    Active Radar Coordinate Scanner & Concierge Controller
                  </p>
                </div>
              </div>

              {/* PERSONA MODE RADIAL SELECTORS */}
              <div className="flex bg-white/5 border border-white/10 rounded-2xl p-1 gap-1">
                <button
                  onClick={() => setActivePersona('auto')}
                  className={`px-4 py-2 rounded-xl text-[9px] font-black uppercase tracking-widest transition-all duration-300 flex items-center gap-1.5 ${
                    activePersona === 'auto'
                      ? 'bg-[#00FF9C]/10 text-[#00FF9C] border border-[#00FF9C]/20'
                      : 'text-white/40 hover:text-white/80 border border-transparent'
                  }`}
                >
                  <Cpu className="w-3.5 h-3.5" />
                  miniAuto
                </button>
                <button
                  onClick={() => setActivePersona('concierge')}
                  className={`px-4 py-2 rounded-xl text-[9px] font-black uppercase tracking-widest transition-all duration-300 flex items-center gap-1.5 ${
                    activePersona === 'concierge'
                      ? 'bg-[#00FF9C]/10 text-[#00FF9C] border border-[#00FF9C]/20'
                      : 'text-white/40 hover:text-white/80 border border-transparent'
                  }`}
                >
                  <Orbit className="w-3.5 h-3.5" />
                  miniConcierge
                </button>
                <button
                  onClick={() => setActivePersona('animal')}
                  className={`px-4 py-2 rounded-xl text-[9px] font-black uppercase tracking-widest transition-all duration-300 flex items-center gap-1.5 ${
                    activePersona === 'animal'
                      ? 'bg-[#00FF9C] text-black font-black'
                      : 'text-white/40 hover:text-white/80 border border-transparent'
                  }`}
                >
                  <Bone className="w-3.5 h-3.5" />
                  miniAnimal
                </button>
              </div>

              {/* Close Button */}
              <button
                onClick={onClose}
                className="absolute right-6 top-6 w-10 h-10 rounded-full bg-white/5 hover:bg-white/10 flex items-center justify-center transition-colors"
              >
                <X className="w-5 h-5 text-white/40 hover:text-white" />
              </button>
            </div>

            {/* THREE PANELS LAYOUT STRUCTURE - ZERO SCROLL (Viewport Grid Split) */}
            <div className="flex-1 min-h-0 w-full grid grid-cols-1 md:grid-cols-12 gap-0 overflow-hidden">
              
              {/* LEFT 5 COLS: CONTROLS & DIAGNOSTIC DECK (miniAuto / Unified miniAnimal) */}
              {(activePersona === 'auto' || activePersona === 'animal') ? (
                <div className={`p-8 bg-black/20 border-r border-white/5 flex flex-col justify-between ${
                  activePersona === 'animal' && animalSubTab === 'DIALOGUE' ? 'hidden md:flex md:col-span-5' : 'col-span-12 md:col-span-12 lg:col-span-5'
                }`}>
                  <div className="space-y-6 overflow-y-auto pr-2 no-scrollbar">
                    
                    <div className="space-y-2">
                      <div className="flex justify-between items-center text-[9px] font-mono uppercase tracking-[0.2em] text-white/30">
                        <span>Calibration Command</span>
                        <span className="text-[#00FF9C]">{signalIntensity}% LOCK</span>
                      </div>
                      <div className="p-4 bg-white/[0.02] border border-white/5 rounded-2xl flex items-center justify-between">
                        <div className="flex items-center gap-3">
                          <Sliders className="w-5 h-5 text-[#00FF9C]" />
                          <div>
                            <p className="text-xs font-bold text-white uppercase italic">Grid Sync Optimization</p>
                            <p className="text-[8px] font-mono text-white/30">CURRENT: {pulseRate}Hz // TEMP: {coreTemp}°C</p>
                          </div>
                        </div>
                        <button
                          disabled={calibrationActive}
                          onClick={handleCalibrate}
                          className="h-10 px-4 bg-white/5 hover:bg-white/10 active:scale-95 border border-white/10 rounded-xl text-[8px] font-black tracking-widest uppercase text-white transition-all flex items-center gap-1.5"
                        >
                          <RefreshCw className={`w-3 h-3 ${calibrationActive ? 'animate-spin text-[#00FF9C]' : ''}`} />
                          Sync
                        </button>
                      </div>
                    </div>

                    {/* RANGE REGULATOR */}
                    <div className="space-y-4">
                      <div className="flex justify-between items-center text-[9px] font-mono uppercase tracking-[0.2em] text-white/30">
                        <span>Sector Scan Range</span>
                        <span className="text-[#00FF9C] font-black">{radius} KM</span>
                      </div>
                      <div className="p-5 bg-white/[0.02] border border-white/5 rounded-[2rem] space-y-3">
                        <div className="flex justify-between text-[11px] font-mono text-white/50">
                          <span>CLOSE Range</span>
                          <span>METRO Wide</span>
                        </div>
                        <input
                          type="range"
                          min="1"
                          max="20"
                          step="1"
                          value={radius}
                          onChange={(e) => onRadiusChange(Number(e.target.value))}
                          className="w-full accent-[#00FF9C] bg-white/15 h-1 rounded-lg outline-none cursor-pointer"
                        />
                        <p className="text-[7px] font-mono text-white/20 uppercase tracking-widest leading-relaxed">
                          *Adjusts the radar scanning field bounds. Higher range increases processing latency.
                        </p>
                      </div>
                    </div>

                    {/* FREQUENCY BAND SELECTOR */}
                    <div className="space-y-4">
                      <div className="flex justify-between items-center text-[9px] font-mono uppercase tracking-[0.2em] text-white/30">
                        <span>Signal Frequencies Filter</span>
                        <span className="text-white/50">{activeCategories.length} Locked</span>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        {CATEGORIES.map(cat => {
                          const isActive = activeCategories.includes(cat);
                          return (
                            <button
                              key={cat}
                              onClick={() => onToggleCategory(cat)}
                              className={`h-9 px-4 rounded-full text-[8.5px] font-black uppercase tracking-wider transition-all border flex items-center gap-1.5 ${
                                isActive 
                                  ? 'bg-[#00FF9C] text-black border-[#00FF9C] shadow-[0_0_15px_rgba(0,255,156,0.25)]' 
                                  : 'bg-white/5 hover:bg-white/10 text-white/60 border-white/5 hover:border-white/10'
                              }`}
                            >
                              <div className={`w-1.5 h-1.5 rounded-full ${isActive ? 'bg-black' : 'bg-white/20'}`} />
                              {cat}
                            </button>
                          );
                        })}
                      </div>
                    </div>

                    {/* VIRTUAL COCKPIT METRICS */}
                    <div className="p-5 bg-brand-primary/[0.02] border border-[#00FF9C]/10 rounded-3xl grid grid-cols-2 gap-4">
                      <div className="space-y-1">
                        <span className="text-[7.5px] font-mono text-white/20 uppercase tracking-widest">WLLS Rhythm Level</span>
                        <p className="text-sm font-black italic uppercase text-white tracking-widest flex items-center gap-1.5">
                          <Wifi className="w-3.5 h-3.5 text-[#00FF9C] animate-pulse" />
                          STABLE_FLUX
                        </p>
                      </div>
                      <div className="space-y-1">
                        <span className="text-[7.5px] font-mono text-white/20 uppercase tracking-widest">Energy Feed Rate</span>
                        <p className="text-sm font-black italic uppercase text-white tracking-widest flex items-center gap-1.5">
                          <Zap className="w-3.5 h-3.5 text-yellow-400" />
                          974.2 GigaHz
                        </p>
                      </div>
                    </div>

                  </div>

                  <div className="pt-4 border-t border-white/5 hidden md:block">
                    <p className="text-[7px] font-mono text-white/20 uppercase tracking-widest leading-relaxed">
                      Operator Diagnostic Panel v0.9 // System variables fully synced with primary telemetry plane.
                    </p>
                  </div>
                </div>
              ) : null}

              {/* RIGHT 7 COLS: DIALOGUE CONSOLE (miniConcierge / Unified miniAnimal) */}
              <div className={`flex flex-col justify-between ${
                activePersona === 'auto'
                  ? 'col-span-12' // Fill full screen if auto is selected
                  : activePersona === 'animal' && animalSubTab === 'CONSOLE'
                    ? 'col-span-12 md:hidden' // hide dialogue on mobile inside console layout
                    : 'col-span-12 md:col-span-7' // standard partition
              }`}>
                
                {/* DUAL SUB-TABS ON MOBILE FOR UNIFIED ANIMAL PERFORMA */}
                {activePersona === 'animal' && (
                  <div className="md:hidden flex border-b border-white/5 bg-black/10 shrink-0">
                    <button
                      onClick={() => setAnimalSubTab('DIALOGUE')}
                      className={`flex-1 py-4 text-[9px] font-black uppercase tracking-widest transition-all ${
                        animalSubTab === 'DIALOGUE' ? 'text-[#00FF9C] border-b-2 border-[#00FF9C]' : 'text-white/40'
                      }`}
                    >
                      Dialogue Chat
                    </button>
                    <button
                      onClick={() => setAnimalSubTab('CONSOLE')}
                      className={`flex-1 py-4 text-[9px] font-black uppercase tracking-widest transition-all ${
                        animalSubTab === 'CONSOLE' ? 'text-[#00FF9C] border-b-2 border-[#00FF9C]' : 'text-white/40'
                      }`}
                    >
                      Calibration Console
                    </button>
                  </div>
                )}

                {/* DYNAMIC AVATAR / WORKSPACE GRAPHIC VIEW */}
                <div className="bg-black/30 border-b border-white/5 p-4 shrink-0 flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <span className="text-[8px] font-mono text-white/30 uppercase tracking-[0.2m]">A.I_Visualplane:</span>
                    <span className="text-[9px] font-black uppercase tracking-widest italic text-[#00FF9C]">
                      {activePersona === 'auto' && 'MINIAUTO_SCANNER_BLUEPRINT'}
                      {activePersona === 'concierge' && 'MINICONCIERGE_ENERGY_WAVE'}
                      {activePersona === 'animal' && 'MINIANIMAL_SPIKE_AVATAR'}
                    </span>
                  </div>

                  {activePersona === 'animal' && (
                    <div className="flex items-center gap-3">
                      <button
                        onClick={() => {
                          const barkLog = {
                            sender: 'assistant' as const,
                            text: "RUFF! BARK! *Spike panting enthusiastically* Let's track some grid vibrations!",
                            timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
                          };
                          setChatLog(prev => [...prev, barkLog]);
                          setIsSpeaking(true);
                          setTimeout(() => setIsSpeaking(false), 2000);
                        }}
                        className="px-3 py-1 bg-[#00FF9C]/10 hover:bg-[#00FF9C]/20 text-[#00FF9C] border border-[#00FF9C]/20 rounded-md text-[8px] font-black tracking-widest uppercase transition-all"
                      >
                        BARK_FEEDBACK
                      </button>
                    </div>
                  )}
                </div>

                {/* THE MINI RENDERS STAGE */}
                <div className="py-4 px-6 md:py-6 bg-black/40 border-b border-white/5 flex items-center justify-center shrink-0">
                  {/* Persona Render Options */}
                  <AnimatePresence mode="wait">
                    {/* MINI AUTO RENDER */}
                    {activePersona === 'auto' && (
                      <motion.div
                        key="auto"
                        initial={{ opacity: 0, scale: 0.8 }}
                        animate={{ opacity: 1, scale: 1 }}
                        exit={{ opacity: 0, scale: 0.8 }}
                        className="relative w-40 h-40 flex items-center justify-center"
                      >
                        {/* Blueprint Rotating Grid */}
                        <div className="absolute inset-0 rounded-full border border-dashed border-[#00FF9C]/20 animate-spin" style={{ animationDuration: '24s' }} />
                        <div className="absolute inset-2 rounded-full border border-dotted border-[#00FF9C]/10 animate-reverse-spin" style={{ animationDuration: '14s' }} />
                        
                        {/* Stylized Neon Vector Car Grid (Mini Cooper Wireframe) */}
                        <svg className="w-28 h-28 text-[#00FF9C]" viewBox="0 0 100 100" fill="none">
                          <motion.path 
                            d="M 15,65 Q 15,55 25,50 L 35,38 Q 42,35 50,35 Q 58,35 65,38 L 75,50 Q 85,55 85,65 Q 85,73 75,75 L 25,75 Q 15,73 15,65 Z" 
                            stroke="currentColor" 
                            strokeWidth="1.5"
                            strokeDasharray="180"
                            strokeDashoffset={calibrationActive ? [180, 0] : 0}
                            animate={calibrationActive ? { strokeDashoffset: [180, 0] } : {}}
                            transition={{ duration: 1.5 }}
                          />
                          <circle cx="33" cy="74" r="8" stroke="currentColor" strokeWidth="1.5" />
                          <circle cx="67" cy="74" r="8" stroke="currentColor" strokeWidth="1.5" />
                          <line x1="25" y1="58" x2="75" y2="58" stroke="currentColor" strokeWidth="1" strokeDasharray="3 3" />
                          <circle cx="50" cy="50" r="1.5" fill="currentColor" className="animate-ping" />
                        </svg>
                      </motion.div>
                    )}

                    {/* MINI CONCIERGE RENDER */}
                    {activePersona === 'concierge' && (
                      <motion.div
                        key="concierge"
                        initial={{ opacity: 0, scale: 0.8 }}
                        animate={{ opacity: 1, scale: 1 }}
                        exit={{ opacity: 0, scale: 0.8 }}
                        className="relative w-40 h-40 flex items-center justify-center"
                      >
                        {/* Orbital particle rings */}
                        <motion.div 
                          animate={{ rotate: 360 }}
                          transition={{ repeat: Infinity, ease: 'linear', duration: 10 }}
                          className="absolute inset-0 rounded-full border border-[#00FF9C]/20 border-t-transparent"
                        />
                        <motion.div 
                          animate={{ rotate: -360 }}
                          transition={{ repeat: Infinity, ease: 'linear', duration: 6 }}
                          className="absolute inset-4 rounded-full border border-dashed border-[#00FF9C]/10 border-b-transparent"
                        />
                        
                        {/* Pulsating Orb */}
                        <motion.div
                          animate={{ 
                            scale: isThinking ? [1, 1.1, 0.9, 1.05, 1] : [1, 1.05, 1],
                            opacity: isThinking ? [0.6, 1, 0.8, 1] : [0.7, 0.9, 0.7]
                          }}
                          transition={{ repeat: Infinity, duration: isThinking ? 1.5 : 3 }}
                          className="w-16 h-16 rounded-full bg-[#00FF9C]/10 border border-[#00FF9C]/40 flex items-center justify-center shadow-[0_0_30px_rgba(0,255,156,0.2)]"
                        >
                          <Orbit className="w-7 h-7 text-[#00FF9C] animate-pulse" />
                        </motion.div>
                      </motion.div>
                    )}

                    {/* MINI ANIMAL RENDER (SPIKE CYBER BULLDOG) */}
                    {activePersona === 'animal' && (
                      <motion.div
                        key="animal"
                        initial={{ opacity: 0, scale: 0.8 }}
                        animate={{ opacity: 1, scale: 1 }}
                        exit={{ opacity: 0, scale: 0.8 }}
                        className="relative w-40 h-40 flex items-center justify-center"
                      >
                        {/* Cybernetic HUD elements in the background */}
                        <div className="absolute inset-0 rounded-full border border-dashed border-[#00FF9C]/10 animate-spin" style={{ animationDuration: '30s' }} />
                        <div className="absolute inset-4 rounded-full border border-[#00FF9C]/5 animate-reverse-spin" style={{ animationDuration: '20s' }} />

                        {/* Bulldog SVG */}
                        <svg className="w-36 h-36 text-[#00FF9C] drop-shadow-[0_0_15px_rgba(0,255,156,0.25)]" viewBox="0 0 100 100" fill="none" xmlns="http://www.w3.org/2000/svg">
                          {/* Ears */}
                          <motion.polygon 
                            points="15,40 25,20 40,35" 
                            fill="currentColor" 
                            fillOpacity="0.08" 
                            stroke="currentColor" 
                            strokeWidth="1.5"
                            animate={{ rotate: isThinking ? [-3, 3, -3] : [0, 1.5, 0] }}
                            transition={{ repeat: Infinity, duration: 1.2 }}
                          />
                          <motion.polygon 
                            points="85,40 75,20 60,35" 
                            fill="currentColor" 
                            fillOpacity="0.08" 
                            stroke="currentColor" 
                            strokeWidth="1.5"
                            animate={{ rotate: isThinking ? [3, -3, 3] : [0, -1.5, 0] }}
                            transition={{ repeat: Infinity, duration: 1.2 }}
                          />
                          
                          {/* Bulldog Face Outline */}
                          <motion.path 
                            d="M 25,45 Q 20,60 30,75 Q 50,85 70,75 Q 80,60 75,45 Q 50,38 25,45 Z" 
                            fill="#060606" 
                            stroke="currentColor" 
                            strokeWidth="2" 
                            animate={{ scale: isSpeaking ? [1, 1.02, 1] : 1 }}
                            transition={{ repeat: Infinity, duration: 0.4 }}
                          />

                          {/* Brow Lines / Cyber Implants */}
                          <path d="M 30,42 L 42,48 M 70,42 L 58,48" stroke="currentColor" strokeWidth="1" strokeOpacity="0.4" />
                          
                          {/* Bulldog Cheeks/Muzzle */}
                          <path d="M 33,60 Q 50,55 67,60 Q 72,70 65,77 Q 50,80 35,77 Q 28,70 33,60 Z" fill="#0b0b0b" stroke="currentColor" strokeWidth="1.5" />
                          
                          {/* Spike Collar with Glowing Cyber-Nobs */}
                          <path d="M 28,75 Q 50,88 72,75" stroke="#ff0055" strokeWidth="2.5" />
                          <circle cx="36" cy="79" r="1" fill="#00FF9C" />
                          <circle cx="50" cy="82" r="1" fill="#00FF9C" />
                          <circle cx="64" cy="79" r="1" fill="#00FF9C" />

                          {/* Cyber Eyes */}
                          <g>
                            <motion.circle 
                              cx="38" 
                              cy="50" 
                              r="3.5" 
                              fill="currentColor" 
                              animate={{ scaleY: isBlinking ? 0 : 1 }}
                              transition={{ repeat: Infinity, repeatDelay: 4, duration: 0.15 }}
                            />
                            <motion.circle 
                              cx="62" 
                              cy="50" 
                              r="3.5" 
                              fill="currentColor" 
                              animate={{ scaleY: isBlinking ? 0 : 1 }}
                              transition={{ repeat: Infinity, repeatDelay: 4, duration: 0.15 }}
                            />
                            {/* Tech HUD circles around eyes */}
                            <circle cx="38" cy="50" r="6" stroke="currentColor" strokeWidth="0.5" strokeDasharray="2 1" className="animate-spin" style={{ animationDuration: '8s' }} />
                            <circle cx="62" cy="50" r="6" stroke="currentColor" strokeWidth="0.5" strokeDasharray="2 1" className="animate-spin" style={{ animationDuration: '8s' }} />
                          </g>

                          {/* Bulldog Nose */}
                          <polygon points="46,57 54,57 50,62" fill="currentColor" />

                          {/* Jowls details */}
                          <path d="M 45,62 Q 50,66 55,62 M 50,62 L 50,72" stroke="currentColor" strokeWidth="1" />
                        </svg>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </div>

                {/* THE CHAT DIALOG LOG - SCROLL LOCKED AND CONSTANT HEIGHT */}
                <div 
                  ref={scrollRef}
                  className="flex-1 min-h-0 overflow-y-auto p-6 space-y-4 no-scrollbar bg-black/10"
                >
                  <AnimatePresence initial={false}>
                    {chatLog.map((log, index) => {
                      if (log.sender === 'system') {
                        return (
                          <motion.div
                            key={index}
                            initial={{ opacity: 0, y: 10 }}
                            animate={{ opacity: 1, y: 0 }}
                            className="p-3 bg-red-500/5 rounded-2xl border border-red-500/10 flex items-start gap-3"
                          >
                            <Sliders className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
                            <div className="flex-1 space-y-1">
                              <p className="text-[7.5px] font-mono text-white/30 uppercase tracking-widest">{log.timestamp}</p>
                              <p className="text-[10px] font-mono text-red-400 uppercase tracking-wider">{log.text}</p>
                            </div>
                          </motion.div>
                        );
                      }

                      const isSelf = log.sender === 'user';
                      return (
                        <motion.div
                          key={index}
                          initial={{ opacity: 0, y: 15 }}
                          animate={{ opacity: 1, y: 0 }}
                          className={`flex items-start gap-4 ${isSelf ? 'flex-row-reverse' : ''}`}
                        >
                          <div className={`w-8 h-8 rounded-xl flex items-center justify-center shrink-0 border ${
                            isSelf 
                              ? 'bg-[#00FF9C]/10 border-[#00FF9C]/20 text-[#00FF9C]' 
                              : 'bg-white/5 border-white/10 text-white/50'
                          }`}>
                            {isSelf ? <Sliders className="w-4 h-4" /> : <Bot className="w-4 h-4" />}
                          </div>

                          <div className="space-y-1 max-w-[80%]">
                            <div className={`flex items-center gap-2 ${isSelf ? 'flex-row-reverse' : ''}`}>
                              <span className="text-[8px] font-mono font-bold tracking-widest uppercase text-white/30">
                                {isSelf ? 'OPERATOR' : activePersona === 'animal' ? 'SPIKE_AI' : 'CONCIERGE_AI'}
                              </span>
                              <span className="text-[7px] font-mono text-white/15">{log.timestamp}</span>
                            </div>
                            <div className={`p-4 rounded-3xl text-[11px] leading-relaxed tracking-wide ${
                              isSelf
                                ? 'bg-[#00FF9C] text-black font-semibold rounded-tr-none'
                                : 'bg-white/5 border border-white/5 text-white/85 rounded-tl-none'
                            }`}>
                              {log.text}
                            </div>
                          </div>
                        </motion.div>
                      );
                    })}

                    {isThinking && (
                      <motion.div
                        initial={{ opacity: 0, y: 10 }}
                        animate={{ opacity: 1, y: 0 }}
                        exit={{ opacity: 0 }}
                        className="flex items-start gap-4"
                      >
                        <div className="w-8 h-8 rounded-xl bg-white/5 border border-white/10 flex items-center justify-center text-white/50 shrink-0">
                          <Bot className="w-4 h-4 animate-spin text-[#00FF9C]" />
                        </div>
                        <div className="p-4 bg-white/5 border border-white/5 rounded-3xl rounded-tl-none flex gap-1 items-center">
                          {[0, 1, 2].map(i => (
                            <div 
                              key={i} 
                              className="w-1.5 h-1.5 rounded-full bg-[#00FF9C] animate-bounce"
                              style={{ animationDelay: `${i * 0.15}s`, animationDuration: '0.8s' }}
                            />
                          ))}
                        </div>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </div>

                {/* DIA INPUT BAR WITH CHIPS FOR INSTANT TRIGGERS */}
                <div className="p-6 border-t border-white/5 bg-black/40 shrink-0 space-y-4">
                  {/* Suggestion Chips */}
                  <div className="flex gap-2 overflow-x-auto no-scrollbar pb-1">
                    <button
                      onClick={() => handleSuggestion("🐶 Spike, suggest concrete dynamic nightlife near me!")}
                      className="shrink-0 h-7 px-3 bg-white/5 hover:bg-white/10 border border-white/5 hover:border-white/10 rounded-full text-[8px] font-bold uppercase tracking-wider text-white/55 transition-all flex items-center gap-1"
                    >
                      <Sparkles className="w-2.5 h-2.5 text-[#00FF9C]" />
                      Suggest Hotspots
                    </button>
                    <button
                      onClick={() => handleSuggestion("🚗 Spike, calibrate the radar system range!")}
                      className="shrink-0 h-7 px-3 bg-white/5 hover:bg-white/10 border border-white/5 hover:border-white/10 rounded-full text-[8px] font-bold uppercase tracking-wider text-white/55 transition-all flex items-center gap-1"
                    >
                      <Sliders className="w-2.5 h-2.5 text-[#00FF9C]" />
                      Calibrate Radar
                    </button>
                    <button
                      onClick={() => handleSuggestion("🔮 Scan Sector 4 and check for Rooftops")}
                      className="shrink-0 h-7 px-3 bg-white/5 hover:bg-white/10 border border-white/5 hover:border-white/10 rounded-full text-[8px] font-bold uppercase tracking-wider text-white/55 transition-all flex items-center gap-1"
                    >
                      <Compass className="w-2.5 h-2.5 text-[#00FF9C]" />
                      Find Rooftops
                    </button>
                  </div>

                  <form onSubmit={handleSend} className="relative flex items-center">
                    <input
                      type="text"
                      placeholder={activePersona === 'auto' ? "System diagnostic tuning commands only..." : "Ask your WeUP co-pilot..."}
                      disabled={activePersona === 'auto'}
                      value={inputText}
                      onChange={(e) => setInputText(e.target.value)}
                      className="w-full h-14 bg-white/5 border border-white/10 rounded-2xl pl-5 pr-14 text-xs font-mono text-white placeholder:text-white/20 outline-none focus:border-[#00FF9C] transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                    />
                    <button
                      type="submit"
                      disabled={!inputText.trim() || isThinking || activePersona === 'auto'}
                      className="absolute right-3 w-10 h-10 rounded-xl bg-[#00FF9C] text-black flex items-center justify-center font-bold hover:scale-105 active:scale-95 disabled:opacity-30 disabled:scale-100 disabled:hover:scale-100 transition-all cursor-pointer"
                    >
                      <Send className="w-4 h-4" />
                    </button>
                  </form>
                </div>

              </div>

            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
