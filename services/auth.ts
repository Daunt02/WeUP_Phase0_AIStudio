import { toApiUrl } from "@/services/apiBase";

export type SessionKind = "anonymous" | "authenticated";

export interface CurrentUserProfile {
  userId: string;
  email: string;
  displayName: string | null;
  homeMarket: string | null;
  onboardingState: string;
  createdAt: string;
  roles?: string[];
}

const DEV_TOKEN_KEY = "weup_dev_token";

export function getStoredToken(): string | null {
  if (typeof window === "undefined") return null;

  try {
    const token = localStorage.getItem(DEV_TOKEN_KEY)?.trim();
    return token ? token : null;
  } catch {
    return null;
  }
}

export function getAuthHeader(): Record<string, string> {
  const token = getStoredToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export function setDevToken(token: string) {
  if (typeof window === "undefined") return;
  try {
    localStorage.setItem(DEV_TOKEN_KEY, token);
  } catch {
    // Ignore local storage failures in client-only convenience path.
  }
}

export async function getCurrentUserProfile(): Promise<CurrentUserProfile | null> {
  const headers = getAuthHeader();
  if (!headers.Authorization) return null;

  const response = await fetch(toApiUrl("/auth/me"), { headers });
  if (response.status === 401) return null;
  if (!response.ok) {
    throw new Error(`Failed to resolve authenticated user: ${response.status}`);
  }

  return (await response.json()) as CurrentUserProfile;
}

export async function resolveSessionKind(): Promise<SessionKind> {
  const profile = await getCurrentUserProfile();
  return profile ? "authenticated" : "anonymous";
}
