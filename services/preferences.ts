import { toApiUrl } from "@/services/apiBase";
import { getAuthHeader } from "@/services/auth";
import type {
  UpdatePreferencesRequest,
  UserPreferencesDto,
} from "@/services/backendContracts";

export async function getMyPreferences(): Promise<UserPreferencesDto | null> {
  const headers = getAuthHeader();
  if (!headers.Authorization) return null;

  const response = await fetch(toApiUrl("/api/users/me/preferences"), {
    headers,
  });

  if (response.status === 401) return null;
  if (!response.ok) {
    throw new Error(`Failed to fetch preferences: ${response.status}`);
  }

  return (await response.json()) as UserPreferencesDto;
}

export async function patchMyPreferences(
  request: UpdatePreferencesRequest,
): Promise<UserPreferencesDto | null> {
  const headers = getAuthHeader();
  if (!headers.Authorization) return null;

  const response = await fetch(toApiUrl("/api/users/me/preferences"), {
    method: "PATCH",
    headers: {
      ...headers,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  if (response.status === 401) return null;
  if (!response.ok) {
    throw new Error(`Failed to update preferences: ${response.status}`);
  }

  return (await response.json()) as UserPreferencesDto;
}
