import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DisplayError } from '../core/http-error';

/** A request failed: say what happened, offer a retry, and show the reference for support. */
@Component({
  selector: 'app-error-panel',
  template: `
    <div class="card alert-card" role="alert">
      <h2>{{ heading() }}</h2>
      <p>{{ error().message }}</p>
      @if (error().correlationId; as ref) {
        <p class="muted small">Reference for support: <code>{{ ref }}</code></p>
      }
      @if (showRetry()) {
        <button type="button" (click)="retry.emit()">Try again</button>
      }
      <ng-content />
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ErrorPanel {
  readonly error = input.required<DisplayError>();
  readonly heading = input('Something went wrong');
  readonly showRetry = input(true);
  readonly retry = output<void>();
}
