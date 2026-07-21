'use client';

import type { ReactNode } from 'react';

import { AppLayout } from '@/components/AppLayout';

type Props = {
  children: ReactNode;
};

/** @deprecated Use `AppLayout` from `@/components/AppLayout`. */
export function IdleTimeoutProvider({ children }: Props) {
  return <AppLayout>{children}</AppLayout>;
}
