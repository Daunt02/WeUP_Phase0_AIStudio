/**
 * Minimal frontend helper to generate/propagate correlation IDs for API calls.
 * Stores a per-tab id in sessionStorage and forwards via X-Correlation-ID header.
 */

function generateId() {
  return (
    crypto.randomUUID?.() ??
    `${Date.now()}-${Math.floor(Math.random() * 10000)}`
  );
}

export function getCorrelationId(): string {
  let id = sessionStorage.getItem("weup.correlation");
  if (!id) {
    id = generateId();
    sessionStorage.setItem("weup.correlation", id);
  }
  return id;
}

export async function fetchWithCorrelation(
  input: RequestInfo,
  init?: RequestInit,
) {
  const headers = new Headers((init?.headers as HeadersInit) ?? {});
  if (!headers.has("X-Correlation-ID"))
    headers.set("X-Correlation-ID", getCorrelationId());

  const merged: RequestInit = { ...init, headers };
  const res = await fetch(input, merged);
  const serverCorr = res.headers.get("X-Correlation-ID");
  if (serverCorr) sessionStorage.setItem("weup.correlation", serverCorr);
  return res;
}
