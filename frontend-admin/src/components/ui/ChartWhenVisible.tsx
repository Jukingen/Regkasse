'use client';

/**
 * Defers mounting heavy Recharts trees until near the viewport.
 * Pair with `ResponsiveContainer` inside children for layout.
 */
import { type ReactNode } from 'react';

import { LazyWhenVisible } from '@/components/ui/LazyWhenVisible';

type ChartWhenVisibleProps = {
  children: ReactNode;
  /** Reserved height while waiting / for ResponsiveContainer parent. */
  height: number;
  rootMargin?: string;
  enabled?: boolean;
};

export function ChartWhenVisible({
  children,
  height,
  rootMargin = '160px',
  enabled = true,
}: ChartWhenVisibleProps) {
  return (
    <LazyWhenVisible enabled={enabled} rootMargin={rootMargin} minHeight={height}>
      {children}
    </LazyWhenVisible>
  );
}
