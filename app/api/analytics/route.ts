import { NextRequest, NextResponse } from "next/server";

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const correlationId =
      req.headers.get("x-correlation-id") ?? crypto.randomUUID();

    // Phase 0: forward to backend if configured, otherwise log to console
    const endpoint = process.env.NEXT_PUBLIC_BACKEND_ANALYTICS_ENDPOINT;
    if (endpoint) {
      await fetch(endpoint, {
        method: "POST",
        headers: {
          "content-type": "application/json",
          "x-correlation-id": correlationId,
        },
        body: JSON.stringify(body),
      });
    } else {
      console.log("Analytics event (local):", body);
    }

    return NextResponse.json(
      { accepted: true, correlationId },
      {
        status: 202,
        headers: { "x-correlation-id": correlationId },
      },
    );
  } catch (err) {
    console.warn("Analytics endpoint error", err);
    return NextResponse.json({ accepted: false }, { status: 500 });
  }
}
