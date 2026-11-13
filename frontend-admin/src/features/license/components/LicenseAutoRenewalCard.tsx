'use client';

import { Button, Card, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import { useState } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { createSupportTicket } from '@/features/support-tickets/api/supportTickets';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type Props = {
  validUntil?: string | null;
};

export function LicenseAutoRenewalCard({ validUntil }: Props) {
  const { t } = useI18n();
  const router = useRouter();
  const notify = useNotify();
  const { user } = useAuth();
  const [loading, setLoading] = useState(false);
  const superAdmin = isSuperAdmin(user?.role);

  const requestAutoRenew = async () => {
    setLoading(true);
    try {
      await createSupportTicket({
        category: 'License',
        priority: 'High',
        title: t('license.autoRenew.requestAutoRenew'),
        message: t('license.renewal.requestMessage', {
          validUntil: validUntil ?? '—',
        }),
      });
      notify.success(t('license.renewal.requestSuccess'));
    } catch (err) {
      notify.apiError(err, {
        logContext: 'LicenseAutoRenewalCard.request',
        fallbackKey: 'license.renewal.requestError',
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card title={t('license.autoRenew.title')} style={{ marginBottom: 16 }}>
      <Typography.Paragraph type="secondary">
        {t('license.autoRenew.subtitle')}
      </Typography.Paragraph>
      {superAdmin ? (
        <Button onClick={() => router.push('/admin/billing/subscription-invoices')}>
          {t('license.autoRenew.openInvoices')}
        </Button>
      ) : (
        <Button type="primary" loading={loading} onClick={() => void requestAutoRenew()}>
          {t('license.autoRenew.requestAutoRenew')}
        </Button>
      )}
    </Card>
  );
}
