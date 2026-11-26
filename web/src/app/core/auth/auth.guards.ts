import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Pages that need a signed-in user. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isSignedIn()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/**
 * Pages that only Admins can use. This only shapes the UI: the API enforces the same rule
 * and answers 403 to anyone else.
 */
export const reviewerGuard: CanActivateFn = (route, state) => {
  const signedIn = authGuard(route, state);
  if (signedIn !== true) {
    return signedIn;
  }
  return inject(AuthService).canReviewRuns() ? true : inject(Router).createUrlTree(['/access-denied']);
};

/** The sign-in page is pointless once signed in. */
export const signedOutGuard: CanActivateFn = () =>
  inject(AuthService).isSignedIn() ? inject(Router).createUrlTree(['/']) : true;
