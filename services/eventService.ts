import { NightlifeItem } from '@/types';
import { MOCK_EVENTS } from '@/constants/mockData';
import { ingestionService } from './ingestionService';

export interface BoundingBox {
  minLat: number;
  maxLat: number;
  minLng: number;
  maxLng: number;
}

class EventService {
  private cache: Map<string, NightlifeItem[]> = new Map();
  private allEvents: NightlifeItem[] = [...MOCK_EVENTS];

  /**
   * Simulates fetching events from multiple sources (Eventbrite, Instagram, Venue Calendars)
   * and normalizes them into the NightlifeItem schema.
   */
  async fetchEventsInBounds(bounds: BoundingBox): Promise<NightlifeItem[]> {
    // Simulate multi-source ingestion pull
    if (Math.random() > 0.8) {
      await ingestionService.pullHoustonData();
    }

    // Simulate network latency
    await new Promise(resolve => setTimeout(resolve, 150));

    const cacheKey = `${bounds.minLat.toFixed(3)},${bounds.maxLat.toFixed(3)},${bounds.minLng.toFixed(3)},${bounds.maxLng.toFixed(3)}`;
    
    if (this.cache.has(cacheKey)) {
      return this.cache.get(cacheKey)!;
    }

    // Filter events within the bounding box
    const filtered = this.allEvents.filter(event => {
      const lat = event.latitude;
      const lng = event.longitude;
      return (
        lat >= bounds.minLat &&
        lat <= bounds.maxLat &&
        lng >= bounds.minLng &&
        lng <= bounds.maxLng
      );
    });

    // Simulate "dynamic" ingestion by adding a few random events if the area is sparse
    // This mimics "scraping" new data on the fly
    const dynamicEvents = this.generateDynamicEvents(bounds, filtered.length);
    
    // Deduplication logic: Ensure we don't add events with the same title at the same venue
    const combined = this.deduplicate([...filtered, ...dynamicEvents]);

    // Cache the result
    this.cache.set(cacheKey, combined);

    return combined;
  }

  private generateDynamicEvents(bounds: BoundingBox, existingCount: number): NightlifeItem[] {
    // Only generate if we have few events in this view to simulate discovery
    if (existingCount > 10) return [];

    const count = Math.floor(Math.random() * 3) + 1;
    const dynamic: NightlifeItem[] = [];

    const categories = ['tech', 'startup', 'creator', 'career'];
    const venues = ['THE_SIGNAL', 'KINETIC_LOUNGE', 'ECHO_CHAMBER', 'NEON_GARDEN', 'VELOCITY_BAR'];
    const neighborhoods = ['DOWNTOWN', 'NORTH_AUSTIN', 'UT_AREA', 'SOUTH_CONGRESS', 'EAST_AUSTIN'];

    for (let i = 0; i < count; i++) {
      const lat = bounds.minLat + Math.random() * (bounds.maxLat - bounds.minLat);
      const lng = bounds.minLng + Math.random() * (bounds.maxLng - bounds.minLng);
      const id = `dynamic-${Math.random().toString(36).substr(2, 9)}`;
      
      dynamic.push({
        id,
        title: `SIGNAL_DETECTED_${Math.floor(Math.random() * 1000)}`,
        description: 'Automatically ingested event from local venue signal.',
        venue_name: venues[Math.floor(Math.random() * venues.length)],
        address: 'HOUSTON_DYNAMIC_LOC',
        latitude: lat,
        longitude: lng,
        start_time: new Date().toISOString(),
        end_time: new Date(Date.now() + 4 * 3600000).toISOString(),
        category: categories[Math.floor(Math.random() * categories.length)] as any,
        price_tier: '$$',
        source: 'scraped',
        image_url: `https://picsum.photos/seed/${id}/800/1200`,
        neighborhood: neighborhoods[Math.floor(Math.random() * neighborhoods.length)],
        energyLevel: Math.floor(Math.random() * 10),
        tags: ['Dynamic', 'Live'],
        coordinates: { lat, lng }
      });
    }

    return dynamic;
  }

  private deduplicate(events: NightlifeItem[]): NightlifeItem[] {
    const seen = new Set<string>();
    return events.filter(event => {
      const key = `${event.title}-${event.venue_name}`.toLowerCase();
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  }

  async geocodeAddress(address: string): Promise<{ latitude: number, longitude: number, full_address: string, confidence: number } | null> {
    const token = process.env.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN;
    if (!address) return null;
    
    try {
      if (token && token.trim() !== '' && token !== 'undefined') {
        const response = await fetch(
          `https://api.mapbox.com/geocoding/v5/mapbox.places/${encodeURIComponent(address)}.json?access_token=${token}&limit=1&proximity=-95.3698,29.7604`
        );
        if (response.ok) {
          const data = await response.json();
          if (data.features && data.features.length > 0) {
            const feature = data.features[0];
            const [lng, lat] = feature.center;
            return { 
              latitude: lat, 
              longitude: lng,
              full_address: feature.place_name,
              confidence: feature.relevance || 0.8
            };
          }
        }
      }

      // OpenStreetMap Nominatim fallback
      const query = address.toLowerCase().includes('houston') ? address : `${address}, Houston, TX`;
      const osmResp = await fetch(
        `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=1`
      );
      if (osmResp.ok) {
        const osmData = await osmResp.json();
        if (osmData && osmData.length > 0) {
          return {
            latitude: parseFloat(osmData[0].lat),
            longitude: parseFloat(osmData[0].lon),
            full_address: osmData[0].display_name,
            confidence: 0.85
          };
        }
      }
      return null;
    } catch (error) {
      console.error("Geocoding failed:", error);
      return null;
    }
  }
}

export const eventService = new EventService();
