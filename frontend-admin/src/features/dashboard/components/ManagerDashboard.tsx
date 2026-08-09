'use client';

import { Alert, Button, Card, Space } from 'antd';
import Link from 'next/link';

import { CashRegisterSelector } from '@/components/CashRegisterSelector';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { Dashboard } from '@/features/dashboard/components/Dashboard';
import { usePendingMonatsbeleg } from '@/features/rksv/hooks/usePendingMonatsbeleg';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { useFormattedDate } from '@/hooks/useFormattedDate';
import { useI18n } from '@/i18n/I18nProvider';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';

function resolveUserDisplayName(
  firstName?: string | null,
  lastName?: string | null,
  userName?: string | null
): string {
  const fullName = [firstName, lastName].filter(Boolean).join(' ').trim();
  if (fullName) {
    return fullName;
  }
  return userName?.trim() || 'Manager';
}

function formatRegisterLabel(
  fallback: string,
  registerNumber?: string | null,
  location?: string | null
): string {
  const number = registerNumber?.trim();
  const place = location?.trim();
  if (number && place) {
    return `${number} — ${place}`;
  }
  return number || place || fallback;
}

/**
 * Mandanten-Admin home: welcome + cash register selector, optional Monatsbeleg alert,
 * then the preference-driven sortable Dashboard widget grid (Handlungsbedarf / Lizenz / …).
 */
export function ManagerDashboard() {
  const { t } = useI18n();
  const { format: formatLocalizedDate } = useFormattedDate();
  const { user } = useAuth();

  const { selectedRegister, selectedRegisterId, setSelectedRegisterId, hasMultipleRegisters } =
    useCashRegisterSelection({
      autoSelect: true,
      persistSelection: true,
    });

  const { data: pendingMonatsbeleg = [] } = usePendingMonatsbeleg();

  const userName = resolveUserDisplayName(user?.firstName, user?.lastName, user?.userName);
  const noRegisterLabel = t('dashboard.manager.noRegister');
  const registerLabel = selectedRegister
    ? formatRegisterLabel(
        noRegisterLabel,
        selectedRegister.registerNumber,
        selectedRegister.location
      )
    : noRegisterLabel;
  const todayLabel = formatLocalizedDate(new Date(), 'medium');
  const pendingMonatsbelegCount = pendingMonatsbeleg.length;
  const pendingMonatsbelegTitle =
    pendingMonatsbelegCount === 1
      ? t('dashboard.manager.pendingMonatsbeleg', { count: pendingMonatsbelegCount })
      : t('dashboard.manager.pendingMonatsbelegPlural', { count: pendingMonatsbelegCount });

  return (
    <div style={{ padding: 24 }}>
      <Card style={{ marginBottom: 16, background: '#f8fafc' }} variant="borderless">
        <Space orientation="vertical" size={8} style={{ width: '100%' }}>
          <h2 style={{ margin: 0 }}>{t('dashboard.manager.welcome', { name: userName })}</h2>
          <p style={{ color: '#64748b', margin: 0 }}>
            {registerLabel} — {todayLabel}
          </p>
          <CashRegisterSelector
            value={selectedRegisterId}
            onChange={setSelectedRegisterId}
            required
            autoSelect
            showFormItem={false}
            style={{ maxWidth: hasMultipleRegisters ? 360 : '100%' }}
          />
        </Space>
      </Card>

      {pendingMonatsbelegCount > 0 ? (
        <Alert
          title={pendingMonatsbelegTitle}
          description={t('dashboard.manager.pendingMonatsbelegDescription')}
          type="warning"
          showIcon
          action={
            <Link href={RKSV_SONDERBELEGE_PATH}>
              <Button size="small" type="primary">
                {t('dashboard.manager.createNow')}
              </Button>
            </Link>
          }
          style={{ marginBottom: 16 }}
        />
      ) : null}

      <Dashboard />
    </div>
  );
}
