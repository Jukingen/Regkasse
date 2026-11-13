'use client';

/**
 * Isolated entry so production builds can tree-shake React Query Devtools.
 * Only imported when `process.env.NODE_ENV === 'development'`.
 */
export { ReactQueryDevtools as default } from '@tanstack/react-query-devtools';
