'use client';

import { Alert, Button, Card, Descriptions, Form, Input, InputNumber, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';

import {
  useAdminFolio,
  useAdminFolioItems,
  useChargeAdminFolio,
  useCheckoutAdminFolio,
  type AdminFolioItem,
} from '@/api/admin/lodging';
import { useAmbientVerticalProfile } from '@/api/admin/vertical-profile';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

function stamp(value: string | null | undefined): string {
  return value ? value.slice(0, 19).replace('T', ' ') : '—';
}

export function FolioDetail({ folioId }: { folioId: string }) {
  const { t } = useI18n();
  const { modal } = useAntdApp();
  const notify = useNotify();
  const { user } = useAuth();
  const canWrite = hasPermission(user, PERMISSIONS.PRODUCT_MANAGE);
  const ambientProfile = useAmbientVerticalProfile();
  const isLodging = ambientProfile.data?.profileId === 'beherbergung';
  const folio = useAdminFolio(folioId, { enabled: isLodging || !ambientProfile.isFetched });
  const items = useAdminFolioItems(folioId, { enabled: isLodging || !ambientProfile.isFetched });
  const charge = useChargeAdminFolio();
  const checkout = useCheckoutAdminFolio();
  const [form] = Form.useForm<{ description: string; amount: number }>();

  if (ambientProfile.isFetched && !isLodging) {
    return <Alert type="info" showIcon title={t('admin.folios.unavailable')} />;
  }

  const row = folio.data;
  const columns: ColumnsType<AdminFolioItem> = [
    { title: t('admin.folios.colDescription'), dataIndex: 'description' },
    {
      title: t('admin.folios.colAmount'),
      dataIndex: 'amount',
      render: (value: number) => value.toFixed(2),
    },
    { title: t('admin.folios.colWhen'), dataIndex: 'createdAtUtc', render: stamp },
  ];

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Link href="/admin/folios">{t('admin.folios.back')}</Link>
      {folio.isError ? <Typography.Text type="danger">{t('admin.folios.loadError')}</Typography.Text> : null}
      {row ? (
        <Card title={row.customerName?.trim() || t('admin.folios.detailTitle')}>
          <Descriptions column={1}>
            <Descriptions.Item label={t('admin.folios.colRoom')}>
              <Link href={`/admin/rooms/${row.roomId}`}>{row.roomNumber || '—'}</Link>
            </Descriptions.Item>
            <Descriptions.Item label={t('admin.folios.colCheckIn')}>{stamp(row.checkIn)}</Descriptions.Item>
            <Descriptions.Item label={t('admin.folios.colCheckOut')}>{stamp(row.checkOut)}</Descriptions.Item>
            <Descriptions.Item label={t('admin.folios.colStatus')}>
              {row.isOpen ? t('admin.folios.open') : t('admin.folios.closed')}
            </Descriptions.Item>
            <Descriptions.Item label={t('admin.folios.balance')}>{row.balance.toFixed(2)}</Descriptions.Item>
          </Descriptions>
        </Card>
      ) : null}
      <Card title={t('admin.folios.charges')}>
        <Table<AdminFolioItem>
          rowKey="id"
          loading={items.isLoading}
          dataSource={items.data ?? []}
          columns={columns}
          pagination={false}
          locale={{ emptyText: t('admin.folios.emptyCharges') }}
        />
      </Card>
      {row?.isOpen && canWrite ? (
        <Card title={t('admin.folios.addCharge')}>
          <Form
            form={form}
            layout="inline"
            onFinish={(values) => {
              charge.mutate(
                { id: folioId, body: values },
                {
                  onSuccess: () => {
                    form.resetFields();
                    notify.success(t('admin.folios.chargeSuccess'));
                  },
                  onError: () => notify.error(t('admin.folios.chargeFailed')),
                }
              );
            }}
          >
            <Form.Item
              name="description"
              rules={[{ required: true, message: t('admin.folios.descriptionRequired') }]}
            >
              <Input aria-label={t('admin.folios.colDescription')} placeholder={t('admin.folios.colDescription')} />
            </Form.Item>
            <Form.Item
              name="amount"
              rules={[{ required: true, message: t('admin.folios.amountRequired') }]}
            >
              <InputNumber min={0.01} step={0.01} aria-label={t('admin.folios.colAmount')} />
            </Form.Item>
            <Form.Item>
              <Button htmlType="submit" type="primary" loading={charge.isPending}>
                {t('admin.folios.addCharge')}
              </Button>
            </Form.Item>
          </Form>
          <Alert style={{ marginTop: 16 }} type="info" showIcon title={t('admin.folios.checkoutHint')} />
          <Button
            style={{ marginTop: 16 }}
            danger
            loading={checkout.isPending}
            onClick={() => {
              modal.confirm({
                title: t('admin.folios.checkoutConfirm'),
                onOk: () =>
                  checkout.mutateAsync(folioId).then(
                    () => notify.success(t('admin.folios.checkoutSuccess')),
                    () => notify.error(t('admin.folios.checkoutFailed'))
                  ),
              });
            }}
          >
            {t('admin.folios.checkout')}
          </Button>
        </Card>
      ) : null}
    </Space>
  );
}
