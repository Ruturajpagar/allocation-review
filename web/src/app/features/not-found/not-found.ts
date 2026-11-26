import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <section class="card narrow">
      <h1>Page not found</h1>
      <p class="muted">That address does not match a page in this app.</p>
      <a routerLink="/">Go to the start page</a>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFound {}
