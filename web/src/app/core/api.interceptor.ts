import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth/auth.service';

/** Endpoints where a 401 is an expected answer, not an expired session. */
const SESSION_PROBES = ['/api/auth/login', '/api/auth/me'];

/**
 * For every API call:
 *  - sends the session cookie and a fresh X-Correlation-ID (the server logs it and returns it on errors);
 *  - when the server says 401 mid-session, sends the user back to sign-in with a "session expired" note.
 */
export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const auth = inject(AuthService);
  const withHeaders = request.clone({
    withCredentials: true,
    setHeaders: { 'X-Correlation-ID': newCorrelationId() },
  });

  return next(withHeaders).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !SESSION_PROBES.some((url) => request.url.startsWith(url))
      ) {
        auth.handleSessionExpired();
      }
      return throwError(() => error);
    }),
  );
};

function newCorrelationId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now().toString(16)}-${Math.random().toString(16).slice(2)}`;
}
