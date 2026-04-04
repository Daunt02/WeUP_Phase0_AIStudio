/**
 * Media Service — P25
 * Upload and manage flyer assets
 */

import { getAuthHeader } from './auth';

export interface FlyerAssetDto {
  assetId: string;
  originalFilename: string;
  fileSizeBytes: number;
  contentType: string;
  submitterId: string;
  uploadedAt: string; // ISO 8601
  s3Url?: string;
  localPath?: string;
}

export interface UploadFlyerResponse {
  assetId: string;
}

export interface ListUserFlyersResponse {
  flyers: FlyerAssetDto[];
}

/**
 * Upload a flyer image (JPEG/PNG)
 * Returns asset ID for reference
 */
export async function uploadFlyer(
  file: File,
  submitterId: string,
  signal?: AbortSignal
): Promise<string> {
  // Validate file type
  if (!['image/jpeg', 'image/png'].includes(file.type)) {
    throw new Error('Only JPEG and PNG images are supported');
  }

  // Create form data
  const formData = new FormData();
  formData.append('file', file);

  const response = await fetch(`/api/media/flyers?submitterId=${encodeURIComponent(submitterId)}`, {
    method: 'POST',
    headers: getAuthHeader(),
    body: formData,
    signal,
  });

  if (!response.ok) {
    throw new Error(`Flyer upload failed: ${response.statusText}`);
  }

  const data: UploadFlyerResponse = await response.json();
  return data.assetId;
}

/**
 * Get flyer asset metadata by ID
 */
export async function getFlyerMetadata(
  assetId: string,
  signal?: AbortSignal
): Promise<FlyerAssetDto> {
  const response = await fetch(`/api/media/flyers/${encodeURIComponent(assetId)}`, {
    method: 'GET',
    headers: getAuthHeader(),
    signal,
  });

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error(`Flyer not found: ${assetId}`);
    }
    throw new Error(`Failed to fetch flyer metadata: ${response.statusText}`);
  }

  return response.json();
}

/**
 * List all flyers uploaded by a user
 */
export async function listUserFlyers(
  submitterId: string,
  signal?: AbortSignal
): Promise<FlyerAssetDto[]> {
  const response = await fetch(
    `/api/media/flyers/user/${encodeURIComponent(submitterId)}`,
    {
      method: 'GET',
      headers: getAuthHeader(),
      signal,
    }
  );

  if (!response.ok) {
    throw new Error(`Failed to list user flyers: ${response.statusText}`);
  }

  const data: ListUserFlyersResponse = await response.json();
  return data.flyers;
}

/**
 * Delete a flyer asset
 */
export async function deleteFlyer(
  assetId: string,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetch(`/api/media/flyers/${encodeURIComponent(assetId)}`, {
    method: 'DELETE',
    headers: getAuthHeader(),
    signal,
  });

  if (!response.ok) {
    throw new Error(`Failed to delete flyer: ${response.statusText}`);
  }
}

/**
 * Helper to format file size for display
 */
export function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
}

/**
 * Helper to get appropriate icon based on content type
 */
export function getMediaIcon(contentType: string): string {
  if (contentType.includes('image')) return '🖼️';
  if (contentType.includes('video')) return '🎥';
  if (contentType.includes('pdf')) return '📄';
  return '📎';
}
