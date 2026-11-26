import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** A message a user can act on, plus the reference support needs to find the server logs. */
export interface DisplayError {
  status: number;
  message: string;
  correlationId?: string;
}

/** Turns any HTTP failure into a short, user-facing message. Never shows raw server text for 5xx. */
export function toDisplayError(error: unknown, notFoundMessage = 'The item you asked for does not exist.'): DisplayError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, message: 'Something unexpected went wrong in the app. Please reload the page.' };
  }

  const problem = (typeof error.error === 'object' && error.error !== null ? error.error : {}) as ProblemDetails;
  const correlationId = problem.correlationId ?? error.headers?.get('X-Correlation-ID') ?? undefined;

  let message: string;
  switch (error.status) {
    case 0:
      message = 'Could not reach the server. Check your connection and try again.';
      break;
    case 400:
      message = problem.errors
        ? Object.values(problem.errors).flat().join(' ')
        : (problem.detail ?? 'The request was not valid.');
      break;
    case 401:
      message = 'Your session has ended. Please sign in again.';
      break;
    case 403:
      message = 'Your account does not have access to this page.';
      break;
    case 404:
      message = notFoundMessage;
      break;
    default:
      message = 'The server could not complete the request. Please try again in a moment.';
  }

  return { status: error.status, message, correlationId };
}
