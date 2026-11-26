import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <section class="card narrow">
      <span class="state-icon" aria-hidden="true">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="11" cy="11" r="6.5" /><path d="M20 20l-4.2-4.2" />
        </svg>
      </span>
      <h1>Page not found</h1>
      <p class="muted">That address does not match a page in this app.</p>
      <a class="button-link" routerLink="/">Go to the start page</a>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFound {}
