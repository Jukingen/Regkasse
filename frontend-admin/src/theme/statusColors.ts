import type { CSSProperties } from 'react';

import { palette } from './palette';

export type StatusKind = 'healthy' | 'success' | 'warning' | 'error' | 'info' | 'neutral';

/**
 * Semantic status colors for Statistic / Tag / icon accents outside JSX
 * (mappers, PDF helpers). In React prefer `theme.useToken()`.
 */
export function statusColor(kind: StatusKind): string {
  switch (kind) {
    case 'healthy':
    case 'info':
      return palette.info;
    case 'success':
      return palette.success;
    case 'warning':
      return palette.warning;
    case 'error':
      return palette.error;
    case 'neutral':
    default:
      return 'inherit';
  }
}

export function statusValueStyle(kind: StatusKind): CSSProperties {
  if (kind === 'neutral') {
    return {};
  }
  return { color: statusColor(kind) };
}
