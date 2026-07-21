'use client';

/**
 * Dedicated Super Admin route for the multi-step create-tenant wizard.
 */
import { ArrowLeftOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button } from 'antd';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import React, { useCallback, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  applyTenantImpersonationSession,
  impersonateAdminTenant,
} from '@/features/super-admin/api/adminTenants';
import { CreateTenantWizard } from '@/features/super-admin/components/CreateTenantWizard';
import { ImpersonationRedirectOverlay } from '@/features/super-admin/components/ImpersonationRedirectOverlay';
import {
  ADMIN_TENANTS_QUERY_KEY,
  invalidateTenantLifecycleQueries,
} from '@/features/super-admin/utils/invalidateTenantLifecycleQueries';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

export default function SuperAdminCreateTenantPage() {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const { user } = useAuth();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [wizardOpen, setWizardOpen] = useState(true);
  const [impersonationRedirecting, setImpersonationRedirecting] = useState(false);

  const canAccess = isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);

  const invalidateTenants = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: ADMIN_TENANTS_QUERY_KEY });
    void invalidateTenantLifecycleQueries(queryClient);
  }, [queryClient]);

  const impersonateMutation = useMutation({
    mutationFn: (id: string) => impersonateAdminTenant(id),
    onSuccess: (res) => {
      setImpersonationRedirecting(true);
      applyTenantImpersonationSession(res);
    },
    onError: () => message.error(t('tenants.messages.impersonationFailed')),
  });

  const handleClose = useCallback(() => {
    setWizardOpen(false);
    router.push('/admin/tenants');
  }, [router]);

  const handleCreateAnother = useCallback(() => {
    setWizardOpen(true);
  }, []);

  if (!canAccess) {
    return (
      <AdminPageShell>
        <Alert
          type="error"
          title={t('tenants.accessDenied.title')}
          description={t('tenants.accessDenied.body')}
        />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('tenants.create.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
          { title: t('tenants.page.title'), href: '/admin/tenants' },
          { title: t('tenants.create.title'), href: '/admin/tenants/create' },
        ]}
        actions={
          <Link href="/admin/tenants">
            <Button icon={<ArrowLeftOutlined />}>{t('tenants.page.title')}</Button>
          </Link>
        }
      />

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('tenants.create.subtitle')}
      />

      <CreateTenantWizard
        open={wizardOpen}
        onClose={handleClose}
        onCreated={invalidateTenants}
        onCreateAnother={handleCreateAnother}
        onSwitchToTenant={(tenantId) => impersonateMutation.mutate(tenantId)}
        switchToTenantLoading={impersonateMutation.isPending}
      />

      {impersonationRedirecting ? <ImpersonationRedirectOverlay /> : null}
    </AdminPageShell>
  );
}
