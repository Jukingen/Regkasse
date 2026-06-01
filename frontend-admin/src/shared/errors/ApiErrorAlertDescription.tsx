'use client';

/**
 * Alert body: short localized summary plus copyable raw API text when needed (single parse path).
 * Technical logging runs only in an effect (no re-render spam).
 */
import { useEffect } from 'react';
import { Space, Typography } from 'antd';
import { BackendRawTextBlock } from '@/components/admin-layout/BackendRawTextBlock';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { extractRawApiErrorMessage } from './extractRawApiErrorMessage';
import { normalizeApiError } from './normalizedApiError';
import { buildTechnicalApiErrorPayload } from './technicalApiErrorLog';
import { getUserFacingApiErrorMessage, type TranslateFn, type UserFacingApiErrorOptions } from './userFacingApiError';

type Props = {
  t: TranslateFn;
  error: unknown;
} & UserFacingApiErrorOptions;

export function ApiErrorAlertDescription({ t, error, ...options }: Props) {
  useEffect(() => {
    technicalConsole.error(`[API Error] ${options.logContext}`, buildTechnicalApiErrorPayload(normalizeApiError(error)));
  }, [error, options.logContext]);

  return (
    <Space orientation="vertical" size={4} style={{ width: '100%' }}>
      <Typography.Text>
        {getUserFacingApiErrorMessage(t, error, { ...options, skipLog: true })}
      </Typography.Text>
      <BackendRawTextBlock introKey="common.backend.serverHintIntro" body={extractRawApiErrorMessage(error)} />
    </Space>
  );
}
