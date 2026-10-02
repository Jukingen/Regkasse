'use client';

/**
 * Super Admin acknowledgement of open CH QR print gaps for one mandant.
 * Does not change the PDF and does not enable bank submission.
 */
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Checkbox, Space, Spin, Typography } from 'antd';
import { useMemo, useState } from 'react';

import {
  getGetApiAdminTenantsTenantIdChQrGapAcceptanceQueryKey,
  useGetApiAdminTenantsTenantIdChQrGapAcceptance,
  usePostApiAdminTenantsTenantIdChQrGapAcceptance,
} from '@/api/generated/admin/admin';
import type { ChQrKnownGapDto } from '@/api/generated/model';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { getHttpStatusFromError } from '@/lib/queryErrorHandling';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

export type ChQrGapAcceptancePanelProps = {
  tenant: AdminTenantDetail;
};

type KnownGapRow = {
  id: string;
  present: boolean;
};

/**
 * Catalog rows expose `id` and `present` only. Labels live under `tenants.chQrGaps.gap.*`.
 * Gap ids contain hyphens, which the locale key regex rejects, so each id maps to a camelCase key.
 * `present` means the print feature exists in code; the checkbox is operator acceptance.
 */
function gapLabel(t: (key: string) => string, id: string): string {
  switch (id) {
    case 'official-swiss-cross':
      return t('tenants.chQrGaps.gap.officialSwissCross');
    case 'font-embedding-liberation-arial':
      return t('tenants.chQrGaps.gap.fontEmbeddingLiberationArial');
    case 'pain001':
      return t('tenants.chQrGaps.gap.pain001');
    case 'bank-scan':
      return t('tenants.chQrGaps.gap.bankScan');
    case 'perforation-line':
      return t('tenants.chQrGaps.gap.perforationLine');
    default:
      return id;
  }
}

function knownGapRows(gaps: ChQrKnownGapDto[] | null | undefined): KnownGapRow[] {
  if (!gaps) return [];
  const rows: KnownGapRow[] = [];
  for (const gap of gaps) {
    if (typeof gap.id !== 'string' || gap.id.length === 0) continue;
    rows.push({ id: gap.id, present: gap.present === true });
  }
  return rows;
}

function readInvalidGapIds(error: unknown): string[] {
  if (!error || typeof error !== 'object') return [];
  const data = (error as { response?: { data?: unknown } }).response?.data;
  if (!data || typeof data !== 'object') return [];
  const raw = (data as { invalidGapIds?: unknown }).invalidGapIds;
  if (!Array.isArray(raw)) return [];
  return raw.filter((id): id is string => typeof id === 'string' && id.length > 0);
}

