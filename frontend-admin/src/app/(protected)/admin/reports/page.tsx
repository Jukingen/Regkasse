'use client';

import { TeamOutlined } from '@ant-design/icons';
import { Card, Col, Row, Typography } from 'antd';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Suspense } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { CashRegisterReportsWorkspace } from '@/features/cash-registers/components/CashRegisterReportsWorkspace';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

const { Paragraph, Text } = Typography;

function AdminReportsHubContent() {
  const { t } = useI18n();
  const searchParams = useSearchParams();
  const registerId = searchParams.get('registerId')?.trim() || undefined;

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('cashRegisters.reports.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('cashRegisters.reports.title') }]}
      />
      <Paragraph type="secondary">{t('cashRegisters.reports.intro')}</Paragraph>
      <CashRegisterReportsWorkspace initialRegisterId={registerId} />
      <Row gutter={[16, 16]} style={{ marginTop: 24 }}>
        <Col xs={24} sm={12} lg={8}>
          <Link href="/admin/reports/user-activity" style={{ display: 'block' }}>
            <Card hoverable>
              <TeamOutlined style={{ fontSize: 28, color: '#1677ff' }} />
              <Paragraph strong style={{ marginTop: 12, marginBottom: 4 }}>
                {t('cashRegisters.reports.userActivityCard')}
              </Paragraph>
              <Text type="secondary">{t('reporting.userActivity.pageIntro')}</Text>
            </Card>
          </Link>
        </Col>
      </Row>
    </AdminPageShell>
  );
}

export default function AdminReportsHubPage() {
  return (
    <Suspense>
      <AdminReportsHubContent />
    </Suspense>
  );
}
