'use client';

import { CloudUploadOutlined, ExperimentOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Popconfirm, Space, Tooltip, Typography } from 'antd';
import React from 'react';

import type { ManualActionsModeConfirmations } from '@/features/backup-dr/logic/backupManualActionsModePresentation';

export interface ManualActionsPanelProps {
  /** Manuel yedek tetikleme yetkisi (Manager: backup.manage). */
  canManage: boolean;
  /**
   * Restore-drill kuyruğa alma yetkisi — platform operatörü (settings.manage / SuperAdmin).
   * Verilmezse geriye dönük olarak <see cref="canManage"/> kullanılır.
   */
  canRestore?: boolean;
  /** Orval mutation — geniş imza ile uyumlu. */
  backupTrigger: { isPending: boolean; mutate: (...args: unknown[]) => unknown };
  restoreTrigger: { isPending: boolean; mutate: (...args: unknown[]) => unknown };
  /** Fake/stub ortamında kuyruk tetiklerinin üretim yedeği olmadığını anlatır. */
  simulatedOperationalMode?: boolean;
  /**
   * execution-mode + son yedek satırından türetilen onay metinleri.
   * Verilmezse eski statik i18n anahtarları kullanılır (geriye dönük testler).
   */
  modeAwareConfirmations?: ManualActionsModeConfirmations | null;
  t: (k: string, options?: Record<string, string | number>) => string;
}

/** İzin yokken devre dışı düğmelerde ipucu — teknik kesinti sanılmasın. */
function WithPermissionTooltip({
  show,
  title,
  children,
}: {
  show: boolean;
  title: string;
  children: React.ReactNode;
}) {
  if (!show) return <>{children}</>;
  return (
    <Tooltip title={title}>
      <span style={{ display: 'inline-block', cursor: 'not-allowed' }}>{children}</span>
    </Tooltip>
  );
}

function PopconfirmDescription({ parts }: { parts: string[] }) {
  return (
    <div style={{ maxWidth: 400 }}>
      {parts.map((p, i) => (
        <Typography.Paragraph key={i} style={{ marginBottom: i === parts.length - 1 ? 0 : 8 }}>
          {p}
        </Typography.Paragraph>
      ))}
    </div>
  );
}

export function ManualActionsPanel({
  canManage,
  canRestore,
  backupTrigger,
  restoreTrigger,
  simulatedOperationalMode = false,
  modeAwareConfirmations,
  t,
}: ManualActionsPanelProps) {
  const canRestoreEffective = canRestore ?? canManage;
  const permissionTip = t('backupDr.permission.manualActionsTooltip');
  const backupDisabled = !canManage || backupTrigger.isPending;
  const restoreDisabled = !canRestoreEffective || restoreTrigger.isPending;

  const backupTitle =
    modeAwareConfirmations?.backupTitle ?? t('backupDr.manual.confirmBackupTitle');
  const backupDescParts = modeAwareConfirmations?.backupDescriptionParts ?? [
    t('backupDr.manual.confirmBackupDescription'),
  ];
  const restoreTitle =
    modeAwareConfirmations?.restoreTitle ?? t('backupDr.manual.confirmRestoreTitle');
  const restoreDescParts = modeAwareConfirmations?.restoreDescriptionParts ?? [
    t('backupDr.manual.confirmRestoreDescription'),
  ];

  const backupControl = (
    <Popconfirm
      title={backupTitle}
      description={<PopconfirmDescription parts={backupDescParts} />}
      okText={t('backupDr.manual.confirmBackupOk')}
      cancelText={t('backupDr.manual.confirmBackupCancel')}
      disabled={backupDisabled}
      onConfirm={() => backupTrigger.mutate({ data: {} })}
      overlayStyle={{ maxWidth: 440 }}
    >
      <Button
        type="primary"
        icon={<CloudUploadOutlined />}
        disabled={backupDisabled}
        loading={backupTrigger.isPending}
      >
        {t('backupDr.actions.enqueueBackup')}
      </Button>
    </Popconfirm>
  );

  const restoreControl = (
    <Popconfirm
      title={restoreTitle}
      description={<PopconfirmDescription parts={restoreDescParts} />}
      okText={t('backupDr.manual.confirmRestoreOk')}
      cancelText={t('backupDr.manual.confirmRestoreCancel')}
      disabled={restoreDisabled}
      onConfirm={() => restoreTrigger.mutate({ data: {} })}
      overlayStyle={{ maxWidth: 440 }}
    >
      <Button
        icon={<ExperimentOutlined />}
        disabled={restoreDisabled}
        loading={restoreTrigger.isPending}
      >
        {t('backupDr.actions.enqueueRestoreDrill')}
      </Button>
    </Popconfirm>
  );

  return (
    <Card title={t('backupDr.manual.title')} size="small">
      <Typography.Paragraph type="secondary">{t('backupDr.manual.hint')}</Typography.Paragraph>
      {simulatedOperationalMode ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('backupDr.manual.hintFake')}
        </Typography.Paragraph>
      ) : null}
      {modeAwareConfirmations?.cardAlert ? (
        <Alert
          type={modeAwareConfirmations.cardAlert.severity === 'error' ? 'error' : 'warning'}
          showIcon
          style={{ marginBottom: 12 }}
          title={modeAwareConfirmations.cardAlert.message}
        />
      ) : null}
      {modeAwareConfirmations?.actionBannerLine ? (
        <Typography.Paragraph style={{ marginBottom: 12 }}>
          <Typography.Text strong>{modeAwareConfirmations.actionBannerLine}</Typography.Text>
        </Typography.Paragraph>
      ) : null}
      <Space wrap>
        <WithPermissionTooltip show={!canManage} title={permissionTip}>
          {backupControl}
        </WithPermissionTooltip>
        <WithPermissionTooltip show={!canRestoreEffective} title={permissionTip}>
          {restoreControl}
        </WithPermissionTooltip>
      </Space>
    </Card>
  );
}
