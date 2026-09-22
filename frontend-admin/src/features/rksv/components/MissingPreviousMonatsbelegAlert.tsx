'use client';

import { Alert, Button } from 'antd';

import { useI18n } from '@/i18n/I18nProvider';

export type MissingPreviousMonatsbelegAlertProps = {
  visible: boolean;
  canCreate: boolean;
  onCreateNow: () => void;
};

export function MissingPreviousMonatsbelegAlert({
  visible,
  canCreate,
  onCreateNow,
}: MissingPreviousMonatsbelegAlertProps) {
  const { t } = useI18n();
  if (!visible) return null;

  const tp = (path: string) => t(`rksvHub.monatsbelegePage.${path}`);

  return (
    <Alert
      type="error"
      showIcon
      style={{ marginBottom: 16 }}
      title={tp('missingPreviousTitle')}
      description={tp('missingPreviousBody')}
      data-testid="missing-previous-monatsbeleg-alert"
      action={
        canCreate ? (
          <Button
            type="primary"
            danger
            onClick={onCreateNow}
            data-testid="missing-previous-monatsbeleg-create">
            {tp('createNowForce')}
          </Button>
        ) : null
      }
    />
  );
}
