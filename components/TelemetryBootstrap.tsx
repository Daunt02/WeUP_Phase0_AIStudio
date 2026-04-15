"use client";

import { useEffect } from "react";
import { installClientTelemetry } from "@/lib/telemetry/clientTelemetry";

export default function TelemetryBootstrap() {
  useEffect(() => {
    installClientTelemetry();
  }, []);

  return null;
}
