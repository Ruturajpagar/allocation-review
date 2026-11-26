import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { GapFilter, PagedResult, RunReview, RunSummary } from '../models';

/** Typed access to the run review endpoints (Admin only). */
@Injectable({ providedIn: 'root' })
export class RunsApi {
  private readonly http = inject(HttpClient);

  getHistory(page: number, pageSize: number): Observable<PagedResult<RunSummary>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<RunSummary>>('/api/runs', { params });
  }

  getReview(runId: string, filter: GapFilter): Observable<RunReview> {
    const params = new HttpParams().set('filter', filter);
    return this.http.get<RunReview>(`/api/runs/${encodeURIComponent(runId)}`, { params });
  }
}
