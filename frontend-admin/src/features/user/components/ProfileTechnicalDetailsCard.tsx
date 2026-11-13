'use client';

import { Card, Collapse, Descriptions, Typography } from 'antd';
import { useMemo } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { readQuickCashRegisterId } from '@/features/cash-registers/constants/quickSwitch';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';

export type ProfileTechnicalDetailsCardProps = {
  /** Authoritative user id from GET /api/user/profile (preferred over auth snapshot). */
  userId: string;
};

function CopyableId({ value }: { value: string }) {
  return (
    <Typography.Text
      code
      copyable={{ text: value }}
      style={{ display: 'inline-block', wordBreak: 'break-all', whiteSpace: 'normal' }}
    >
      {value}
    </Typography.Text>
  );
}

function displayOrDash(value: string | null | undefined, emptyLabel: string): string {
  const trimmed = value?.trim();
  return trimmed ? trimmed : emptyLabel;
}

/**
 * Rare technical identifiers for support / debugging (full values, copyable, no truncation).
 * Placed on /profile so operators can reach user/tenant/register ids without digging through network tabs.
 */
export function ProfileTechnicalDetailsCard({ userId }: ProfileTechnicalDetailsCardProps) {
  const { t } = useI18n();
  const { user } = useAuth();
  const { tenantId, tenantSlug, requiresTenantSelection } = useCurrentTenant();

  const resolvedUserId = userId.trim() || user?.id?.trim() || '';
  const resolvedTenantId = tenantId?.trim() || user?.tenantId?.trim() || '';
  const resolvedTenantSlug = tenantSlug?.trim() || user?.tenantSlug?.trim() || '';
  const branchId = user?.branchId?.trim() || '';
  const appContext = user?.appContext?.trim() || '';
  const roles = useMemo(() => {
    const fromRoles = (user?.roles ?? []).map((r) => r.trim()).filter(Boolean);
    if (fromRoles.length > 0) return fromRoles.join(', ');
    return user?.role?.trim() || '';
  }, [user?.role, user?.roles]);

  const selectedCashRegisterId = useMemo(() => {
    if (requiresTenantSelection || !resolvedTenantId) return '';
    return readQuickCashRegisterId(resolvedTenantId)?.trim() || '';
  }, [requiresTenantSelection, resolvedTenantId]);

  const emptyLabel = t('profile.emptyValue');

  return (
    <Card variant="borderless">
      <Collapse
        ghost
        defaultActiveKey={['tech']}
        items={[
          {
            key: 'tech',
            label: t('profile.technicalDetails.title'),
            children: (
              <>
                <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                  {t('profile.technicalDetails.description')}
                </Typography.Paragraph>
                <Descriptions bordered column={1} size="small">
                  <Descriptions.Item label={t('profile.technicalDetails.userId')}>
                    {resolvedUserId ? (
                      <CopyableId value={resolvedUserId} />
                    ) : (
                      emptyLabel
                    )}
                  </Descriptions.Item>
                  {!requiresTenantSelection && resolvedTenantId ? (
                    <Descriptions.Item label={t('profile.technicalDetails.tenantId')}>
                      <CopyableId value={resolvedTenantId} />
                    </Descriptions.Item>
                  ) : null}
                  {!requiresTenantSelection && resolvedTenantSlug ? (
                    <Descriptions.Item label={t('profile.technicalDetails.tenantSlug')}>
                      <CopyableId value={resolvedTenantSlug} />
                    </Descriptions.Item>
                  ) : null}
                  {branchId ? (
                    <Descriptions.Item label={t('profile.technicalDetails.branchId')}>
                      <CopyableId value={branchId} />
                    </Descriptions.Item>
                  ) : null}
                  {selectedCashRegisterId ? (
                    <Descriptions.Item label={t('profile.technicalDetails.selectedCashRegisterId')}>
                      <CopyableId value={selectedCashRegisterId} />
                    </Descriptions.Item>
                  ) : null}
                  <Descriptions.Item label={t('profile.technicalDetails.backendRole')}>
                    {displayOrDash(roles, emptyLabel)}
                  </Descriptions.Item>
                  {appContext ? (
                    <Descriptions.Item label={t('profile.technicalDetails.appContext')}>
                      {appContext}
                    </Descriptions.Item>
                  ) : null}
                </Descriptions>
              </>
            ),
          },
        ]}
      />
    </Card>
  );
}
