import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { createAppHarness } from '../testing/app-harness';
import { ADMIN, HISTORY, R102_ALL, R102_EXCEPTIONS, VIEWER } from '../testing/test-data';

/**
 * User-facing flows: an Admin signs in, opens the newest run from the history and narrows it
 * to the exceptions; a Viewer is kept away from reviews; an expired session goes back to sign-in.
 */
describe('Run review flow', () => {
  // Every API call a test triggers must have been answered.
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('lets an admin sign in, open the latest run and filter it to exceptions', async () => {
    const app = await createAppHarness();

    // 1. Visiting a protected page while signed out lands on sign-in.
    await app.harness.navigateByUrl('/runs');
    await app.settle();
    expect(app.router.url).toBe('/login?returnUrl=%2Fruns');

    // 2. Sign in through the form.
    const email = app.element().querySelector<HTMLInputElement>('#email')!;
    const password = app.element().querySelector<HTMLInputElement>('#password')!;
    email.value = 'admin@demo.local';
    email.dispatchEvent(new Event('input'));
    password.value = 'Admin#Demo2026';
    password.dispatchEvent(new Event('input'));
    app.button('Sign in').click();

    const login = app.http.expectOne('/api/auth/login');
    expect(login.request.method).toBe('POST');
    expect(login.request.body).toEqual({ email: 'admin@demo.local', password: 'Admin#Demo2026' });
    expect(login.request.headers.has('X-Correlation-ID')).toBe(true);
    login.flush(ADMIN);
    await app.settle();

    // 3. The history loads newest first.
    expect(app.router.url).toBe('/runs');
    const history = app.http.expectOne((r) => r.url === '/api/runs');
    expect(history.request.params.get('page')).toBe('1');
    expect(history.request.params.get('pageSize')).toBe('10');
    history.flush(HISTORY);
    await app.settle();

    const runLinks = [...app.element().querySelectorAll('a.run-id')].map((a) => a.textContent?.trim());
    expect(runLinks).toEqual(['R-102', 'R-101']);

    // 4. Open R-102.
    (app.element().querySelector('a.run-id') as HTMLAnchorElement).click();
    await app.settle();
    expect(app.router.url).toBe('/runs/R-102');

    const review = app.http.expectOne((r) => r.url === '/api/runs/R-102');
    expect(review.request.params.get('filter')).toBe('all');
    review.flush(R102_ALL);
    await app.settle();

    expect(app.element().querySelector('[data-testid="total-units"]')?.textContent?.trim()).toBe('5');
    expect(app.element().querySelector('[data-testid="count-partially-filled"]')?.textContent?.trim()).toBe('1');
    expect(gapIds(app.element())).toEqual(['G-01', 'G-02', 'G-03']);
    expect(app.text()).toContain('Partially filled');

    // 5. Narrow to exceptions: the filter goes to the API and into the URL.
    app.button('Exceptions only (2)').click();
    await app.settle();
    expect(app.router.url).toBe('/runs/R-102?filter=exceptions');

    const exceptions = app.http.expectOne((r) => r.url === '/api/runs/R-102');
    expect(exceptions.request.params.get('filter')).toBe('exceptions');
    exceptions.flush(R102_EXCEPTIONS);
    await app.settle();

    expect(gapIds(app.element())).toEqual(['G-01', 'G-03']);
    // Whole-run totals stay visible while filtered.
    expect(app.element().querySelector('[data-testid="total-units"]')?.textContent?.trim()).toBe('5');
  });

  it('keeps a viewer out of run reviews', async () => {
    const app = await createAppHarness();
    await app.signInAs(VIEWER);

    await app.harness.navigateByUrl('/runs/R-102');
    await app.settle();

    expect(app.router.url).toBe('/access-denied');
    expect(app.text()).toContain('No access to run reviews');
    app.http.expectNone((r) => r.url.startsWith('/api/runs'));
  });

  it('sends the user back to sign-in when the session expires mid-review', async () => {
    const app = await createAppHarness();
    await app.signInAs(ADMIN);

    await app.harness.navigateByUrl('/runs');
    await app.settle();
    app.http.expectOne((r) => r.url === '/api/runs').flush(
      { title: 'Unauthorized', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    await app.settle();

    expect(app.router.url).toBe('/login?reason=expired&returnUrl=%2Fruns');
    expect(app.text()).toContain('Your session expired');
    expect(app.auth.isSignedIn()).toBe(false);
  });

  it('shows an unknown run as not found, without a retry button', async () => {
    const app = await createAppHarness();
    await app.signInAs(ADMIN);

    await app.harness.navigateByUrl('/runs/R-999');
    await app.settle();
    app.http.expectOne((r) => r.url === '/api/runs/R-999').flush(
      { title: 'Run not found', status: 404, correlationId: 'abc123' },
      { status: 404, statusText: 'Not Found' },
    );
    await app.settle();

    expect(app.text()).toContain('Run not found');
    expect(app.text()).toContain('There is no saved run with the ID “R-999”.');
    expect(app.text()).not.toContain('Try again');
  });

  it('shows a server error with the support reference and retries on request', async () => {
    const app = await createAppHarness();
    await app.signInAs(ADMIN);

    await app.harness.navigateByUrl('/runs');
    await app.settle();
    app.http.expectOne((r) => r.url === '/api/runs').flush(
      { title: 'Something went wrong', status: 500, correlationId: 'corr-500' },
      { status: 500, statusText: 'Server Error' },
    );
    await app.settle();

    expect(app.text()).toContain('Could not load the run history');
    expect(app.text()).toContain('corr-500');

    app.button('Try again').click();
    await app.settle();
    app.http.expectOne((r) => r.url === '/api/runs').flush(HISTORY);
    await app.settle();

    expect(app.text()).toContain('R-102');
  });

  it('shows an empty history clearly', async () => {
    const app = await createAppHarness();
    await app.signInAs(ADMIN);

    await app.harness.navigateByUrl('/runs');
    await app.settle();
    app.http.expectOne((r) => r.url === '/api/runs').flush({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 });
    await app.settle();

    expect(app.text()).toContain('No saved runs yet');
  });

  it('shows a friendly message for a wrong password', async () => {
    const app = await createAppHarness();
    await app.harness.navigateByUrl('/login');
    await app.settle();

    const email = app.element().querySelector<HTMLInputElement>('#email')!;
    const password = app.element().querySelector<HTMLInputElement>('#password')!;
    email.value = 'admin@demo.local';
    email.dispatchEvent(new Event('input'));
    password.value = 'wrong';
    password.dispatchEvent(new Event('input'));
    app.button('Sign in').click();

    app.http.expectOne('/api/auth/login').flush(
      { title: 'Invalid email or password', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    await app.settle();

    expect(app.router.url).toBe('/login');
    expect(app.text()).toContain('That email and password do not match an account.');
    // A failed sign-in is not treated as an expired session.
    expect(app.text()).not.toContain('Your session expired');
  });
});

function gapIds(root: HTMLElement): string[] {
  return [...root.querySelectorAll('[data-testid="gap-row"] .gap-id')].map((cell) => cell.textContent?.trim() ?? '');
}
