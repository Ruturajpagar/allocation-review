import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, firstValueFrom, map, Observable, of, tap } from 'rxjs';
import { CurrentUser } from '../models';

/**
 * Holds who is signed in. The session itself is an HttpOnly cookie that scripts cannot read,
 * so the app learns about the user from /api/auth/login and /api/auth/me.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly user = signal<CurrentUser | null>(null);

  readonly currentUser = this.user.asReadonly();
  readonly isSignedIn = computed(() => this.user() !== null);
  readonly canReviewRuns = computed(() => this.user()?.canReviewRuns ?? false);

  /** Called once at start-up: restores the session if the cookie is still valid. */
  restoreSession(): Promise<void> {
    return firstValueFrom(
      this.http.get<CurrentUser>('/api/auth/me').pipe(
        tap((user) => this.user.set(user)),
        map(() => undefined),
        catchError(() => {
          this.user.set(null);
          return of(undefined);
        }),
      ),
    );
  }

  login(email: string, password: string): Observable<CurrentUser> {
    return this.http
      .post<CurrentUser>('/api/auth/login', { email, password })
      .pipe(tap((user) => this.user.set(user)));
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', null).pipe(
      catchError((error: HttpErrorResponse) => {
        // Signing out locally still matters even if the server call failed.
        console.warn('Sign-out request failed', error.status);
        return of(undefined);
      }),
      tap(() => {
        this.user.set(null);
        void this.router.navigate(['/login']);
      }),
    );
  }

  /** The server rejected the session (expired or revoked): forget the user and ask them to sign in. */
  handleSessionExpired(): void {
    if (this.user() === null) {
      return;
    }
    this.user.set(null);
    const returnUrl = this.router.url.startsWith('/login') ? undefined : this.router.url;
    void this.router.navigate(['/login'], { queryParams: { reason: 'expired', returnUrl } });
  }
}
