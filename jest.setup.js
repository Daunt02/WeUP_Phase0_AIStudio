// jest.setup.js
import '@testing-library/jest-dom'

// Mock fetch if needed
global.fetch = jest.fn()

// lib/env/public.ts intentionally throws when NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN
// is missing (fail-fast for app runtime). Tests that import the env-validated
// module chain need a dummy value; this does not weaken the runtime check.
if (!process.env.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN) {
  process.env.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN = "test-token";
}
