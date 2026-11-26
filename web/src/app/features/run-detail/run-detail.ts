import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { combineLatest, switchMap } from 'rxjs';
import { LoadState, toLoadState } from '../../core/load-state';
import { GAP_STATUS_LABELS, GapFilter, RunReview } from '../../core/models';
import { RunsApi } from '../../core/runs/runs.api';
import { ErrorPanel } from '../../shared/error-panel';

/**
 * Review of one saved run: totals, counts by gap status and the gap rows.
 * The exceptions filter (partially filled + unfilled) is applied by the API and kept in the URL.
 */
@Component({
  selector: 'app-run-detail',
  imports: [RouterLink, DatePipe, DecimalPipe, ErrorPanel],
  templateUrl: './run-detail.html',
  styleUrl: './run-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RunDetail {
  private readonly api = inject(RunsApi);
  private readonly router = inject(Router);

  /** :runId from the route and ?filter= from the URL (router input binding). */
  readonly runId = input.required<string>();
  readonly filter = input<string>();

  protected readonly activeFilter = computed<GapFilter>(() => (this.filter() === 'exceptions' ? 'exceptions' : 'all'));
  protected readonly statusLabels = GAP_STATUS_LABELS;

  private readonly reload = signal(0);

  protected readonly state = toSignal(
    combineLatest([toObservable(this.runId), toObservable(this.activeFilter), toObservable(this.reload)]).pipe(
      switchMap(([runId, filter]) =>
        toLoadState(this.api.getReview(runId, filter), `There is no saved run with the ID “${runId}”.`),
      ),
    ),
    { initialValue: { status: 'loading' } as LoadState<RunReview> },
  );

  protected setFilter(filter: GapFilter): void {
    void this.router.navigate([], { queryParams: { filter: filter === 'all' ? null : filter }, queryParamsHandling: 'merge' });
  }

  protected retry(): void {
    this.reload.update((n) => n + 1);
  }

  protected exceptionCount(review: RunReview): number {
    return review.statusCounts.partiallyFilled + review.statusCounts.unfilled;
  }
}
