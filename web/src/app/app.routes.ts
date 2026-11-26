import { Routes } from '@angular/router';
import { authGuard, reviewerGuard, signedOutGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'runs' },
  {
    path: 'login',
    title: 'Sign in · Allocation Review',
    canActivate: [signedOutGuard],
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: 'runs',
    title: 'Run history · Allocation Review',
    canActivate: [reviewerGuard],
    loadComponent: () => import('./features/run-history/run-history').then((m) => m.RunHistory),
  },
  {
    path: 'runs/:runId',
    title: 'Run review · Allocation Review',
    canActivate: [reviewerGuard],
    loadComponent: () => import('./features/run-detail/run-detail').then((m) => m.RunDetail),
  },
  {
    path: 'access-denied',
    title: 'No access · Allocation Review',
    canActivate: [authGuard],
    loadComponent: () => import('./features/access-denied/access-denied').then((m) => m.AccessDenied),
  },
  {
    path: '**',
    title: 'Not found · Allocation Review',
    loadComponent: () => import('./features/not-found/not-found').then((m) => m.NotFound),
  },
];
