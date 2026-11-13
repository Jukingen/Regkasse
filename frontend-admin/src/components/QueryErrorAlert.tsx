'use client';

/**
 * Inline query failure UI: localized Alert + ApiErrorAlertDescription + optional retry.
 */
import { Alert, Button } from 'antd';
import type { CSSProperties } from 'react';

import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import type { UserFacingApiErrorOptions } from '@/shared/errors/userFacingApiError';

export type QueryErrorAlertProps = {
  error: unknown;
  logContext: string;
  /** i18n key for Alert title (default: common.errorLoadTitle) */
  titleKey?: string;
  onRetry?: () => void;
  /** i18n key for retry button (default: common.buttons.retry) */
  retryLabelKey?: string;
  style?: CSSProperties;
  fallbackKey?: UserFacingApiErrorOptions['fallbackKey'];
};

export function QueryErrorAlert({
  error,
  logContext,
  titleKey = 'common.errorLoadTitle',
  onRetry,
  retryLabelKey = 'common.buttons.retry',
  style,
  fallbackKey,
}: QueryErrorAlertProps) {
  const { t } = useI18n();

  return (
    <Alert
      type="error"
      showIcon
      style={style}
      title={t(titleKey)}
      description={
        <ApiErrorAlertDescription
          t={t}
          error={error}
          logContext={logContext}
          fallbackKey={fallbackKey}
        />
      }
      action={
        onRetry ? (
          <Button size="small" onClick={onRetry}>
            {t(retryLabelKey)}
          </Button>
        ) : undefined
      }
    />
  );
}
