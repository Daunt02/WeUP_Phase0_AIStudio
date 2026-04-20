import manifest from "@/contracts/backend-contract-manifest.json";
import type {
  CalendarFeedResponse,
  EventDetailResponse,
  GeoBoundingBox,
  MapFeedQuery,
  MapFeedResponse,
  TimeWindow,
  CalendarFeedQuery,
} from "@/domains/query/contracts";
import type {
  EventCalendarProjection,
  EventDetailProjection,
  EventMapCardProjection,
} from "@/domains/event/projections";
import type { DraftSubmissionRequest } from "@/features/world/runtimeTypes";
import type {
  EventMapDensityControlDto,
  EventMapFeedClusterDto,
  EventMapFeedV1ResponseDto,
} from "@/domains/event/apiContracts";
import type {
  SavedEventDto,
  SavedEventsResponse,
  SaveEventResponse,
  SubmissionDto,
  SubmissionListResponse,
  SubmitForReviewResponse,
} from "@/services/backendContracts";

function expectKeys(contractName: string, sample: object) {
  expect(Object.keys(sample).sort()).toEqual(
    [
      ...manifest.contracts[contractName as keyof typeof manifest.contracts],
    ].sort(),
  );
}

describe("backend contract manifest", () => {
  const geoBoundingBox: GeoBoundingBox = {
    minLat: 37.7,
    maxLat: 37.85,
    minLng: -122.52,
    maxLng: -122.37,
  };

  const timeWindow: TimeWindow = {
    startUtc: "2026-04-11T00:00:00Z",
    endUtc: "2026-04-13T00:00:00Z",
    timezone: "America/Los_Angeles",
  };

  const mapCard: EventMapCardProjection = {
    id: "evt-sf-midnight-groove",
    title: "Midnight Groove Assembly",
    venueName: "Public Works",
    category: "nightlife",
    lat: 37.76809,
    lng: -122.42112,
    thumbnailUrl: "https://example.test/flyer.jpg",
    status: "PUBLISHED",
    confidence: 0.96,
  };

  const localityFilter = {
    marketCode: "houston",
    districtCode: "downtown",
    neighborhoodCode: "market-square",
  };

  const mapCluster = {
    clusterId: "29.75:-95.36",
    centerLat: 29.75,
    centerLng: -95.36,
    count: 3,
    eventIds: ["evt-sf-midnight-groove"],
  };

  const mapFeedCluster: EventMapFeedClusterDto = {
    clusterId: "37.768:-122.421",
    centerLat: 37.76809,
    centerLng: -122.42112,
    count: 3,
    eventIds: [
      "evt-sf-midnight-groove",
      "evt-sf-rooftop-signals",
      "evt-sf-after-hours-atelier",
    ],
    savedCount: 1,
  };

  const densityControl: EventMapDensityControlDto = {
    clusteringEnabled: true,
    activationVisibleEventCountThreshold: 24,
    activationMaxZoomInclusive: 13.5,
    clusterRadiusPixels: 56,
    clusterMaxZoomInclusive: 15,
    selectedMarkerBypassEnabled: true,
    expansionBehavior: "zoom_or_expand",
  };

  const calendarItem: EventCalendarProjection = {
    id: "evt-sf-midnight-groove",
    title: "Midnight Groove Assembly",
    venueName: "Public Works",
    category: "nightlife",
    startUtc: "2026-04-12T04:00:00Z",
    endUtc: "2026-04-12T08:00:00Z",
    timezone: "America/Los_Angeles",
    thumbnailUrl: "https://example.test/flyer.jpg",
    status: "PUBLISHED",
  };

  const detailProjection: EventDetailProjection = {
    id: "evt-sf-midnight-groove",
    title: "Midnight Groove Assembly",
    description: "Warehouse-scale house set.",
    venueName: "Public Works",
    address: "161 Erie St, San Francisco, CA 94103",
    lat: 37.76809,
    lng: -122.42112,
    category: "nightlife",
    startUtc: "2026-04-12T04:00:00Z",
    endUtc: "2026-04-12T08:00:00Z",
    timezone: "America/Los_Angeles",
    mediaRefs: [{ url: "https://example.test/flyer.jpg", kind: "poster" }],
    tags: ["House"],
    status: "PUBLISHED",
    confidence: 0.96,
    sourceKind: "manual_submission",
  };

  const detailDto = {
    ...detailProjection,
    version: 3,
    lastChangeType: "MaterialEventChange",
    concurrencyToken: "evt-sf-midnight-groove:v3:1712966400000",
  };

  it("matches the selected frontend API shapes", () => {
    const mapFeedRequest: MapFeedQuery = {
      bounds: geoBoundingBox,
      window: timeWindow,
      filters: {
        categories: ["nightlife"],
        districtCode: "mission",
        minConfidence: 0.9,
      },
      sort: "start_time_asc",
    };

    const calendarFeedRequest: CalendarFeedQuery = {
      window: timeWindow,
      filters: {
        categories: ["nightlife"],
        districtCode: "mission",
        minConfidence: 0.9,
      },
      sort: "start_time_asc",
      pagination: {
        page: 1,
        pageSize: 20,
      },
    };

    const mapFeedResponse: MapFeedResponse = {
      events: [mapCard],
      totalCount: 1,
      clusters: [mapCluster],
      queryMode: "bounding_box",
    };

    const mapFeedV1Response: EventMapFeedV1ResponseDto = {
      events: [
        {
          eventId: "evt-sf-midnight-groove",
          title: "Midnight Groove Assembly",
          startUtc: "2026-04-12T04:00:00Z",
          endUtc: "2026-04-12T08:00:00Z",
          latitude: 37.76809,
          longitude: -122.42112,
          venueName: "Public Works",
          district: "mission",
          primaryCategory: "nightlife",
          savedByCurrentUser: false,
          markerState: "default",
        },
      ],
      totalCount: 1,
      clusters: [mapFeedCluster],
      densityControl,
      clusterStrategy: "client_v1",
    };

    const calendarFeedResponse: CalendarFeedResponse = {
      items: [calendarItem],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
    };

    const eventDetailResponse: EventDetailResponse = {
      event: detailProjection,
    };

    const draftSubmissionRequest: DraftSubmissionRequest = {
      title: "Persistence Night",
      venueName: "Public Works",
      address: "161 Erie St, San Francisco, CA 94103",
      startUtc: "2026-04-13T04:00:00Z",
      endUtc: "2026-04-13T07:00:00Z",
      timezone: "America/Los_Angeles",
      category: "nightlife",
      description: "EF-backed submission smoke test",
      tags: ["Late Night"],
      flyerAssetIds: ["asset-1"],
    };

    const savedEvent: SavedEventDto = {
      eventId: "evt-sf-midnight-groove",
      title: "Midnight Groove Assembly",
      venueName: "Public Works",
      startUtc: "2026-04-12T04:00:00Z",
      thumbnailUrl: "https://example.test/flyer.jpg",
      savedAt: "2026-04-10T22:15:00Z",
    };

    const savedEventsResponse: SavedEventsResponse = {
      items: [savedEvent],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
    };

    const saveEventResponse: SaveEventResponse = {
      eventId: "evt-sf-midnight-groove",
      saved: true,
      message: "Saved",
    };

    const submissionDto: SubmissionDto = {
      submissionId: "submission-1",
      submittedByUserId: "user-sf-camille",
      status: "Draft",
      title: "Persistence Night",
      venueName: "Public Works",
      address: "161 Erie St, San Francisco, CA 94103",
      startUtc: "2026-04-13T04:00:00Z",
      endUtc: "2026-04-13T07:00:00Z",
      timezone: "America/Los_Angeles",
      category: "nightlife",
      description: "EF-backed submission smoke test",
      tags: ["Late Night"],
      flyerAssetIds: ["asset-1"],
      reviewNote: null,
      createdAt: "2026-04-11T18:00:00Z",
      updatedAt: "2026-04-11T18:05:00Z",
    };

    const submissionListResponse: SubmissionListResponse = {
      items: [submissionDto],
      totalCount: 1,
    };

    const submitForReviewResponse: SubmitForReviewResponse = {
      submissionId: "submission-1",
      status: "SubmittedForReview",
      message: "Submitted for review.",
    };

    expectKeys("GeoBoundingBox", geoBoundingBox);
    expectKeys("TimeWindowRequest", timeWindow);
    expectKeys("MapFeedRequest", {
      bounds: mapFeedRequest.bounds,
      categories: mapFeedRequest.filters?.categories,
      districtCode: mapFeedRequest.filters?.districtCode,
      locality: localityFilter,
      minConfidence: mapFeedRequest.filters?.minConfidence ?? 0,
      sort: mapFeedRequest.sort,
      window: mapFeedRequest.window,
    });
    expectKeys("EventMapCardDto", mapCard);
    expectKeys("LocalityFilterRequest", localityFilter);
    expectKeys("EventMapClusterDto", mapCluster);
    expectKeys("EventMapFeedClusterDto", mapFeedCluster);
    expectKeys("EventMapDensityControlDto", densityControl);
    expectKeys("MapFeedResponse", mapFeedResponse);
    expectKeys("EventMapFeedV1ResponseDto", mapFeedV1Response);
    expectKeys("CalendarFeedRequest", {
      categories: calendarFeedRequest.filters?.categories,
      districtCode: calendarFeedRequest.filters?.districtCode,
      locality: localityFilter,
      minConfidence: calendarFeedRequest.filters?.minConfidence ?? 0,
      page: calendarFeedRequest.pagination?.page,
      pageSize: calendarFeedRequest.pagination?.pageSize,
      sort: calendarFeedRequest.sort,
      window: calendarFeedRequest.window,
    });
    expectKeys("EventCalendarDto", calendarItem);
    expectKeys("CalendarFeedResponse", calendarFeedResponse);
    expectKeys("MediaRefDto", detailProjection.mediaRefs[0]);
    expectKeys("EventDetailDto", detailDto);
    expectKeys("EventDetailResponse", eventDetailResponse);
    expectKeys("DraftSubmissionRequest", draftSubmissionRequest);
    expectKeys("SavedEventDto", savedEvent);
    expectKeys("SavedEventsResponse", savedEventsResponse);
    expectKeys("SaveEventResponse", saveEventResponse);
    expectKeys("SubmissionDto", submissionDto);
    expectKeys("SubmissionListResponse", submissionListResponse);
    expectKeys("SubmitForReviewResponse", submitForReviewResponse);
  });
});
