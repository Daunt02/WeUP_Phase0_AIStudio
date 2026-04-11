"use client";
import React from "react";
import { useAnalytics } from "../hooks/useAnalytics";
import { isFeatureEnabled } from "../lib/featureFlags";

export default function VerificationExamples() {
  const { track } = useAnalytics();

  const onSave = async () => {
    // Example: track a save action (signal_saved)
    await track({
      name: "signal_saved",
      props: { source: "verification_example" },
    });
    alert("Saved and tracked");
  };

  const onThrow = () => {
    try {
      throw new Error("Frontend test exception");
    } catch (err) {
      // Show how one might report frontend exception to analytics endpoint
      void track({
        name: "frontend_exception",
        props: { message: (err as Error).message } as any,
      });
      // Re-throw for visibility in dev
      console.error(err);
      alert("Exception thrown and reported to analytics seam");
    }
  };

  return (
    <div style={{ padding: 12, border: "1px solid #ddd" }}>
      <h4>Verification examples</h4>
      <button onClick={onSave}>Save (track event)</button>
      <button style={{ marginLeft: 8 }} onClick={onThrow}>
        Throw (report exception)
      </button>
      <div style={{ marginTop: 8 }}>
        Feature flags snapshot:
        <ul>
          <li>Flyer OCR: {String(isFeatureEnabled("flyerOcr"))}</li>
          <li>Link ingestion: {String(isFeatureEnabled("linkIngestion"))}</li>
          <li>
            Ingestion disabled: {String(isFeatureEnabled("ingestionDisabled"))}
          </li>
        </ul>
      </div>
    </div>
  );
}
