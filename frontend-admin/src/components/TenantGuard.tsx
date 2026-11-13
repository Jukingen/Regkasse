'use client';

import { Alert, Button, Modal, Select, Space, Spin, Typography } from 'antd';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { isDevelopment } from '@/features/auth/services/devTenant';
import {
  isPathAllowedWithoutTenant,
  useSuperAdminTenantMode,
} from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { switchDevTenantContext } from '@/features/tenancy/services/setTenantAndRefresh';
import { buildTenantSelectorLabel } from '@/features/super-admin/utils/tenantSelectorLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useTenant } from '@/hooks/useTenant';
import { useI18n } from '@/i18n';

export type TenantGuardProps = {
  children: ReactNode;
};

/**
 * Blocks mandant-scoped pages until Super Admin selects a tenant.
 * Selection rebinds JWT `tenant_id`, persists slug/id, and reloads so all pages share the context.
 * Platform routes (`/admin/tenants`, `/admin/digital`, …) stay reachable without a mandant.
 *
 * Development: auto-selects seeded `dev` (no picker modal) — tenant switcher remains for Super Admin overrides.
 * Production/Staging: JWT tenant after login; picker only when Super Admin has no mandant context.
 */
export function TenantGuard({ children }: TenantGuardProps) {
  const pathname = usePathname();
  const { t } = useI18n();
  const { message } = useAntdApp();
  const { refreshToken } = useAuth();
  const { requiresTenantSelection } = useSuperAdminTenantMode();
  const needsTenantPicker =
    requiresTenantSelection && !isPathAllowedWithoutTenant(pathname);
  const { setTenant, refresh, tenants, tenantsLoading } = useTenant({
    loadTenants: needsTenantPicker,
  });

  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [devAutoSelectFailed, setDevAutoSelectFailed] = useState(false);
  const autoSelectAttemptedRef = useRef(false);

  const selectOptions = useMemo(
    () =>
      tenants.map((row) => ({
        value: row.id,
        label: buildTenantSelectorLabel(row, t),
      })),
    [tenants, t]
  );

  const applyTenant = useCallback(
    async (row: (typeof tenants)[number], options?: { fromDevAutoSelect?: boolean }) => {
      setSubmitting(true);
      try {
        const licenseValidUntilUtc = row.licenseValidUntilUtc ?? null;
        const licenseValid = Boolean(
          licenseValidUntilUtc && new Date(licenseValidUntilUtc).getTime() > Date.now()
        );
        setTenant({
          id: row.id,
          slug: row.slug,
          name: row.name,
          licenseValid,
          licenseValidUntilUtc,
        });

        const tokenOk = await refreshToken(row.id);
        if (!tokenOk) {
          message.error(t('adminShell.tenant.devSwitcher.refreshFailed'));
          setSubmitting(false);
          if (options?.fromDevAutoSelect) {
            setDevAutoSelectFailed(true);
          }
          return;
        }

        refresh();
        await switchDevTenantContext({ slug: row.slug, id: row.id });
      } catch {
        message.error(t('adminShell.tenant.guard.applyFailed'));
        setSubmitting(false);
        if (options?.fromDevAutoSelect) {
          setDevAutoSelectFailed(true);
        }
      }
    },
    [message, refresh, refreshToken, setTenant, t]
  );

  const applySelectedTenant = useCallback(async () => {
    const row = tenants.find((tenant) => tenant.id === selectedTenantId);
    if (!row) {
      return;
    }
    await applyTenant(row);
  }, [applyTenant, selectedTenantId, tenants]);

  // In development, auto-select `dev` tenant (skip picker modal).
  useEffect(() => {
    if (!isDevelopment() || !needsTenantPicker) {
      return;
    }
    if (autoSelectAttemptedRef.current || tenantsLoading) {
      return;
    }
    if (tenants.length === 0) {
      setDevAutoSelectFailed(true);
      return;
    }

    const preferred =
      tenants.find((row) => row.slug === 'dev' && row.isActive) ??
      tenants.find((row) => row.isActive) ??
      tenants[0];
    if (!preferred) {
      setDevAutoSelectFailed(true);
      return;
    }

    autoSelectAttemptedRef.current = true;
    void applyTenant(preferred, { fromDevAutoSelect: true });
  }, [applyTenant, needsTenantPicker, tenants, tenantsLoading]);

  if (!requiresTenantSelection) {
    return <>{children}</>;
  }

  if (isPathAllowedWithoutTenant(pathname)) {
    return <>{children}</>;
  }

  // Development: auto-select in flight — show a light spinner instead of the picker.
  // Fall back to the picker if the list is empty or auto-select failed.
  if (isDevelopment() && !devAutoSelectFailed && (tenantsLoading || tenants.length > 0)) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin description={t('adminShell.tenant.guard.devAutoSelecting')} />
      </div>
    );
  }

  return (
    <Modal
      title={t('adminShell.tenant.guard.title')}
      open
      closable={false}
      mask={{ closable: false }}
      keyboard={false}
      destroyOnHidden
      footer={
        <Space wrap>
          <Link href="/admin/tenants">
            <Button disabled={submitting}>{t('adminShell.tenant.superAdminPromptAction')}</Button>
          </Link>
          <Button
            type="primary"
            loading={submitting}
            disabled={!selectedTenantId || tenantsLoading}
            onClick={() => void applySelectedTenant()}
          >
            {t('adminShell.tenant.guard.confirm')}
          </Button>
        </Space>
      }
    >
      <Space orientation="vertical" size={12} style={{ width: '100%' }}>
        <Alert type="info" showIcon title={t('adminShell.tenant.guard.body')} />
        {tenantsLoading ? (
          <div style={{ display: 'flex', justifyContent: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : (
          <>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
              {t('adminShell.tenant.selectTenantFirstBody')}
            </Typography.Paragraph>
            <Select
              showSearch
              allowClear
              style={{ width: '100%' }}
              placeholder={t('superadmin.selectorPlaceholder')}
              options={selectOptions}
              value={selectedTenantId}
              onChange={(value) => setSelectedTenantId(value ?? null)}
              optionFilterProp="label"
              aria-label={t('adminShell.tenant.guard.title')}
            />
          </>
        )}
      </Space>
    </Modal>
  );
}
