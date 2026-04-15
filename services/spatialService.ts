import { getAuthHeader } from "./auth";
import { publicEnv } from "@/lib/env/public";

const API_BASE = publicEnv.NEXT_PUBLIC_API_BASE_URL;

export interface BoundingBoxDto {
  minLat: number;
  maxLat: number;
  minLng: number;
  maxLng: number;
}

export interface TimeWindowRequest {
  startUtc: string; // ISO 8601
  endUtc: string; // ISO 8601
  timezone: string; // IANA timezone
}

export interface LocalityFilterRequest {
  marketCode?: string;
  districtCode?: string;
  neighborhoodCode?: string;
}

export interface MapFeedRequestDto {
  bounds: BoundingBoxDto;
  window: TimeWindowRequest;
  categories?: string[];
  locality?: LocalityFilterRequest;
  districtCode?: string;
  minConfidence?: number;
  sort?: string;
}

export interface EventMapCardDto {
  id: string;
  title: string;
  venueName: string;
  category: string;
  lat: number;
  lng: number;
  thumbnailUrl?: string;
  status: string;
  confidence: number;
}

export interface EventMapClusterDto {
  clusterId: string;
  centerLat: number;
  centerLng: number;
  count: number;
  eventIds: string[];
}

export interface MapFeedResponseDto {
  events: EventMapCardDto[];
  totalCount: number;
  clusters?: EventMapClusterDto[];
  queryMode?: "bounding_box" | "cluster_aggregation";
}

export interface DistrictDto {
  code: string;
  displayName: string;
  description: string;
  marketCode: string;
  sortOrder: number;
  parentCode?: string;
}

export interface DistrictListResponseDto {
  districts: DistrictDto[];
  totalCount: number;
}

export interface DetermineDistrictRequestDto {
  latitude: number;
  longitude: number;
  marketCode: string;
}

export interface DetermineDistrictResponseDto {
  districtCode: string | null;
}

/**
 * Spatial query service for map viewport, bounding box, and district operations.
 * P19: Bounding Box, Cluster, and District Query Semantics
 */
export const spatialService = {
  /**
   * Query events within a bounding box.
   */
  async getMapFeed(request: MapFeedRequestDto): Promise<MapFeedResponseDto> {
    const res = await fetch(`${API_BASE}/api/spatial/map-feed`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...getAuthHeader(),
      },
      body: JSON.stringify(request),
    });

    if (!res.ok) {
      const text = await res.text();
      throw new Error(`Map feed error: ${res.statusText} — ${text}`);
    }

    return res.json();
  },

  /**
   * Get all districts for a market.
   */
  async getDistrictsForMarket(
    marketCode: string,
  ): Promise<DistrictListResponseDto> {
    const res = await fetch(
      `${API_BASE}/api/spatial/districts/${encodeURIComponent(marketCode)}`,
      {
        headers: getAuthHeader(),
      },
    );

    if (!res.ok) {
      throw new Error(`Get districts error: ${res.statusText}`);
    }

    return res.json();
  },

  /**
   * Get metadata for a specific district.
   */
  async getDistrict(districtCode: string): Promise<DistrictDto | null> {
    const res = await fetch(
      `${API_BASE}/api/spatial/district/${encodeURIComponent(districtCode)}`,
      { headers: getAuthHeader() },
    );

    if (res.status === 404) return null;
    if (!res.ok) throw new Error(`Get district error: ${res.statusText}`);

    const data = await res.json();
    return data.district || null;
  },

  /**
   * Determine which district a coordinate belongs to.
   */
  async determineDistrict(
    request: DetermineDistrictRequestDto,
  ): Promise<string | null> {
    const res = await fetch(`${API_BASE}/api/spatial/determine-district`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...getAuthHeader(),
      },
      body: JSON.stringify(request),
    });

    if (!res.ok) {
      throw new Error(`Determine district error: ${res.statusText}`);
    }

    const data = await res.json();
    return data.districtCode || null;
  },
};

export default spatialService;
