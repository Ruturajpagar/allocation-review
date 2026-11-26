import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

/** Shown to signed-in users who are not Admins (for example the demo Viewer). */
@Component({
  selector: 'app-access-denied',
  template: `
    <section class="card narrow">
      <h1>No access to run reviews</h1>
      <p>
        You are signed in as <strong>{{ auth.currentUser()?.email }}</strong>
        ({{ auth.currentUser()?.roles?.join(', ') || 'no role' }}).
      </p>
      <p class="muted">Reviewing allocation runs needs the Admin role. Ask an administrator for access, or sign in with another account.</p>
      <button type="button" (click)="signOut()">Sign out</button>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccessDenied {
  protected readonly auth = inject(AuthService);

  protected signOut(): void {
    this.auth.logout().subscribe();
  }
}
