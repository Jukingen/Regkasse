import { ReactNode } from 'react';

import { PageTransition } from '@/components/ui/PageTransition';

export default function LoginTemplate({ children }: { children: ReactNode }) {
  return <PageTransition>{children}</PageTransition>;
}
