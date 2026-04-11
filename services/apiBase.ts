import { publicEnv } from "@/lib/env/public";

const rawBase = publicEnv.NEXT_PUBLIC_API_BASE_URL?.trim() ?? "/";

export const API_BASE_URL = rawBase === "/" ? "" : rawBase.replace(/\/$/, "");

export function toApiUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  return API_BASE_URL ? `${API_BASE_URL}${normalizedPath}` : normalizedPath;
}
