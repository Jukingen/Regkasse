'use client';

import { Suspense } from 'react';

import { DepExportTestPage } from '@/features/rksv/components/DepExportTestPage';

export default function AdminRksvDepExportPage() {
  return (
    <Suspense fallback={null}>
      <DepExportTestPage />
    </Suspense>
  );
}
