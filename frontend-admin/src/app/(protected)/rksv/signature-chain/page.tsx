'use client';

import React, { Suspense } from 'react';
import { Spin } from 'antd';
import RksvSignatureChainVerification from '@/features/rksv/signature-chain/RksvSignatureChainVerification';
import { useI18n } from '@/i18n';

function SignatureChainPageFallback() {
  const { t } = useI18n();
  return <Spin style={{ display: 'block', margin: '80px auto' }} description={t('common.loading.data')} />;
}

/**
 * Suspense boundary required for useSearchParams in RksvSignatureChainVerification.
 */
export default function RksvSignatureChainPage() {
  return (
    <Suspense fallback={<SignatureChainPageFallback />}>
      <RksvSignatureChainVerification />
    </Suspense>
  );
}
