'use client';

import { Alert, Typography } from 'antd';
import { useCallback, useEffect, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import {
  getOperationLog,
  listOperationLogs,
  undoOperation,
  type OperationLogListItem,
} from '@/features/audit/api/operationLogs';
import { AuditLogsSubNav } from '@/features/audit/components/AuditLogsSubNav';
import { OperationLogViewer } from '@/features/audit/components/OperationLogViewer';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

export default function AuditLogsOperationsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const { hasPermission } = usePermissions();
  const canView = hasPermission(PERMISSIONS.AUDIT_VIEW);

  const [logs, setLogs] = useState<OperationLogListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [total, setTotal] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await listOperationLogs({ page, pageSize });
      setLogs(res.items ?? []);
      setTotal(res.totalCount ?? 0);
    } catch (err) {
      notify.apiError(err, {
        logContext: 'OperationLogs.load',
        fallbackKey: 'activity.operationLog.loadFailed',
      });
    } finally {
      setLoading(false);
    }
  }, [notify, page, pageSize]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  const handleUndo = (id: string) => {
    modal.confirm({
      title: t('activity.operationLog.undoConfirmTitle'),
      content: t('activity.operationLog.undoConfirmContent'),
      okText: t('activity.operationLog.actions.undo'),
      cancelText: t('common.buttons.cancel'),
      onOk: async () => {
        try {
          const res = await undoOperation(id);
          if (!res.success) {
            notify.error(res.message || t('activity.operationLog.undoFailed'));
            return;
          }
          notify.success(t('activity.operationLog.undoSuccess'));
          await load();
        } catch (err) {
          notify.apiError(err, {
            logContext: 'OperationLogs.undo',
            fallbackKey: 'activity.operationLog.undoFailed',
          });
        }
      },
    });
  };

  if (!canView) {
    return (
      <Alert
        type="warning"
        showIcon
        title={t('adminShell.staffPerformance.noPermission')}
        style={{ margin: 24 }}
      />
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('activity.operationLog.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_LABEL_KEYS.auditLogs), href: '/audit-logs' },
          { title: t('activity.operationLog.title') },
        ]}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, maxWidth: 720 }}>
          {t('activity.operationLog.pageIntro')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <AuditLogsSubNav />

      <OperationLogViewer
        logs={logs}
        loading={loading}
        onUndo={handleUndo}
        onViewDetails={async (id) => {
          try {
            return await getOperationLog(id);
          } catch (err) {
            notify.apiError(err, {
              logContext: 'OperationLogs.detail',
              fallbackKey: 'activity.operationLog.loadFailed',
            });
            return null;
          }
        }}
        page={page}
        pageSize={pageSize}
        total={total}
        onPageChange={(nextPage, nextSize) => {
          setPage(nextPage);
          setPageSize(nextSize);
        }}
      />
    </AdminPageShell>
  );
}
