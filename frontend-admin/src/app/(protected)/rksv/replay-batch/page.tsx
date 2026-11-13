'use client';

import { Button, Card, Form, Input, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import React, { useCallback, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

/**
 * Replay-Batch-Suche: Correlation-ID eingeben und Batch-Detail anzeigen (Incident-Debugging).
 * Metinler: `rksvHub.replayBatchSearch` (RKSV hub namespace).
 */
export default function ReplayBatchSearchPage() {
  const { t } = useI18n();
  const tr = useCallback((path: string) => t(`rksvHub.replayBatchSearch.${path}`), [t]);
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();

  const onFinish = (values: { correlationId: string }) => {
    const id = values.correlationId?.trim();
    if (!id) return;
    setLoading(true);
    router.push(`/rksv/replay-batch/${encodeURIComponent(id)}`);
    setLoading(false);
  };

  return (
    <>
      <AdminPageHeader
        title={tr('pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('adminShell.group.rksv'), href: '/rksv' },
          { title: tr('breadcrumb') },
        ]}
      />
      <Card>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
          {tr('intro')}
        </Typography.Paragraph>
        <Form form={form} layout="inline" onFinish={onFinish}>
          <Form.Item
            name="correlationId"
            label={tr('formLabel')}
            rules={[{ required: true, message: tr('formRuleRequired') }]}
            style={{ minWidth: 320 }}
          >
            <Input placeholder={tr('inputPlaceholder')} allowClear />
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={loading}>
              {tr('submit')}
            </Button>
          </Form.Item>
        </Form>
      </Card>
    </>
  );
}
