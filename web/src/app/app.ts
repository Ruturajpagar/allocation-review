import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';

/** App shell: top bar with the signed-in user, and the routed page below. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <div class="topbar-inner">
        <a class="brand" routerLink="/">
          <span class="brand-mark" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M4 7h10M4 12h16M4 17h7" />
            </svg>
          </span>
          Allocation Review
        </a>
        @if (auth.currentUser(); as user) {
          <nav class="topnav" aria-label="Main">
            @if (user.canReviewRuns) {
              <a routerLink="/runs" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: false }">Run history</a>
            }
          </nav>
          <div class="user">
            <span class="avatar" aria-hidden="true">{{ user.email.charAt(0).toUpperCase() }}</span>
            <span class="user-meta">
              <span class="user-email">{{ user.email }}</span>
              <span class="role-pill">{{ user.roles.join(', ') }}</span>
            </span>
            <button type="button" class="link-button" (click)="signOut()">Sign out</button>
          </div>
        }
      </div>
    </header>
    <main class="page">
      <router-outlet />
    </main>
  `,
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly auth = inject(AuthService);

  protected signOut(): void {
    this.auth.logout().subscribe();
  }
}
