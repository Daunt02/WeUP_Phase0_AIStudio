import { getAuthHeader } from "./auth";
import { toApiUrl } from "./apiBase";

export type MediaAssetType =
  | "FlyerImage"
  | "VenueImage"
  | "PromotionalPoster"
  | "EventMedia";

export type MediaOwnerType = "User" | "Venue" | "SystemWorkflow";

export interface CreateMediaUploadParams {
  assetType: MediaAssetType;
  ownerType?: MediaOwnerType;
  ownerId?: string;
  venueId?: string;
  uploaderUserId?: string;
  submissionId?: string;
  ingestionWorkflowSource?: string;
  metadataJson?: string;
}

export interface CreateMediaUploadResponse {
  uploadId: string;
  assetId: string;
  status: string;
}

export interface CompleteMediaUploadRequest {
  processingSucceeded?: boolean;
  queueForReview?: boolean;
  failureReason?: string;
  metadataJson?: string;
}

export interface MediaUploadDto {
  uploadId: string;
  assetId: string;
  status: string;
  initializedAt: string;
  completedAt?: string;
  requestedByUserId?: string;
  failureReason?: string;
}

export interface MediaOwnerRefDto {
  ownerType: string;
  ownerId?: string;
  venueId?: string;
  ingestionWorkflowSource?: string;
}

export interface MediaStorageRefDto {
  provider: string;
  container: string;
  objectKey: string;
  uri?: string;
  eTag?: string;
  versionId?: string;
}

export interface MediaAssetDto {
  assetId: string;
  assetType: string;
  status: string;
  contentType: string;
  fileSizeBytes: number;
  checksumSha256: string;
  originalFilename: string;
  uploadedAt: string;
  uploaderUserId?: string;
  owner: MediaOwnerRefDto;
  storage: MediaStorageRefDto;
  submissionId?: string;
  metadataJson?: string;
}

export async function createMediaUpload(
  file: File,
  params: CreateMediaUploadParams,
  signal?: AbortSignal,
): Promise<CreateMediaUploadResponse> {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("assetType", params.assetType);
  formData.append("ownerType", params.ownerType ?? "User");

  if (params.ownerId) formData.append("ownerId", params.ownerId);
  if (params.venueId) formData.append("venueId", params.venueId);
  if (params.uploaderUserId)
    formData.append("uploaderUserId", params.uploaderUserId);
  if (params.submissionId) formData.append("submissionId", params.submissionId);
  if (params.ingestionWorkflowSource) {
    formData.append("ingestionWorkflowSource", params.ingestionWorkflowSource);
  }
  if (params.metadataJson) formData.append("metadataJson", params.metadataJson);

  const response = await fetch(toApiUrl("/api/media/uploads"), {
    method: "POST",
    headers: {
      ...getAuthHeader(),
    },
    body: formData,
    signal,
  });

  await throwOnError(response, "Media upload failed");
  return response.json();
}

export async function completeMediaUpload(
  uploadId: string,
  payload: CompleteMediaUploadRequest,
  signal?: AbortSignal,
): Promise<MediaUploadDto> {
  const response = await fetch(
    toApiUrl(`/api/media/uploads/${encodeURIComponent(uploadId)}/complete`),
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...getAuthHeader(),
      },
      body: JSON.stringify(payload),
      signal,
    },
  );

  await throwOnError(response, "Failed to complete media upload");
  return response.json();
}

export async function getMediaUpload(
  uploadId: string,
  signal?: AbortSignal,
): Promise<MediaUploadDto> {
  const response = await fetch(
    toApiUrl(`/api/media/uploads/${encodeURIComponent(uploadId)}`),
    {
      method: "GET",
      headers: getAuthHeader(),
      signal,
    },
  );

  await throwOnError(response, "Failed to fetch media upload");
  return response.json();
}

export async function getMediaAsset(
  assetId: string,
  signal?: AbortSignal,
): Promise<MediaAssetDto> {
  const response = await fetch(
    toApiUrl(`/api/media/assets/${encodeURIComponent(assetId)}`),
    {
      method: "GET",
      headers: getAuthHeader(),
      signal,
    },
  );

  await throwOnError(response, "Failed to fetch media asset");
  return response.json();
}

// Compatibility wrapper used by existing flyer UI.
export async function uploadFlyer(
  file: File,
  uploaderUserId?: string,
  signal?: AbortSignal,
): Promise<string> {
  const created = await createMediaUpload(
    file,
    {
      assetType: "FlyerImage",
      ownerType: "User",
      uploaderUserId,
      ownerId: uploaderUserId,
    },
    signal,
  );

  await completeMediaUpload(
    created.uploadId,
    {
      processingSucceeded: true,
      queueForReview: true,
    },
    signal,
  );

  return created.assetId;
}

async function throwOnError(
  response: Response,
  fallbackMessage: string,
): Promise<void> {
  if (response.ok) {
    return;
  }

  const contentType = response.headers.get("content-type") ?? "";
  if (
    contentType.includes("application/problem+json") ||
    contentType.includes("application/json")
  ) {
    const payload = await response.json().catch(() => null as any);
    const detail = payload?.detail || payload?.title || payload?.error;
    throw new Error(detail || fallbackMessage);
  }

  throw new Error(fallbackMessage);
}
