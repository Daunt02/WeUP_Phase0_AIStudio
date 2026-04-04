/**
 * Seed Data Generator — Phase 0 / P24
 * Generates realistic test data for local development and QA.
 *
 * Usage:
 *   npx ts-node scripts/seed-data.ts          # 50 events (default)
 *   npx ts-node scripts/seed-data.ts 100      # custom count
 */

const BASE_URL = process.env.API_URL ?? 'http://localhost:5000';

const ANALYTICS_EVENT_TYPES = [
  'MapViewed',
  'EventSaved',
  'EventUnsaved',
  'EventSubmitted',
  'EventDetailViewed',
  'TemporalPresetSelected',
  'DistrictFilterApplied',
  'ErrorOccurred',
] as const;

const TEMPORAL_PRESETS = [
  'NOW',
  'Evening6PM',
  'Evening9PM',
  'Midnight',
  'EarlyMorning3AM',
  'Friday',
  'Saturday',
  'Sunday',
] as const;

async function post(path: string, body: unknown): Promise<Response> {
  return fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

async function seedAnalyticsEvents(count: number): Promise<void> {
  console.log(`Seeding ${count} analytics events...`);
  let seeded = 0;

  for (let i = 0; i < count; i++) {
    const eventType = ANALYTICS_EVENT_TYPES[i % ANALYTICS_EVENT_TYPES.length];
    const res = await post('/api/analytics/events', {
      eventType,
      userId: `seed-user-${(i % 10) + 1}`,
      properties: { source: 'seed-script', index: i },
    });

    if (res.ok || res.status === 202) {
      seeded++;
    } else {
      console.warn(`  Failed event ${i}: ${res.status} ${res.statusText}`);
    }
  }

  console.log(`  Analytics events seeded: ${seeded}/${count}`);
}

async function seedTemporalQueries(): Promise<void> {
  console.log('Seeding temporal queries...');
  let seeded = 0;

  for (const preset of TEMPORAL_PRESETS) {
    const res = await post('/api/temporal/events-at-time', {
      preset,
      marketTimezone: 'America/Los_Angeles',
    });

    if (res.ok) {
      seeded++;
    } else {
      console.warn(`  Failed preset ${preset}: ${res.status} ${res.statusText}`);
    }
  }

  console.log(`  Temporal queries seeded: ${seeded}/${TEMPORAL_PRESETS.length}`);
}

export async function seedDatabase(count = 50): Promise<void> {
  console.log(`\nStarting seed data generation (${count} events, target: ${BASE_URL})...\n`);

  try {
    await seedAnalyticsEvents(count);
    await seedTemporalQueries();
    console.log('\nSeed data generation complete.');
  } catch (err) {
    console.error('\nSeed failed:', err);
    process.exit(1);
  }
}

// CLI invocation
if (require.main === module) {
  const count = parseInt(process.argv[2] ?? '50', 10);
  if (isNaN(count) || count < 1) {
    console.error('Usage: npx ts-node scripts/seed-data.ts [count]');
    process.exit(1);
  }
  seedDatabase(count);
}
