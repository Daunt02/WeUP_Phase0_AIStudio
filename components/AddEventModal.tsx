"use client";

import React, { useState, useEffect, useRef } from "react";
import { motion, AnimatePresence } from "motion/react";
import {
  Upload,
  Link as LinkIcon,
  Plus,
  Map as MapIcon,
  X,
  Check,
  Loader2,
  MapPin,
  Calendar,
  Clock,
  Tag,
  ChevronRight,
  ArrowRight,
  AlertCircle,
} from "lucide-react";
import Image from "next/image";
import { publicEnv } from "../lib/env/public";
import type { SubmissionDraftProjection } from "@/features/world/runtimeTypes";
import {
  getDeterministicDraft,
  PHASE0_FIXED_NOW,
} from "@/lib/testing/phase0Seed";
import { uploadFlyer } from "@/services/mediaService";

interface AddEventModalProps {
  isVisible: boolean;
  onClose: () => void;
  onPublish: (event: SubmissionDraftProjection) => Promise<any> | void;
  onGhostUpdate: (event: Partial<SubmissionDraftProjection> | null) => void;
  ghostEvent?: Partial<SubmissionDraftProjection> | null;
  mapCenter: { lat: number; lng: number };
}

type Step =
  | "CHOICE"
  | "PASTE_LINK"
  | "UPLOADING"
  | "EXTRACTING"
  | "PREVIEW"
  | "CONFIRM_LOCATION"
  | "EDIT_DETAILS"
  | "SUCCESS"
  | "DUPLICATE"
  | "ERROR";

