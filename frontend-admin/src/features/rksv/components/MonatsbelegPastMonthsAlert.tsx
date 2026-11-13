'use client';

import { Alert, Button } from 'antd';

export type MonatsbelegPastMonthsAlertProps = {
  otherMissingCount: number;
  onManagePastMonths: () => void;
  canCreate?: boolean;
};

export function MonatsbelegPastMonthsAlert({
  otherMissingCount,
  onManagePastMonths,
  canCreate = true,
}: MonatsbelegPastMonthsAlertProps) {
  if (otherMissingCount <= 0) {
    return null;
  }

  return (
    <Alert
      type="warning"
      showIcon
      style={{ marginBottom: 12 }}
      title="Frühere Monatsbelege fehlen"
      description={`${otherMissingCount} Monatsbelege aus früheren Monaten sind nicht erstellt. Diese können erstellt werden, aber FinanzOnline könnte sie hinterfragen.`}
      action={
        canCreate ? (
          <Button size="small" danger onClick={onManagePastMonths}>
            Frühere Monate verwalten
          </Button>
        ) : undefined
      }
    />
  );
}
