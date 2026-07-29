'use client';

import { Checkbox, Card, Flex, List, Progress, Typography } from 'antd';
import { useEffect, useMemo, useState } from 'react';

import {
  LICENSE_RENEWAL_CHECKLIST_ITEM_IDS,
  type LicenseRenewalChecklistItemId,
  getLicenseRenewalChecklistProgressPercent,
  loadLicenseRenewalChecklistCompleted,
  saveLicenseRenewalChecklistCompleted,
  toggleLicenseRenewalChecklistItem,
} from '@/features/license/utils/licenseRenewalChecklist';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { FORMAT_EMPTY_DISPLAY, formatDate, useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

type ChecklistRow = {
  id: LicenseRenewalChecklistItemId;
  label: string;
  description?: string;
  completed: boolean;
};

/**
 * Mandant renewal preparation checklist (persisted per tenant in localStorage).
 */
export function LicenseRenewalChecklistCard() {
  const { t, formatLocale } = useI18n();
  const tenant = useCurrentTenant();
  const { status, isLoading } = useLicenseStatus();
  const { isAuthorized: canView } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_VIEW,
  });
  const [completed, setCompleted] = useState<Set<LicenseRenewalChecklistItemId>>(
    () => new Set()
  );

  useEffect(() => {
    if (!tenant.tenantId) {
      setCompleted(new Set());
      return;
    }
    setCompleted(loadLicenseRenewalChecklistCompleted(tenant.tenantId));
  }, [tenant.tenantId]);

  const validUntilLabel = status?.expiredAt
    ? formatDate(status.expiredAt, formatLocale)
    : FORMAT_EMPTY_DISPLAY;

  const rows = useMemo<ChecklistRow[]>(() => {
    return LICENSE_RENEWAL_CHECKLIST_ITEM_IDS.map((id) => {
      const label = t(`dashboard.widgets.licenseRenewalChecklist.items.${id}.label`);
      const description =
        id === 'reviewLicenseData'
          ? t('dashboard.widgets.licenseRenewalChecklist.items.reviewLicenseData.description', {
              date: validUntilLabel,
            })
          : undefined;

      return {
        id,
        label,
        description,
        completed: completed.has(id),
      };
    });
  }, [completed, t, validUntilLabel]);

  if (!canView || isLoading) return null;
  if (!tenant.isRealTenantSlug || tenant.isSuperAdminPlatformMode || !tenant.tenantId) {
    return null;
  }

  const totalItems = rows.length;
  const completedItems = rows.filter((r) => r.completed).length;
  const percent = getLicenseRenewalChecklistProgressPercent(completedItems, totalItems);

  const toggleItem = (id: LicenseRenewalChecklistItemId) => {
    setCompleted((prev) => {
      const next = toggleLicenseRenewalChecklistItem(prev, id);
      saveLicenseRenewalChecklistCompleted(tenant.tenantId!, next);
      return next;
    });
  };

  return (
    <Card
      size="small"
      title={t('dashboard.widgets.licenseRenewalChecklist.title')}
      style={{ marginBottom: 16 }}
      styles={{ body: { paddingBlock: 16 } }}
    >
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('dashboard.widgets.licenseRenewalChecklist.subtitle')}
      </Typography.Paragraph>

      <List
        dataSource={rows}
        split
        renderItem={(item) => (
          <List.Item style={{ paddingInline: 0 }}>
            <Flex align="flex-start" gap={12} style={{ width: '100%' }}>
              <Checkbox
                checked={item.completed}
                onChange={() => toggleItem(item.id)}
                aria-label={item.label}
              />
              <div style={{ flex: 1, minWidth: 0 }}>
                <Typography.Text
                  style={
                    item.completed
                      ? { textDecoration: 'line-through', color: 'rgba(0,0,0,0.45)' }
                      : undefined
                  }
                >
                  {item.label}
                </Typography.Text>
                {item.description ? (
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginBottom: 0, marginTop: 2, fontSize: 12 }}
                  >
                    {item.description}
                  </Typography.Paragraph>
                ) : null}
              </div>
            </Flex>
          </List.Item>
        )}
      />

      <Flex align="center" justify="space-between" gap={16} wrap="wrap" style={{ marginTop: 16 }}>
        <Typography.Text type="secondary" style={{ fontSize: 13 }}>
          {t('dashboard.widgets.licenseRenewalChecklist.progressLabel', {
            completed: completedItems,
            total: totalItems,
          })}
        </Typography.Text>
        <Progress
          percent={percent}
          size="small"
          style={{ width: 128, marginBottom: 0 }}
          aria-label={t('dashboard.widgets.licenseRenewalChecklist.progressAria', {
            percent,
          })}
        />
      </Flex>
    </Card>
  );
}