export default function AddEventModal({
  isVisible,
  onClose,
  onPublish,
  onGhostUpdate,
  ghostEvent,
  mapCenter,
}: AddEventModalProps) {
  const [step, setStep] = useState<Step>("CHOICE");
  const [uploadProgress, setUploadProgress] = useState(0);
  const [extractedData, setExtractedData] =
    useState<Partial<SubmissionDraftProjection> | null>(null);
  const [confidenceScore, setConfidenceScore] = useState(0);
  const [isPublishing, setIsPublishing] = useState(false);
  const [isGeocoding, setIsGeocoding] = useState(false);
  const [manualAddress, setManualAddress] = useState("");
  const [linkInput, setLinkInput] = useState("");
  const [errorMessage, setErrorMessage] = useState("");
  const [uploadedFlyerAssetId, setUploadedFlyerAssetId] = useState<
    string | null
  >(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const buildDraftId = (title: string) => {
    const slug = title
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/(^-|-$)/g, "");

    return `draft-${slug || "phase0-signal"}`;
  };

  // Sync with map drag
  useEffect(() => {
    if (
      ghostEvent &&
      extractedData &&
      (ghostEvent.latitude !== extractedData.latitude ||
        ghostEvent.longitude !== extractedData.longitude)
    ) {
      setExtractedData((prev) =>
        prev
          ? {
              ...prev,
              latitude: ghostEvent.latitude,
              longitude: ghostEvent.longitude,
            }
          : null,
      );
    }
  }, [ghostEvent, extractedData]);

  const [isClient, setIsClient] = useState(false);
  useEffect(() => {
    const timer = setTimeout(() => setIsClient(true), 0);
    return () => clearTimeout(timer);
  }, []);

  const [prevVisible, setPrevVisible] = useState(isVisible);
  // Avoid render-time mutation: keep visibility-driven initialization inside an effect
  useEffect(() => {
    if (isVisible && !prevVisible) {
      setPrevVisible(true);
      setStep("CHOICE");
      setUploadProgress(0);
      setExtractedData(null);
      setLinkInput("");
      setErrorMessage("");
      setUploadedFlyerAssetId(null);
    } else if (!isVisible && prevVisible) {
      setPrevVisible(false);
    }
  }, [isVisible, prevVisible]);

  const handleChoice = (choice: "UPLOAD" | "LINK" | "MANUAL" | "CENTER") => {
    if (choice === "UPLOAD") {
      fileInputRef.current?.click();
    } else if (choice === "LINK") {
      setStep("PASTE_LINK");
    } else if (choice === "MANUAL") {
      const blankData = getDeterministicDraft("MANUAL", mapCenter);
      setExtractedData(blankData);
      onGhostUpdate(blankData);
      setStep("EDIT_DETAILS");
    } else if (choice === "CENTER") {
      const initialData = getDeterministicDraft("CENTER", mapCenter);
      setExtractedData(initialData);
      onGhostUpdate(initialData);
      setStep("CONFIRM_LOCATION");
    }
  };

  const handleFlyerSelected = async (
    event: React.ChangeEvent<HTMLInputElement>,
  ) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;

    if (!file.type.startsWith("image/")) {
      setErrorMessage("Only image files are supported for flyer upload.");
      setStep("ERROR");
      return;
    }

    setStep("UPLOADING");
    setUploadProgress(15);

    let progress = 15;
    const progressInterval = setInterval(() => {
      progress = Math.min(progress + 12, 90);
      setUploadProgress(progress);
    }, 160);

    try {
      const localUploaderId =
        typeof window !== "undefined"
          ? localStorage.getItem("weup_dev_user_id") || "phase0-ui-uploader"
          : "phase0-ui-uploader";

      const assetId = await uploadFlyer(file, localUploaderId);
      clearInterval(progressInterval);
      setUploadProgress(100);
      setUploadedFlyerAssetId(assetId);
      setStep("EXTRACTING");
      const localPreviewUrl = URL.createObjectURL(file);
      simulateExtraction("UPLOAD", localPreviewUrl);
    } catch (err) {
      clearInterval(progressInterval);
      setErrorMessage((err as Error)?.message || "Flyer upload failed.");
      setStep("ERROR");
    }
  };

  const geocodeAddress = async (address: string) => {
    const token = publicEnv.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN;
    if (!token || !address) return null;

    setIsGeocoding(true);
    try {
      const response = await fetch(
        `https://api.mapbox.com/geocoding/v5/mapbox.places/${encodeURIComponent(address)}.json?access_token=${token}&limit=1`,
      );
      const data = await response.json();
      if (data.features && data.features.length > 0) {
        const feature = data.features[0];
        return {
          latitude: feature.center[1],
          longitude: feature.center[0],
          full_address: feature.place_name,
          confidence: feature.relevance || 0.5,
        };
      }
    } catch (err) {
      console.error("Geocoding error:", err);
    } finally {
      setIsGeocoding(false);
    }
    return null;
  };

  const simulateExtraction = async (
    source: "UPLOAD" | "LINK",
    uploadedPreviewUrl?: string,
  ) => {
    setTimeout(async () => {
      // Logic for different link types to show different states
      if (source === "LINK") {
        if (linkInput.toLowerCase().includes("error")) {
          setErrorMessage(
            "The link provided does not contain valid event metadata or is restricted.",
          );
          setStep("ERROR");
          return;
        }
        if (linkInput.toLowerCase().includes("duplicate")) {
          setStep("DUPLICATE");
          return;
        }
      }

      const seededDraft = getDeterministicDraft(source, mapCenter);
      const rawAddress = seededDraft.address || "";
      const geocodeResult = await geocodeAddress(rawAddress);

      const mockExtracted: Partial<SubmissionDraftProjection> = {
        ...seededDraft,
        imageUrl:
          source === "UPLOAD"
            ? uploadedPreviewUrl || seededDraft.imageUrl
            : seededDraft.imageUrl,
        address:
          geocodeResult?.full_address || seededDraft.address || rawAddress,
        latitude:
          geocodeResult?.latitude || seededDraft.latitude || mapCenter.lat,
        longitude:
          geocodeResult?.longitude || seededDraft.longitude || mapCenter.lng,
        status:
          (geocodeResult?.confidence || seededDraft.confidence || 0) > 0.8
            ? "PUBLISHED"
            : "NEEDS_REVIEW",
        confidence: geocodeResult?.confidence || seededDraft.confidence || 0.5,
      };
      setExtractedData(mockExtracted);
      onGhostUpdate(mockExtracted);
      setConfidenceScore(geocodeResult?.confidence || 0.5);
      setManualAddress(geocodeResult?.full_address || rawAddress);

      if (source === "LINK") {
        setStep("PREVIEW");
      } else {
        setStep("CONFIRM_LOCATION");
      }
    }, 2000);
  };

  const handlePublish = async () => {
    if (!extractedData) return;
    // STRICT VALIDATION
    if (
      !extractedData.title ||
      !extractedData.startTime ||
      !extractedData.venueName ||
      !extractedData.address ||
      !extractedData.latitude ||
      !extractedData.longitude
    ) {
      alert(
        "CRITICAL: Missing required signal data. Title, Time, Venue, and Validated Address are mandatory.",
      );
      return;
    }

    const finalEvent: SubmissionDraftProjection = {
      ...(extractedData as SubmissionDraftProjection),
      id:
        extractedData.id ||
        buildDraftId(extractedData.title || "phase0-signal"),
      priceTier: extractedData.priceTier || "$$",
      source: "manual",
      status: "PUBLISHED",
      confidence: extractedData.confidence || 1,
    };

    const submissionPayload: SubmissionDraftProjection = {
      ...finalEvent,
      ...(uploadedFlyerAssetId
        ? { flyerAssetIds: [uploadedFlyerAssetId] }
        : {}),
    };

    try {
      setIsPublishing(true);
      const res = await onPublish(submissionPayload);
      setIsPublishing(false);
      setStep("SUCCESS");
      setTimeout(() => onClose(), 1500);
    } catch (err) {
      console.error("Publish failed", err);
      setIsPublishing(false);
      setStep("ERROR");
      setErrorMessage((err as any)?.message || "Publish failed.");
    }
  };

  if (!isVisible) return null;

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-[1000] flex items-center justify-center p-6 pointer-events-none">
        {/* Backdrop for Choice/Edit steps */}
        {(step === "CHOICE" || step === "EDIT_DETAILS") && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="absolute inset-0 bg-black/60 backdrop-blur-sm pointer-events-auto"
            onClick={onClose}
          />
        )}

        <motion.div
          initial={{ opacity: 0, scale: 0.9, y: 20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.9, y: 20 }}
          className={`
            relative w-full max-w-md bg-black/90 backdrop-blur-3xl border border-white/10 rounded-[2.5rem] overflow-hidden pointer-events-auto shadow-[0_30px_100px_rgba(0,0,0,0.8)]
            ${step === "CONFIRM_LOCATION" ? "mt-auto mb-24 max-w-sm" : ""}
          `}
        >
          {/* Header */}
          <div className="p-6 border-b border-white/5 flex items-center justify-between">
            <h2 className="text-xl font-black uppercase italic tracking-tighter text-white">
              {step === "CHOICE" && "Add Event"}
              {step === "PASTE_LINK" && "Paste Signal Link"}
              {step === "UPLOADING" && "Uploading Flyer"}
              {step === "EXTRACTING" && "Detecting Details"}
              {step === "PREVIEW" && "Extraction Success"}
              {step === "DUPLICATE" && "Duplicate Signal"}
              {step === "ERROR" && "Extraction Failed"}
              {step === "CONFIRM_LOCATION" && "Confirm Location"}
              {step === "EDIT_DETAILS" && "Finalize Signal"}
              {step === "SUCCESS" && "Signal Published"}
            </h2>
            <button
              onClick={onClose}
              className="w-8 h-8 rounded-full bg-white/5 flex items-center justify-center text-white/40 hover:text-white transition-colors"
            >
              <X size={16} />
            </button>
          </div>

          <div className="p-6">
            {/* STEP 1: CHOICE */}
            {step === "CHOICE" && (
              <div className="grid grid-cols-2 gap-3">
                <ChoiceButton
                  icon={<Upload size={20} />}
                  label="Upload Flyer"
                  sub="OCR Extraction"
                  onClick={() => handleChoice("UPLOAD")}
                />
                <ChoiceButton
                  icon={<LinkIcon size={20} />}
                  label="Paste Link"
                  sub="AI Auto-fill"
                  onClick={() => handleChoice("LINK")}
                />
                <ChoiceButton
                  icon={<Plus size={20} />}
                  label="Add Manually"
                  sub="Blank Signal"
                  onClick={() => handleChoice("MANUAL")}
                />
                <ChoiceButton
                  icon={<MapIcon size={20} />}
                  label="Map Center"
                  sub="Current View"
                  onClick={() => handleChoice("CENTER")}
                />
              </div>
            )}

            {/* STEP 1.5: PASTE LINK */}
            {step === "PASTE_LINK" && (
              <div className="space-y-6">
                <div className="space-y-2">
                  <label className="text-[10px] font-mono text-white/40 uppercase tracking-[0.2em]">
                    Source URL
                  </label>
                  <div className="relative">
                    <input
                      autoFocus
                      className="w-full bg-white/5 border border-white/10 rounded-2xl p-4 pr-12 text-sm text-white placeholder:text-white/20 outline-none focus:border-brand-primary transition-all"
                      placeholder="https://instagram.com/p/..."
                      value={linkInput}
                      onChange={(e) => setLinkInput(e.target.value)}
                    />
                    {linkInput && (
                      <button
                        onClick={() => setLinkInput("")}
                        className="absolute right-4 top-1/2 -translate-y-1/2 text-white/20 hover:text-white"
                      >
                        <X size={16} />
                      </button>
                    )}
                  </div>
                  <p className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                    Supports Instagram, Eventbrite, RA, and Dice
                  </p>
                </div>

                <div className="p-4 bg-brand-primary/5 rounded-2xl border border-brand-primary/10">
                  <p className="text-[10px] text-white/60 leading-relaxed italic">
                    &quot;Our AI will parse the link to extract title, venue,
                    time, and imagery automatically.&quot;
                  </p>
                </div>

                <button
                  disabled={!linkInput}
                  onClick={() => {
                    setStep("EXTRACTING");
                    simulateExtraction("LINK");
                  }}
                  className="w-full h-14 bg-white text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] flex items-center justify-center gap-2 disabled:opacity-50 transition-all"
                >
                  Analyze Link <ArrowRight size={16} />
                </button>
              </div>
            )}

            {/* STEP 2: UPLOADING */}
            {step === "UPLOADING" && (
              <div className="py-12 flex flex-col items-center gap-6">
                <div className="relative w-24 h-24">
                  <svg className="w-full h-full -rotate-90">
                    <circle
                      cx="48"
                      cy="48"
                      r="44"
                      className="stroke-white/5 stroke-[4] fill-none"
                    />
                    <motion.circle
                      cx="48"
                      cy="48"
                      r="44"
                      className="stroke-brand-primary stroke-[4] fill-none"
                      strokeDasharray="276"
                      animate={{
                        strokeDashoffset: 276 - (276 * uploadProgress) / 100,
                      }}
                    />
                  </svg>
                  <div className="absolute inset-0 flex items-center justify-center">
                    <Upload className="text-brand-primary animate-pulse" />
                  </div>
                </div>
                <p className="text-[10px] font-mono text-white/40 uppercase tracking-[0.2em]">
                  Uploading Signal Data...
                </p>
              </div>
            )}

            {/* STEP 3: EXTRACTING */}
            {step === "EXTRACTING" && (
              <div className="py-12 flex flex-col items-center gap-6">
                <div className="flex gap-2">
                  {[...Array(3)].map((_, i) => (
                    <motion.div
                      key={i}
                      animate={{
                        height: [20, 60, 20],
                        opacity: [0.3, 1, 0.3],
                      }}
                      transition={{
                        duration: 1,
                        repeat: Infinity,
                        delay: i * 0.2,
                      }}
                      className="w-2 bg-brand-primary rounded-full"
                    />
                  ))}
                </div>
                <div className="text-center space-y-2">
                  <p className="text-[10px] font-mono text-white/40 uppercase tracking-[0.2em]">
                    Analyzing Visual Signal
                  </p>
                  <p className="text-[8px] font-mono text-brand-primary/60 uppercase tracking-widest">
                    Detecting Title, Venue, Time...
                  </p>
                </div>
              </div>
            )}

            {/* STEP 3.5: PREVIEW */}
            {step === "PREVIEW" && extractedData && (
              <div className="space-y-6">
                <div className="relative aspect-video w-full rounded-2xl overflow-hidden border border-white/10">
                  <Image
                    src={extractedData.imageUrl || ""}
                    alt="Preview"
                    fill
                    className="object-cover"
                    referrerPolicy="no-referrer"
                  />
                  <div className="absolute inset-0 bg-gradient-to-t from-black/80 to-transparent" />
                  <div className="absolute bottom-4 left-4 right-4">
                    <h3 className="text-lg font-black uppercase italic tracking-tighter text-white leading-none">
                      {extractedData.title}
                    </h3>
                    <p className="text-[10px] font-mono text-white/60 uppercase tracking-widest mt-1">
                      {extractedData.venueName}
                    </p>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div className="p-3 bg-white/5 rounded-xl border border-white/10 flex items-center gap-3">
                    <Calendar size={14} className="text-brand-primary" />
                    <span className="text-[10px] font-mono text-white/80 uppercase tracking-widest">
                      {extractedData.startTime
                        ? new Date(extractedData.startTime).toLocaleDateString()
                        : "DATE_MISSING"}
                    </span>
                  </div>
                  <div className="p-3 bg-white/5 rounded-xl border border-white/10 flex items-center gap-3">
                    <Clock size={14} className="text-brand-primary" />
                    <span className="text-[10px] font-mono text-white/80 uppercase tracking-widest">
                      {extractedData.startTime
                        ? new Date(extractedData.startTime).toLocaleTimeString(
                            [],
                            { hour: "2-digit", minute: "2-digit" },
                          )
                        : "TIME_MISSING"}
                    </span>
                  </div>
                </div>

                <div className="flex gap-3">
                  <button
                    onClick={() => setStep("PASTE_LINK")}
                    className="flex-1 h-14 bg-white/5 border border-white/10 text-white rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] hover:bg-white/10 transition-all"
                  >
                    Retry
                  </button>
                  <button
                    onClick={() => setStep("CONFIRM_LOCATION")}
                    className="flex-[2] h-14 bg-brand-primary text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] flex items-center justify-center gap-2 shadow-[0_10px_30px_rgba(var(--brand-primary-rgb),0.3)]"
                  >
                    Continue <ArrowRight size={16} />
                  </button>
                </div>
              </div>
            )}

            {/* STEP 3.6: DUPLICATE */}
            {step === "DUPLICATE" && (
              <div className="py-8 flex flex-col items-center gap-6 text-center">
                <div className="w-20 h-20 rounded-full bg-amber-500/20 flex items-center justify-center text-amber-500">
                  <AlertCircle size={40} />
                </div>
                <div className="space-y-2">
                  <p className="text-xl font-black uppercase italic tracking-tighter text-white">
                    Signal Already Active
                  </p>
                  <p className="text-[10px] font-mono text-white/40 uppercase tracking-[0.2em] leading-relaxed">
                    This event has already been published to the radar by
                    another user.
                  </p>
                </div>
                <button
                  onClick={onClose}
                  className="w-full h-14 bg-white text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px]"
                >
                  Return to Radar
                </button>
              </div>
            )}

            {/* STEP 3.7: ERROR */}
            {step === "ERROR" && (
              <div className="py-8 flex flex-col items-center gap-6 text-center">
                <div className="w-20 h-20 rounded-full bg-red-500/20 flex items-center justify-center text-red-500">
                  <X size={40} />
                </div>
                <div className="space-y-2">
                  <p className="text-xl font-black uppercase italic tracking-tighter text-white">
                    Extraction Failed
                  </p>
                  <p className="text-[10px] font-mono text-white/40 uppercase tracking-[0.2em] leading-relaxed">
                    {errorMessage ||
                      "We couldn't parse a valid signal from this link."}
                  </p>
                </div>
                <div className="flex gap-3 w-full">
                  <button
                    onClick={() => setStep("PASTE_LINK")}
                    className="flex-1 h-14 bg-white/5 border border-white/10 text-white rounded-2xl font-black uppercase tracking-[0.2em] text-[10px]"
                  >
                    Try Again
                  </button>
                  <button
                    onClick={() => handleChoice("MANUAL")}
                    className="flex-1 h-14 bg-white text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px]"
                  >
                    Manual Entry
                  </button>
                </div>
              </div>
            )}

            {/* STEP 4: CONFIRM LOCATION */}
            {step === "CONFIRM_LOCATION" && extractedData && (
              <div className="space-y-6">
                <div className="p-4 bg-white/5 rounded-2xl border border-white/10 space-y-4">
                  <div className="space-y-4">
                    <div className="flex items-center gap-3">
                      <div
                        className={`w-10 h-10 rounded-full flex items-center justify-center shrink-0 ${
                          confidenceScore > 0.8
                            ? "bg-green-500/20 text-green-500"
                            : confidenceScore > 0.4
                              ? "bg-amber-500/20 text-amber-500"
                              : "bg-red-500/20 text-red-500"
                        }`}
                      >
                        {confidenceScore > 0.8 ? (
                          <Check size={20} />
                        ) : (
                          <AlertCircle size={20} />
                        )}
                      </div>
                      <div className="flex-1 min-w-0">
                        <input
                          className="w-full bg-transparent border-b border-white/10 text-[10px] font-black uppercase text-white outline-none focus:border-brand-primary pb-1"
                          value={extractedData.venueName}
                          onChange={(e) =>
                            setExtractedData({
                              ...extractedData,
                              venueName: e.target.value,
                            })
                          }
                          placeholder="VENUE_NAME"
                        />
                        <p className="text-[8px] font-mono text-white/40 uppercase tracking-widest mt-1">
                          {confidenceScore > 0.8
                            ? "High Confidence Match"
                            : confidenceScore > 0.4
                              ? "Review Required"
                              : "Low Confidence / Manual"}
                        </p>
                      </div>
                    </div>

                    <div className="space-y-2">
                      <label className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                        Validated Address
                      </label>
                      <div className="flex gap-2">
                        <textarea
                          className="flex-1 bg-black/40 border border-white/10 rounded-xl p-3 text-[10px] text-white/80 font-mono leading-relaxed outline-none focus:border-brand-primary resize-none h-20"
                          value={manualAddress}
                          onChange={(e) => setManualAddress(e.target.value)}
                          placeholder="Enter full address for geocoding..."
                        />
                        <button
                          onClick={async () => {
                            const result = await geocodeAddress(manualAddress);
                            if (result) {
                              const updated = {
                                ...extractedData,
                                address: result.full_address,
                                latitude: result.latitude,
                                longitude: result.longitude,
                                confidence: result.confidence,
                              };
                              setExtractedData(updated);
                              onGhostUpdate(updated);
                              setConfidenceScore(result.confidence);
                              setManualAddress(result.full_address);
                            } else {
                              alert(
                                "Geocoding failed. Please check the address or drag the pin manually.",
                              );
                            }
                          }}
                          disabled={isGeocoding}
                          className="w-12 h-20 bg-white/5 border border-white/10 rounded-xl flex items-center justify-center text-white/40 hover:text-brand-primary transition-colors disabled:opacity-50"
                        >
                          {isGeocoding ? (
                            <Loader2 size={16} className="animate-spin" />
                          ) : (
                            <MapPin size={16} />
                          )}
                        </button>
                      </div>
                    </div>
                  </div>

                  <div className="p-3 bg-brand-primary/10 rounded-xl border border-brand-primary/20">
                    <p className="text-[8px] font-mono text-brand-primary/60 uppercase tracking-widest mb-2">
                      Spatial Guidance
                    </p>
                    <p className="text-[10px] text-white/80 leading-relaxed italic">
                      &quot;Drag the ghost marker on the map to fine-tune the
                      signal&apos;s origin point. The radar will lock onto these
                      coordinates.&quot;
                    </p>
                  </div>
                </div>

                <button
                  onClick={() => setStep("EDIT_DETAILS")}
                  className="w-full h-14 bg-white text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] flex items-center justify-center gap-2 shadow-[0_20px_40px_rgba(255,255,255,0.1)]"
                >
                  Confirm Location <ChevronRight size={16} />
                </button>
              </div>
            )}

            {/* STEP 5: EDIT DETAILS */}
            {step === "EDIT_DETAILS" && extractedData && (
              <div className="space-y-6">
                <div className="flex gap-4">
                  <div className="relative w-24 h-32 rounded-2xl overflow-hidden bg-white/5 shrink-0 border border-white/10">
                    {extractedData.imageUrl ? (
                      <Image
                        src={extractedData.imageUrl}
                        alt="Flyer"
                        fill
                        className="object-cover"
                        referrerPolicy="no-referrer"
                      />
                    ) : (
                      <div className="w-full h-full flex items-center justify-center text-white/20">
                        <Upload size={24} />
                      </div>
                    )}
                  </div>
                  <div className="flex-1 space-y-4">
                    <div className="space-y-1">
                      <label className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                        Event Title
                      </label>
                      <input
                        className="w-full bg-transparent border-b border-white/10 text-sm font-black uppercase italic tracking-tighter text-white focus:border-brand-primary outline-none pb-1"
                        value={extractedData.title}
                        onChange={(e) =>
                          setExtractedData({
                            ...extractedData,
                            title: e.target.value,
                          })
                        }
                      />
                    </div>
                    <div className="space-y-1">
                      <label className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                        Venue
                      </label>
                      <input
                        className="w-full bg-transparent border-b border-white/10 text-xs font-bold uppercase text-white/80 focus:border-brand-primary outline-none pb-1"
                        value={extractedData.venueName}
                        onChange={(e) =>
                          setExtractedData({
                            ...extractedData,
                            venueName: e.target.value,
                          })
                        }
                      />
                    </div>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1">
                    <label className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                      Date
                    </label>
                    <input
                      type="date"
                      className="w-full bg-black/40 border border-white/10 rounded-xl p-2 text-[10px] text-white/80 font-mono outline-none focus:border-brand-primary"
                      value={extractedData.startTime?.split("T")[0]}
                      onChange={(e) => {
                        const time =
                          extractedData.startTime?.split("T")[1] || "20:00:00";
                        setExtractedData({
                          ...extractedData,
                          startTime: `${e.target.value}T${time}`,
                        });
                      }}
                    />
                  </div>
                  <div className="space-y-1">
                    <label className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                      Time
                    </label>
                    <input
                      type="time"
                      className="w-full bg-black/40 border border-white/10 rounded-xl p-2 text-[10px] text-white/80 font-mono outline-none focus:border-brand-primary"
                      value={extractedData.startTime
                        ?.split("T")[1]
                        ?.substring(0, 5)}
                      onChange={(e) => {
                        const date =
                          extractedData.startTime?.split("T")[0] ||
                          PHASE0_FIXED_NOW.split("T")[0];
                        setExtractedData({
                          ...extractedData,
                          startTime: `${date}T${e.target.value}:00`,
                        });
                      }}
                    />
                  </div>
                </div>

                <div className="space-y-1">
                  <label className="text-[8px] font-mono text-white/20 uppercase tracking-widest">
                    Category
                  </label>
                  <select
                    className="w-full bg-black/40 border border-white/10 rounded-xl p-3 text-[10px] text-white/80 font-mono outline-none focus:border-brand-primary appearance-none"
                    value={extractedData.category}
                    onChange={(e) =>
                      setExtractedData({
                        ...extractedData,
                        category: e.target
                          .value as import("@/domains/event/types").EventCategory,
                      })
                    }
                  >
                    <option value="nightlife">NIGHTLIFE</option>
                    <option value="concert">CONCERT</option>
                    <option value="lounge">LOUNGE</option>
                    <option value="restaurant">RESTAURANT</option>
                    <option value="private">PRIVATE</option>
                    <option value="tech">TECH</option>
                  </select>
                </div>

                <button
                  onClick={handlePublish}
                  className="w-full h-16 bg-brand-primary text-black rounded-2xl font-black uppercase tracking-[0.2em] text-[10px] flex items-center justify-center gap-2 shadow-[0_10px_30px_rgba(var(--brand-primary-rgb),0.3)]"
                >
                  {isPublishing ? (
                    <>
                      Publishing... <Loader2 size={16} className="ml-2" />
                    </>
                  ) : (
                    <>
                      Publish Signal <ArrowRight size={16} />
                    </>
                  )}
                </button>
              </div>
            )}

            {/* STEP 6: SUCCESS */}
            {step === "SUCCESS" && (
              <div className="py-12 flex flex-col items-center gap-6">
                <motion.div
                  initial={{ scale: 0 }}
                  animate={{ scale: 1 }}
                  className="w-20 h-20 rounded-full bg-brand-primary flex items-center justify-center text-black"
                >
                  <Check size={40} strokeWidth={3} />
                </motion.div>
                <div className="text-center space-y-2">
                  <p className="text-xl font-black uppercase italic tracking-tighter text-white">
                    Signal Live
                  </p>
                  <p className="text-[10px] font-mono text-white/40 uppercase tracking-[0.2em]">
                    New event added to radar
                  </p>
                </div>
              </div>
            )}
          </div>
        </motion.div>
      </div>
      <input
        ref={fileInputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        onChange={handleFlyerSelected}
        className="hidden"
      />
    </AnimatePresence>
  );
}

function ChoiceButton({
  icon,
  label,
  sub,
  onClick,
}: {
  icon: React.ReactNode;
  label: string;
  sub: string;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className="flex flex-col items-center justify-center gap-3 p-6 bg-white/5 border border-white/10 rounded-3xl hover:bg-white/10 hover:border-white/20 transition-all group"
    >
      <div className="w-12 h-12 rounded-2xl bg-white/5 flex items-center justify-center text-white/40 group-hover:text-brand-primary group-hover:scale-110 transition-all">
        {icon}
      </div>
      <div className="text-center">
        <p className="text-[10px] font-black uppercase text-white tracking-tight">
          {label}
        </p>
        <p className="text-[7px] font-mono text-white/20 uppercase tracking-widest mt-0.5">
          {sub}
        </p>
      </div>
    </button>
  );
}
