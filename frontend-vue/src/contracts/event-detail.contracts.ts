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
