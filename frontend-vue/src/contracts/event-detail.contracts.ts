export interface MediaRefDto {
  readonly url: string;
  readonly kind: string;
}

export interface EventDetailProvenanceSummaryDto {
  readonly primarySourceKind: string;
  readonly sourceCount: number;
  readonly firstObservedAtUtc: string;
  readonly lastObservedAtUtc: string;
  readonly summaryLabel: string;
}

/** Backend DTO: WeUP.Contracts.Events.EventDetailDto */
export interface EventDetailDto {
  readonly id: string;
  readonly title: string;
  readonly description: string | null;
  readonly venueName: string;
  readonly address: string;
  readonly lat: number;
  readonly lng: number;
  readonly category: string;
  readonly categories: string[];
  readonly startUtc: string;
  readonly endUtc: string | null;
  readonly timezone: string;
  readonly flyerImageUrl: string | null;
  readonly mediaRefs: MediaRefDto[];
  readonly tags: string[];
  readonly status: string;
  readonly confidence: number;
  readonly sourceKind: string;
  readonly provenanceSummary: EventDetailProvenanceSummaryDto;
  readonly savedByCurrentUser: boolean;
  readonly version: number;
  readonly lastChangeType: string | null;
  readonly concurrencyToken: string | null;
}

export interface EventDetailResponse {
  readonly event: EventDetailDto | null;
}

/** Backend DTO: WeUP.Contracts.Saves.SaveEventRequestDto */
export interface SaveEventRequestDto {
  readonly eventId: string;
}

/** Backend DTO: WeUP.Contracts.Saves.UnsaveEventRequestDto */
export interface UnsaveEventRequestDto {
  readonly eventId: string;
}

/** Backend DTO: WeUP.Contracts.Saves.SaveEventResponseDto */
export interface SaveEventResponseDto {
  readonly eventId: string;
  readonly saved: boolean;
  readonly message: string;
}

export type SaveSessionKind = "authenticated" | "anonymous";
export type SavePersistenceSource = "backend" | "anonymous-local";

/** Backend DTO: WeUP.Contracts.Saves.SavedStateDto */
export interface SavedStateDto {
  readonly eventId: string;
  readonly saved: boolean;
  readonly sessionKind: SaveSessionKind;
  readonly persistenceSource: SavePersistenceSource;
}

export type SaveStateMigrationItemOutcome =
  | "already-saved"
  | "migrated"
  | "invalid-local-event-id"
  | "missing-local-event-id";

export interface SaveStateLocalTemporalFilterDto {
  readonly preset: string | null;
  readonly timezone: string | null;
  readonly customStartUtc: string | null;
  readonly customEndUtc: string | null;
}

export interface SaveStateLocalMapViewportDto {
  readonly bbox: string | null;
  readonly capturedAtUtc: string | null;
}

export interface SaveStateLocalDiscoveryContextDto {
  readonly lastViewedDistrict: string | null;
  readonly lastTemporalFilter: SaveStateLocalTemporalFilterDto | null;
  readonly recentMapViewport: SaveStateLocalMapViewportDto | null;
  readonly updatedAtUtc: string | null;
}

export interface SaveStateMigrationRequestDto {
  readonly localSavedEventIds: string[];
  readonly localDiscoveryContext: SaveStateLocalDiscoveryContextDto | null;
  readonly clientMigrationKey: string | null;
}

export interface SaveStateMigrationCountsDto {
  readonly receivedLocalSavedCount: number;
  readonly distinctLocalSavedCount: number;
  readonly duplicateCollapsedCount: number;
  readonly migratedCount: number;
  readonly alreadySavedCount: number;
  readonly invalidLocalIdCount: number;
  readonly missingLocalIdCount: number;
}

export interface SaveStateMigrationItemResultDto {
  readonly eventId: string;
  readonly outcome: SaveStateMigrationItemOutcome;
  readonly message: string;
}

export interface SaveStateDiscoveryContextMigrationResultDto {
  readonly status: string;
  readonly applied: boolean;
  readonly message: string;
  readonly resolvedPreferredTimezone: string | null;
}

export interface SaveStateMigrationResultDto {
  readonly status: "completed" | "completed-with-issues";
  readonly migrationDirection: "anonymous-to-authenticated";
  readonly ownership: "authenticated-user";
  readonly clientMigrationKey: string | null;
  readonly processedAtUtc: string;
  readonly counts: SaveStateMigrationCountsDto;
  readonly itemResults: SaveStateMigrationItemResultDto[];
  readonly retainedLocalSavedEventIds: string[];
  readonly discoveryContext: SaveStateDiscoveryContextMigrationResultDto;
}