export function ChQrGapAcceptancePanel({ tenant }: ChQrGapAcceptancePanelProps) {
  const { t } = useI18n();
  const { user } = useAuth();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const superAdmin = isSuperAdmin(user?.role);
  const canSave = hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
  const isSwitzerland = tenant.country === 'CH';
  const visible = superAdmin && isSwitzerland;
  const [checked, setChecked] = useState<string[]>([]);
  const [invalidIds, setInvalidIds] = useState<string[]>([]);
  const [postNotFound, setPostNotFound] = useState(false);
  const [appliedSnapshot, setAppliedSnapshot] = useState<string | null>(null);

  const query = useGetApiAdminTenantsTenantIdChQrGapAcceptance(tenant.id, {
    query: {
      enabled: visible && tenant.id.length > 0,
      retry: (failureCount, error) =>
        getHttpStatusFromError(error) !== 404 && failureCount < 1,
    },
  });

  const mutation = usePostApiAdminTenantsTenantIdChQrGapAcceptance();

  const gaps = useMemo(() => knownGapRows(query.data?.knownGaps), [query.data]);
  const acceptedSignature = (query.data?.acceptance?.acceptedGaps ?? []).join('\u0001');
  const snapshotKey = query.isSuccess ? `${acceptedSignature}|${gaps.map((gap) => gap.id).join(',')}` : null;

  if (visible && query.isSuccess && snapshotKey != null && appliedSnapshot !== snapshotKey) {
    const knownIds = new Set(gaps.map((gap) => gap.id));
    const accepted = (query.data?.acceptance?.acceptedGaps ?? []).filter(
      (id): id is string => typeof id === 'string' && knownIds.has(id)
    );
    setAppliedSnapshot(snapshotKey);
    setChecked(accepted);
  }

  const checkedSet = useMemo(() => new Set(checked), [checked]);
  const outstanding = gaps.filter((gap) => !gap.present && !checkedSet.has(gap.id));
  const notFound =
    postNotFound || (query.isError && getHttpStatusFromError(query.error) === 404);

  if (!visible || notFound) {
    return null;
  }

  const toggle = (id: string, next: boolean) => {
    setChecked((current) => {
      if (next) {
        return current.includes(id) ? current : [...current, id];
      }
      return current.filter((item) => item !== id);
    });
    setInvalidIds((current) => current.filter((item) => item !== id));
  };

  const save = async () => {
    const acceptedGaps = gaps.map((gap) => gap.id).filter((id) => checkedSet.has(id));
    try {
      await mutation.mutateAsync({
        tenantId: tenant.id,
        data: { acceptedGaps },
      });
      setInvalidIds([]);
      notify.success(t('tenants.chQrGaps.saved'));
      await queryClient.invalidateQueries({
        queryKey: getGetApiAdminTenantsTenantIdChQrGapAcceptanceQueryKey(tenant.id),
      });
    } catch (error: unknown) {
      if (getHttpStatusFromError(error) === 404) {
        setPostNotFound(true);
        return;
      }
      if (getHttpStatusFromError(error) === 400) {
        setInvalidIds(readInvalidGapIds(error));
        notify.error(t('tenants.chQrGaps.invalidGaps'));
        return;
      }
      notify.apiError(error, {
        logContext: 'ChQrGapAcceptancePanel.save',
        fallbackKey: 'tenants.messages.saveFailed',
      });
    }
  };

  return (
    <Card data-testid="ch-qr-gap-acceptance-panel" title={t('tenants.chQrGaps.title')}>
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tenants.chQrGaps.description')}
        </Typography.Paragraph>
        {query.isLoading ? <Spin data-testid="ch-qr-gap-loading" /> : null}
        {query.isError && !notFound ? (
          <Alert type="error" showIcon title={t('tenants.chQrGaps.loadFailed')} />
        ) : null}
        {outstanding.length > 0 ? (
          <div data-testid="ch-qr-gap-outstanding-warning">
            <Alert type="warning" showIcon title={t('tenants.chQrGaps.outstandingWarning')} />
          </div>
        ) : null}
        {invalidIds.length > 0 ? (
          <Alert
            type="error"
            showIcon
            data-testid="ch-qr-gap-invalid-alert"
            title={t('tenants.chQrGaps.invalidGaps')}
            description={invalidIds.join(', ')}
          />
        ) : null}
        {gaps.map((gap) => {
          const invalid = invalidIds.includes(gap.id);
          return (
            <div
              key={gap.id}
              data-testid={`ch-qr-gap-${gap.id}`}
              data-invalid={invalid ? 'true' : 'false'}
            >
              <Checkbox
                checked={checkedSet.has(gap.id)}
                disabled={!canSave || mutation.isPending}
                onChange={(event) => toggle(gap.id, event.target.checked)}
              >
                {gapLabel(t, gap.id)}
                {invalid ? (
                  <Typography.Text type="danger" data-testid={`ch-qr-gap-invalid-${gap.id}`}>
                    {` ${gap.id}`}
                  </Typography.Text>
                ) : null}
              </Checkbox>
            </div>
          );
        })}
        {canSave ? (
          <Button
            data-testid="ch-qr-gap-save"
            type="primary"
            loading={mutation.isPending}
            disabled={query.isLoading || query.isError}
            onClick={() => {
              void save();
            }}
          >
            {t('tenants.chQrGaps.save')}
          </Button>
        ) : null}
      </Space>
    </Card>
  );
}
