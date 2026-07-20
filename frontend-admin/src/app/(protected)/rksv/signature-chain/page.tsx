'use client';

import React, { Suspense } from 'react';
import { PageSkeleton } from '@/components/Skeleton';
import RksvSignatureChainVerification from '@/features/rksv/signature-chain/RksvSignatureChainVerification';

/**
 * Suspense boundary required for useSearchParams in RksvSignatureChainVerification.
 */
export default function RksvSignatureChainPage() {
  return (
    <Suspense fallback={<PageSkeleton widgets={3} />}>
      <RksvSignatureChainVerification />
    </Suspense>
  );
}
