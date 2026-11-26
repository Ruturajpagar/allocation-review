import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';

/** App shell: top bar with the signed-in user, and the routed page below. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <a class="brand" routerLink="/">Allocation Review</a>
      @if (auth.currentUser(); as user) {
        <nav class="topnav" aria-label="Main">
          @if (user.canReviewRuns) {
            <a routerLink="/runs" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: false }">Run history</a>
          }
        </nav>
        <div class="user">
          <span class="user-email">{{ user.email }}</span>
          <span class="role-pill">{{ user.roles.join(', ') }}</span>
          <button type="button" class="link-button" (click)="signOut()">Sign out</button>
        </div>
      }
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
