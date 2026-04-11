import { NextRequest, NextResponse } from "next/server";

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();

    // Phase 0: forward to backend if configured, otherwise log to console
    const endpoint = process.env.NEXT_PUBLIC_BACKEND_ANALYTICS_ENDPOINT;
    if (endpoint) {
      await fetch(endpoint, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
      });
    } else {
      // eslint-disable-next-line no-console
      console.log("Analytics event (local):", body);
    }

    return NextResponse.json({ accepted: true }, { status: 202 });
  } catch (err) {
    // eslint-disable-next-line no-console
    console.warn("Analytics endpoint error", err);
    return NextResponse.json({ accepted: false }, { status: 500 });
  }
}
