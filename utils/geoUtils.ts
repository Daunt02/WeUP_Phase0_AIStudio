/**
 * Calculates the Haversine distance between two points in kilometers.
 */
export function getDistance(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371; // Radius of the earth in km
  const dLat = deg2rad(lat2 - lat1);
  const dLon = deg2rad(lon2 - lon1);
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(deg2rad(lat1)) * Math.cos(deg2rad(lat2)) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  const d = R * c; // Distance in km
  return d;
}

function deg2rad(deg: number): number {
  return deg * (Math.PI / 180);
}

/**
 * Recommends similar nearby events based on distance and category.
 */
export function getNearbyRecommendations(
  currentEvent: any,
  allEvents: any[],
  limit: number = 3,
  maxRadiusKm: number = 10
) {
  const currentLat = currentEvent.latitude || currentEvent.coordinates?.lat;
  const currentLng = currentEvent.longitude || currentEvent.coordinates?.lng;

  if (!currentLat || !currentLng) return [];

  return allEvents
    .filter(event => event.id !== currentEvent.id)
    .map(event => {
      const lat = event.latitude || event.coordinates?.lat;
      const lng = event.longitude || event.coordinates?.lng;
      const distance = getDistance(currentLat, currentLng, lat, lng);
      
      // Relevance score: 
      // - Lower distance is better
      // - Same category gives a boost
      // - Closer start time gives a boost
      let score = distance;
      if (event.category === currentEvent.category) score -= 2; // Category boost
      
      const timeDiff = Math.abs(new Date(event.start_time).getTime() - new Date(currentEvent.start_time).getTime());
      const hoursDiff = timeDiff / (1000 * 60 * 60);
      score += hoursDiff * 0.5; // Time penalty

      return { ...event, distance, score };
    })
    .filter(event => event.distance <= maxRadiusKm)
    .sort((a, b) => a.score - b.score)
    .slice(0, limit);
}
