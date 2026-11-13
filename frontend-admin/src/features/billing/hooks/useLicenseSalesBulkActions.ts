'use client';

import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useState } from 'react';

import {
  postApiAdminBillingLicenseSalesIdCancel,
  postApiAdminTenantsTenantIdLicenseExtend,
} from '@/api/generated/admin/admin';
import type { LicenseSaleResponse } from '@/api/generated/model';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import {
  type BulkRunProgress,
  type BulkRunResult,
  type LicenseSalesBulkActionKind,
  buildBulkExtendTargets,
  extendDaysForBulkAction,
  filterBulkRevokeSales,
  runBulkSequential,
} from '@/features/billing/utils/billingSalesBulk';
import { exportLicenseSalesCsv } from '@/features/billing/utils/exportLicenseSalesCsv';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

export type UseLicenseSalesBulkActionsResult = {
  pendingAction: LicenseSalesBulkActionKind | null;
  confirmOpen: boolean;
  progressOpen: boolean;
  progress: BulkRunProgress | null;
  running: boolean;
  eligibleCountForPending: number;
  requestAction: (action: LicenseSalesBulkActionKind, selected: LicenseSaleResponse[]) => void;
  closeConfirm: () => void;
  confirmPending: (reason?: string) => Promise<void>;
};

export function useLicenseSalesBulkActions(): UseLicenseSalesBulkActionsResult {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();

  const [pendingAction, setPendingAction] = useState<LicenseSalesBulkActionKind | null>(null);
  const [pendingSales, setPendingSales] = useState<LicenseSaleResponse[]>([]);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [progressOpen, setProgressOpen] = useState(false);
  const [progress, setProgress] = useState<BulkRunProgress | null>(null);
  const [running, setRunning] = useState(false);

  const eligibleCountForPending = (() => {
    if (!pendingAction) return 0;
    const days = extendDaysForBulkAction(pendingAction);
    if (days != null) return buildBulkExtendTargets(pendingSales, days).length;
    if (pendingAction === 'revoke') return filterBulkRevokeSales(pendingSales).length;
    return pendingSales.length;
  })();

  const finishWithResult = useCallback(
    async (result: BulkRunResult) => {
      setProgressOpen(false);
      setProgress(null);
      setRunning(false);
      setConfirmOpen(false);
      setPendingAction(null);
      setPendingSales([]);

      if (result.total === 0) {
        notify.warning(t('billing.licenseSales.bulk.confirm.noneEligible'));
        return;
      }

      await queryClient.invalidateQueries({ queryKey: billingQueryKeys.all });

      if (result.failed === 0) {
        notify.success(
          t('billing.licenseSales.bulk.result.success', { count: result.success })
        );
        return;
      }

      notify.warning(
        t('billing.licenseSales.bulk.result.partial', {
          success: result.success,
          failed: result.failed,
        })
      );
    },
    [notify, queryClient, t]
  );

  const runExtend = useCallback(
    async (days: number, sales: LicenseSaleResponse[]) => {
      const targets = buildBulkExtendTargets(sales, days);
      setProgressOpen(true);
      setRunning(true);
      const result = await runBulkSequential(
        targets,
        (target) => ({
          id: target.tenantId,
          label:
            target.sale.tenantName ??
            target.sale.tenantSlug ??
            target.tenantId,
        }),
        async (target) => {
          await postApiAdminTenantsTenantIdLicenseExtend(target.tenantId, {
            validUntilUtc: target.nextValidUntilUtc,
          });
        },
        setProgress
      );
      await finishWithResult(result);
    },
    [finishWithResult]
  );

  const runRevoke = useCallback(
    async (sales: LicenseSaleResponse[], reason: string) => {
      const targets = filterBulkRevokeSales(sales);
      setProgressOpen(true);
      setRunning(true);
      const result = await runBulkSequential(
        targets,
        (sale) => ({
          id: sale.id!,
          label: sale.invoiceNumber ?? sale.licenseKey ?? sale.id!,
        }),
        async (sale) => {
          await postApiAdminBillingLicenseSalesIdCancel(sale.id!, {
            cancellationReason: reason,
          });
        },
        setProgress
      );
      await finishWithResult(result);
    },
    [finishWithResult]
  );

  const requestAction = useCallback(
    (action: LicenseSalesBulkActionKind, selected: LicenseSaleResponse[]) => {
      if (selected.length === 0) return;

      if (action === 'exportCsv') {
        exportLicenseSalesCsv(selected, {
          invoiceNumber: t('billing.licenseSales.detail.labels.invoiceNumber'),
          tenantName: t('billing.licenseSales.detail.labels.tenantName'),
          tenantSlug: t('billing.licenseSales.detail.labels.tenantSlug'),
          licenseKey: t('billing.licenseSales.detail.labels.licenseKey'),
          licensePlan: t('billing.licenseSales.detail.labels.plan'),
          licenseType: t('billing.licenseSales.detail.labels.licenseType'),
          status: t('billing.licenseSales.detail.labels.status'),
          validFrom: t('billing.licenseSales.detail.labels.validFrom'),
          validUntil: t('billing.licenseSales.detail.labels.validUntil'),
          priceNet: t('billing.licenseSales.detail.labels.priceNet'),
          priceGross: t('billing.licenseSales.detail.labels.priceGross'),
          soldAt: t('billing.sales.columns.soldAt'),
        });
        notify.success(t('billing.licenseSales.bulk.result.exportSuccess', { count: selected.length }));
        return;
      }

      setPendingAction(action);
      setPendingSales(selected);
      setConfirmOpen(true);
    },
    [notify, t]
  );

  const closeConfirm = useCallback(() => {
    if (running) return;
    setConfirmOpen(false);
    setPendingAction(null);
    setPendingSales([]);
  }, [running]);

  const confirmPending = useCallback(
    async (reason?: string) => {
      if (!pendingAction || pendingSales.length === 0) return;

      const days = extendDaysForBulkAction(pendingAction);
      if (days != null) {
        await runExtend(days, pendingSales);
        return;
      }

      if (pendingAction === 'revoke') {
        const trimmed =
          reason?.trim() || t('billing.licenseSales.bulk.confirm.revokeDefaultReason');
        await runRevoke(pendingSales, trimmed);
      }
    },
    [pendingAction, pendingSales, runExtend, runRevoke, t]
  );

  return {
    pendingAction,
    confirmOpen,
    progressOpen,
    progress,
    running,
    eligibleCountForPending,
    requestAction,
    closeConfirm,
    confirmPending,
  };
}
