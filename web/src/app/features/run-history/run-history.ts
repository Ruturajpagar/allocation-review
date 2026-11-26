import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { combineLatest, switchMap } from 'rxjs';
import { LoadState, toLoadState } from '../../core/load-state';
import { PagedResult, RunSummary } from '../../core/models';
import { RunsApi } from '../../core/runs/runs.api';
import { ErrorPanel } from '../../shared/error-panel';

export const HISTORY_PAGE_SIZE = 10;

/** Saved runs, newest first. The page number lives in the URL (?page=2) so it survives reloads and back/forward. */
@Component({
  selector: 'app-run-history',
  imports: [RouterLink, DatePipe, DecimalPipe, ErrorPanel],
  templateUrl: './run-history.html',
  styleUrl: './run-history.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RunHistory {
  private readonly api = inject(RunsApi);
  private readonly router = inject(Router);

  /** ?page= from the URL (router input binding). */
  readonly page = input<string>();

  protected readonly currentPage = computed(() => {
    const parsed = Number.parseInt(this.page() ?? '1', 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
  });

  private readonly reload = signal(0);

  protected readonly state = toSignal(
    combineLatest([toObservable(this.currentPage), toObservable(this.reload)]).pipe(
      switchMap(([page]) => toLoadState(this.api.getHistory(page, HISTORY_PAGE_SIZE))),
    ),
    { initialValue: { status: 'loading' } as LoadState<PagedResult<RunSummary>> },
  );

  protected goToPage(page: number): void {
    void this.router.navigate([], { queryParams: { page: page === 1 ? null : page }, queryParamsHandling: 'merge' });
  }

  protected retry(): void {
    this.reload.update((n) => n + 1);
  }

  /** Width of a run's units bar, relative to the busiest run on this page (display only). */
  protected unitsPercent(run: RunSummary, runs: RunSummary[]): number {
    const max = Math.max(...runs.map((r) => r.totalAllocatedUnits), 0);
    return max === 0 ? 0 : Math.round((run.totalAllocatedUnits / max) * 100);
  }
}
