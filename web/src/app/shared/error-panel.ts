import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DisplayError } from '../core/http-error';

/** A request failed: say what happened, offer a retry, and show the reference for support. */
@Component({
  selector: 'app-error-panel',
  template: `
    <div class="card alert-card error-panel" role="alert">
      <span class="state-icon danger" aria-hidden="true">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="9" /><path d="M12 7.5v5.5M12 16.2v.3" />
        </svg>
      </span>
      <div class="body">
        <h2>{{ heading() }}</h2>
        <p>{{ error().message }}</p>
        @if (error().correlationId; as ref) {
          <p class="muted small">Reference for support: <code>{{ ref }}</code></p>
        }
        <div class="actions">
          @if (showRetry()) {
            <button type="button" class="primary" (click)="retry.emit()">Try again</button>
          }
          <ng-content />
        </div>
      </div>
    </div>
  `,
  styles: `
    .error-panel {
      display: flex;
      gap: 16px;
      align-items: flex-start;
    }
    .state-icon {
      flex: none;
      margin: 0;
    }
    .body p {
      margin: 4px 0;
    }
    .actions {
      display: flex;
      gap: 10px;
      margin-top: 14px;
    }
    .actions:empty {
      display: none;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ErrorPanel {
  readonly error = input.required<DisplayError>();
  readonly heading = input('Something went wrong');
  readonly showRetry = input(true);
  readonly retry = output<void>();
}
