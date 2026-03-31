# Frontend → Real API Migration Guide

## Current state
`services/eventService.ts` returns mock data from `constants/mockData.ts`.

## Migration steps

### 1. Map feed
Replace `eventService.fetchEventsInBounds(bounds)` with:

```ts
const response = await fetch('/api/events/map', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    bounds: { minLat, maxLat, minLng, maxLng },
    window: { startUtc, endUtc, timezone },
    sort: 'start_time_asc',
  }),
});
const { events } = await response.json(); // EventMapCardProjection[]
```

### 2. Calendar feed
Replace temporal-filtered event arrays with:

```ts
const response = await fetch('/api/events/calendar', {
  method: 'POST',
  body: JSON.stringify({ window: { startUtc, endUtc, timezone }, page: 1, pageSize: 50 }),
});
const { items } = await response.json(); // EventCalendarProjection[]
```

### 3. Event detail
Replace local array lookup with:

```ts
const response = await fetch(`/api/events/${eventId}`);
const { event } = await response.json(); // EventDetailProjection
```

### 4. Save / unsave
Replace `savedEventIds` state with:

```ts
// Save
await fetch(`/api/users/me/saves/${eventId}`, { method: 'POST' });
// Unsave
await fetch(`/api/users/me/saves/${eventId}`, { method: 'DELETE' });
// List
const { items } = await (await fetch('/api/users/me/saves')).json();
```

## Approach
- Keep `eventService.ts` as the single point of contact — swap internals to `fetch` without changing component call sites.
- The `@deprecated fetchEventsInBounds()` method on `EventService` is the seam to replace first.
- Add a base URL config to `lib/env/public.ts` (`NEXT_PUBLIC_API_URL`) so local dev points to `http://localhost:5000`.
