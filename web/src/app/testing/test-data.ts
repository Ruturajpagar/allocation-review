import { CurrentUser, PagedResult, RunReview, RunSummary } from '../core/models';

export const ADMIN: CurrentUser = { email: 'admin@demo.local', roles: ['Admin'], canReviewRuns: true };
export const VIEWER: CurrentUser = { email: 'viewer@demo.local', roles: ['Viewer'], canReviewRuns: false };

export const HISTORY: PagedResult<RunSummary> = {
  items: [
    { runId: 'R-102', runDate: '2026-09-14', totalAllocatedUnits: 5 },
    { runId: 'R-101', runDate: '2026-09-13', totalAllocatedUnits: 1 },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 2,
  totalPages: 1,
};

/** The brief's "Expected review for the latest run". */
export const R102_ALL: RunReview = {
  runId: 'R-102',
  runDate: '2026-09-14',
  totalAllocatedUnits: 5,
  gapCount: 3,
  statusCounts: { filled: 1, partiallyFilled: 1, unfilled: 1 },
  filter: 'all',
  gaps: [
    { gapId: 'G-01', store: 'Store A', brand: 'Brand X', productType: 'Frames', originalShortfall: 5, unitsAllocated: 3, unitsRemaining: 2, status: 'partiallyFilled' },
    { gapId: 'G-02', store: 'Store B', brand: 'Brand X', productType: 'Frames', originalShortfall: 2, unitsAllocated: 2, unitsRemaining: 0, status: 'filled' },
    { gapId: 'G-03', store: 'Store C', brand: 'Brand Y', productType: 'Sunglasses', originalShortfall: 4, unitsAllocated: 0, unitsRemaining: 4, status: 'unfilled' },
  ],
};

export const R102_EXCEPTIONS: RunReview = {
  ...R102_ALL,
  filter: 'exceptions',
  gaps: R102_ALL.gaps.filter((g) => g.status !== 'filled'),
};
