import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app.routes';
import { apiInterceptor } from '../core/api.interceptor';
import { AuthService } from '../core/auth/auth.service';
import { CurrentUser } from '../core/models';

/**
 * Boots the real routes, guards and interceptor with a fake HTTP backend, so tests can drive
 * the app the way a user does (navigate, type, click) and answer the API calls it makes.
 */
export async function createAppHarness() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(routes, withComponentInputBinding()),
      provideHttpClient(withInterceptors([apiInterceptor])),
      provideHttpClientTesting(),
    ],
  });

  const harness = await RouterTestingHarness.create();
  const http = TestBed.inject(HttpTestingController);
  const router = TestBed.inject(Router);
  const auth = TestBed.inject(AuthService);

  /** Lets lazy routes, HTTP callbacks and change detection settle. */
  const settle = async () => {
    for (let i = 0; i < 5; i++) {
      await harness.fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve));
      harness.detectChanges();
    }
  };

  /** Signs in through the real AuthService, answering the login call. */
  const signInAs = async (user: CurrentUser) => {
    auth.login(user.email, 'password').subscribe();
    http.expectOne('/api/auth/login').flush(user);
    await settle();
  };

  const element = () => harness.routeNativeElement as HTMLElement;
  const text = () => element().textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const button = (label: string) => {
    const found = [...element().querySelectorAll('button')].find((b) => b.textContent?.includes(label));
    if (!found) {
      throw new Error(`No button labelled "${label}". Page text: ${text()}`);
    }
    return found as HTMLButtonElement;
  };

  return { harness, http, router, auth, settle, signInAs, element, text, button };
}
