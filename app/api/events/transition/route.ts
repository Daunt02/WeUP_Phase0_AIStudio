import { NextResponse } from "next/server";
import { tryTransitionEventStatus } from "@/domains/event/transitions";
import type { EventAggregate, EventStatus } from "@/domains/event/types";

export async function POST(req: Request) {
  try {
    const body = await req.json();
    const { event, to } = body as {
      event: Partial<EventAggregate>;
      to: EventStatus;
    };
    const result = tryTransitionEventStatus(event, to);
    if (!result.ok)
      return NextResponse.json(
        { ok: false, reason: result.reason },
        { status: 400 },
      );
    return NextResponse.json(
      { ok: true, event: result.event },
      { status: 200 },
    );
  } catch (err) {
    return NextResponse.json(
      { ok: false, reason: (err as Error).message },
      { status: 500 },
    );
  }
}
