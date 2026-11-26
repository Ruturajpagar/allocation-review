/** Shapes returned by the API (see AllocationReview.Api/Runs/RunContracts.cs). */

export interface CurrentUser {
  email: string;
  roles: string[];
  canReviewRuns: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface RunSummary {
  runId: string;
  /** ISO date, for example "2026-09-14". */
  runDate: string;
  totalAllocatedUnits: number;
}

export type GapStatus = 'filled' | 'partiallyFilled' | 'unfilled';

export type GapFilter = 'all' | 'exceptions';

export interface GapReviewRow {
  gapId: string;
  store: string;
  brand: string;
  productType: string;
  originalShortfall: number;
  unitsAllocated: number;
  unitsRemaining: number;
  status: GapStatus;
}

export interface RunReview {
  runId: string;
  runDate: string;
  totalAllocatedUnits: number;
  gapCount: number;
  statusCounts: { filled: number; partiallyFilled: number; unfilled: number };
  filter: GapFilter;
  gaps: GapReviewRow[];
}

/** RFC 9457 problem details, plus the correlation ID every API error carries. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

export const GAP_STATUS_LABELS: Record<GapStatus, string> = {
  filled: 'Filled',
  partiallyFilled: 'Partially filled',
  unfilled: 'Unfilled',
};
