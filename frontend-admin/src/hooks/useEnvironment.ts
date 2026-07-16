'use client';

/**
 * Frontend build / runtime environment helpers for admin UI.
 * For TSE fiscal simulation mode prefer `useRksvStatus().isDemo` (backend RKSV env).
 */
export function useEnvironment() {
  const isDevelopment = process.env.NODE_ENV === 'development';
  const isProduction = process.env.NODE_ENV === 'production';

  return {
    isDevelopment,
    isProduction,
    nodeEnv: process.env.NODE_ENV ?? 'development',
  } as const;
}
