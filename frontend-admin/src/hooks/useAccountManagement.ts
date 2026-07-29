'use client';

import { useCallback, useMemo, useState } from 'react';

import {
  useCreateDataRightsRequest,
  useDataRightsRequests,
  useDownloadDataRightsExport,
  useTenantDataManagementSummary,
} from '@/features/data-management/hooks/useTenantDataManagement';
import { buildDataExportFileName } from '@/features/data-management/utils/dataExportFileName';
import { useCurrentTenant } from '@/hooks/useCurrentTenant';
import { useLicenseStatus, type LicenseStatusView } from '@/hooks/useLicenseStatus';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useSensitiveExportGate } from '@/hooks/useSensitiveExportGate';
import { SENSITIVE_EXPORT_KINDS } from '@/lib/download/sensitiveExportSecurity';

export type AccountInventoryCounts = {
  productsAndCategories: number;
  customers: number;
  transactions: number;
  rksvRetained: number;
};

function sumByKeys(
  rows: Array<{ key: string; rowCount: number }> | undefined,
  keys: readonly string[]
): number {
  if (!rows?.length) return 0;
  const wanted = new Set(keys);
  return rows.reduce((sum, row) => (wanted.has(row.key) ? sum + (row.rowCount ?? 0) : sum), 0);
}

/**
 * Mandant account / GDPR actions for non-renewing users:
 * data export (ZIP) + account closure (deletion request wizard).
 */
export function useAccountManagement() {
  const notify = useNotify();
  const tenant = useCurrentTenant();
  const { isSuperAdmin } = usePermissions();
  const { status: licenseStatus, isLoading: isLicenseLoading } = useLicenseStatus();
  const tenantId = tenant.tenantId ?? '';

  const summaryQuery = useTenantDataManagementSummary(tenantId);
  const requestsQuery = useDataRightsRequests(tenantId);
  const createMutation = useCreateDataRightsRequest(tenantId);
  const downloadMutation = useDownloadDataRightsExport(tenantId);
  const sensitiveGate = useSensitiveExportGate();

  const [closureModalOpen, setClosureModalOpen] = useState(false);

  const inventory: AccountInventoryCounts = useMemo(() => {
    const rows = summaryQuery.data?.dataTypes;
    return {
      productsAndCategories: sumByKeys(rows, ['products', 'categories']),
      customers: sumByKeys(rows, ['customers']),
      transactions: sumByKeys(rows, ['payment_details', 'receipts', 'invoices_fiscal', 'invoices_non_fiscal']),
      rksvRetained: rows?.reduce((sum, row) => (row.isRksvRetained ? sum + (row.rowCount ?? 0) : sum), 0) ?? 0,
    };
  }, [summaryQuery.data?.dataTypes]);

  const canExport = summaryQuery.data?.canExport !== false;
  const canRequestClosure = summaryQuery.data?.canRequestDeletion === true;

  const downloadReadyExport = useCallback(
    async (requestId: string, artifactFileName?: string | null) => {
      sensitiveGate.run({
        kind: SENSITIVE_EXPORT_KINDS.GdprDataExport,
        resourceId: requestId,
        isSuperAdmin,
        execute: async (headers) => {
          const blob = await downloadMutation.mutateAsync({ requestId, headers });
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download =
            artifactFileName ??
            buildDataExportFileName(summaryQuery.data?.tenantSlug ?? tenant.tenantSlug ?? null);
          a.click();
          URL.revokeObjectURL(url);
          notify.successKey('dataManagement.exportSuccess');
        },
      });
    },
    [
      downloadMutation,
      isSuperAdmin,
      notify,
      sensitiveGate,
      summaryQuery.data?.tenantSlug,
      tenant.tenantSlug,
    ]
  );

  const requestDataExport = useCallback(async () => {
    if (!tenantId) {
      notify.warning('dataManagement.noTenantContext');
      return;
    }
    if (!canExport) {
      notify.warning('dataManagement.lockedCard.exportUnavailable');
      return;
    }

    try {
      const row = await createMutation.mutateAsync({ type: 'export' });
      notify.successKey('dataManagement.requestSent');

      if (row.canDownload) {
        await downloadReadyExport(row.id, row.artifactFileName);
      } else if (row.downloadLink) {
        notify.successKey('dataManagement.ready');
        window.open(row.downloadLink, '_blank', 'noopener,noreferrer');
      } else {
        notify.info('dataManagement.processing');
      }
      void requestsQuery.refetch();
    } catch {
      notify.errorKey('dataManagement.rights.requestFailed');
    }
  }, [
    canExport,
    createMutation,
    downloadReadyExport,
    notify,
    requestsQuery,
    tenantId,
  ]);

  const requestAccountClosure = useCallback(() => {
    if (!tenantId) {
      notify.warning('dataManagement.noTenantContext');
      return;
    }
    if (!canRequestClosure) {
      notify.warning('dataManagement.deleteWarning');
      return;
    }
    setClosureModalOpen(true);
  }, [canRequestClosure, notify, tenantId]);

  return {
    tenant,
    tenantId,
    licenseStatus: licenseStatus as LicenseStatusView | null,
    summary: summaryQuery.data ?? null,
    inventory,
    canExport,
    canRequestClosure,
    requestDataExport,
    requestAccountClosure,
    isExporting: createMutation.isPending || downloadMutation.isPending,
    isClosing: false,
    isLoading: isLicenseLoading || summaryQuery.isLoading,
    isSummaryError: summaryQuery.isError,
    closureModalOpen,
    setClosureModalOpen,
    refetch: summaryQuery.refetch,
  };
}
