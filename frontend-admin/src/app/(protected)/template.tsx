import { ReactNode } from 'react';

import { PageTransition } from '@/components/ui/PageTransition';

/**
 * Remounts on segment navigations (App Router). PageTransition also
 * re-triggers the enter animation when the pathname changes within a segment.
 */
export default function ProtectedTemplate({ children }: { children: ReactNode }) {
  return <PageTransition>{children}</PageTransition>;
}
