import { expect, test, type APIRequestContext } from "@playwright/test";

const backendBaseUrl = "http://127.0.0.1:5074";

async function loginSeededUser(request: APIRequestContext) {
  const response = await request.post(`${backendBaseUrl}/auth/login`, {
    data: { email: "camille+phase0@weup.test" },
  });

  expect(response.ok()).toBeTruthy();
  return response.json();
}

test.beforeEach(async ({ request }) => {
  const reset = await request.post(`${backendBaseUrl}/internal/seed/reset`);
  expect(reset.ok()).toBeTruthy();
});

test("critical Phase 0 release path stays healthy", async ({
  page,
  request,
}) => {
  const auth = await loginSeededUser(request);

  const mapFeed = await request.post(`${backendBaseUrl}/api/events/map`, {
    data: {
      bounds: { minLat: 37.7, maxLat: 37.85, minLng: -122.52, maxLng: -122.37 },
      window: {
        startUtc: "2026-04-11T00:00:00Z",
        endUtc: "2026-04-13T00:00:00Z",
        timezone: "America/Los_Angeles",
      },
    },
  });
  expect(mapFeed.ok()).toBeTruthy();
  expect(await mapFeed.text()).toContain("evt-sf-midnight-groove");

  await page.goto("/");
  await page.evaluate((token) => {
    window.localStorage.setItem("weup_dev_token", token);
  }, auth.token as string);

  await page.goto("/events/evt-sf-midnight-groove");
  await expect(page.getByText("Midnight Groove Assembly")).toBeVisible();
  await expect(page.getByTestId("event-session-state")).toContainText(
    "Camille Phase0",
  );

  const saveButton = page.getByTestId("event-save-button");
  await expect(saveButton).toContainText("SAVE_SIGNAL");
  await saveButton.click();
  await expect(saveButton).toContainText("UNSAVE_SIGNAL");
  await saveButton.click();
  await expect(saveButton).toContainText("SAVE_SIGNAL");

  const me = await request.get(`${backendBaseUrl}/auth/me`, {
    headers: { Authorization: `Bearer ${auth.token}` },
  });
  expect(me.ok()).toBeTruthy();

  const draft = await request.post(`${backendBaseUrl}/api/events/submissions`, {
    headers: { Authorization: `Bearer ${auth.token}` },
    data: {
      title: "Phase 0 Submission Draft",
      venueName: "Public Works",
      address: "161 Erie St, San Francisco, CA 94103",
      startUtc: "2026-04-13T03:30:00Z",
      endUtc: "2026-04-13T07:00:00Z",
      timezone: "America/Los_Angeles",
      category: "nightlife",
      description: "Submission created during Playwright release validation.",
      tags: ["Release", "Critical Path"],
      flyerAssetIds: [],
    },
  });
  expect(draft.status()).toBe(201);
  const createdDraft = await draft.json();

  const submit = await request.post(
    `${backendBaseUrl}/api/events/submissions/${createdDraft.submissionId}/submit`,
    {
      headers: { Authorization: `Bearer ${auth.token}` },
    },
  );
  expect(submit.status()).toBe(202);

  const moderationQueue = await request.get(
    `${backendBaseUrl}/api/moderation/queue?pageSize=5`,
  );
  expect(moderationQueue.ok()).toBeTruthy();
  const moderationBody = await moderationQueue.text();
  expect(moderationBody).toContain("mod-sf-neon-market");

  const approve = await request.post(
    `${backendBaseUrl}/api/moderation/queue/mod-sf-neon-market/approve`,
    {
      data: {
        actorId: "user-sf-moderator",
        note: "Resolved in release validation",
        publishedEventId: "evt-sf-midnight-groove",
      },
    },
  );
  expect(approve.ok()).toBeTruthy();
});
