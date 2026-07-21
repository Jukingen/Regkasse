'use client';

import React, { Suspense } from 'react';

import { PageSkeleton } from '@/components/Skeleton';
import RksvSonderbelegePage from '@/features/rksv-operations/components/RksvSonderbelegePage';

export default function RksvSonderbelegeRoutePage() {
  return (
    <Suspense fallback={<PageSkeleton widgets={4} />}>
      <RksvSonderbelegePage />
    </Suspense>
  );
}
