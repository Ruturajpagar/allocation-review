import { Observable, catchError, map, of, startWith } from 'rxjs';
import { DisplayError, toDisplayError } from './http-error';

/** The three states every data view handles: loading, loaded, failed. */
export type LoadState<T> =
  | { status: 'loading' }
  | { status: 'loaded'; data: T }
  | { status: 'error'; error: DisplayError };

/** Wraps a request so a view can render loading, data and errors from one value. */
export function toLoadState<T>(source: Observable<T>, notFoundMessage?: string): Observable<LoadState<T>> {
  return source.pipe(
    map((data): LoadState<T> => ({ status: 'loaded', data })),
    catchError((error: unknown) => of<LoadState<T>>({ status: 'error', error: toDisplayError(error, notFoundMessage) })),
    startWith<LoadState<T>>({ status: 'loading' }),
  );
}
