'use client';

import React, { useState } from 'react';
import { Button } from 'antd';
import { SafetyCertificateOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n';
import { BackupVerificationReport } from '@/features/backup/components/BackupVerificationReport';

export interface BackupVerificationReportPanelProps {
  runId: string;
  enabled: boolean;
}

/** Inline entry point: opens full verification report modal. */
export function BackupVerificationReportPanel({ runId, enabled }: BackupVerificationReportPanelProps) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);

  if (!enabled || !runId) return null;

  return (
    <>
      <Button type="default" icon={<SafetyCertificateOutlined />} onClick={() => setOpen(true)}>
        {t('backupDr.verificationReport.openReport')}
      </Button>
      <BackupVerificationReport backupRunId={runId} open={open} onClose={() => setOpen(false)} />
    </>
  );
}
