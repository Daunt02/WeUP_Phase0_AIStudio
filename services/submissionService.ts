import { getAuthHeader } from "./auth";
import { toApiUrl } from "./apiBase";

const BASE = toApiUrl("/api/events/submissions");

async function checkResponse(res: Response) {
  const text = await res.text();
  let json: any = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch (e) {}
  if (!res.ok) {
    const msg = json?.message || res.statusText || "submission service error";
    throw new Error(msg);
  }
  return json;
}

export async function createDraft(draft: any) {
  const res = await fetch(BASE, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...getAuthHeader() },
    body: JSON.stringify(draft),
  });
  return checkResponse(res);
}

export async function updateDraft(id: string, patch: any) {
  const res = await fetch(`${BASE}/${encodeURIComponent(id)}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json", ...getAuthHeader() },
    body: JSON.stringify(patch),
  });
  return checkResponse(res);
}

export async function submitForReview(id: string) {
  const res = await fetch(`${BASE}/${encodeURIComponent(id)}/submit`, {
    method: "POST",
    headers: { ...getAuthHeader() },
  });
  return checkResponse(res);
}

export async function getSubmission(id: string) {
  const res = await fetch(`${BASE}/${encodeURIComponent(id)}`, {
    headers: { ...getAuthHeader() },
  });
  return checkResponse(res);
}

export async function listSubmissions() {
  const res = await fetch(BASE, { headers: { ...getAuthHeader() } });
  return checkResponse(res);
}

const submissionService = {
  createDraft,
  updateDraft,
  submitForReview,
  getSubmission,
  listSubmissions,
};

export default submissionService;
