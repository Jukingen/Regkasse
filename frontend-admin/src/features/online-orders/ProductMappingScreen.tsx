'use client';

import { Button, Form, Input, Select, Table, Typography } from 'antd';
import { useState } from 'react';

import {
  getGetApiAdminOnlineOrderIntegrationsProductMappingsQueryKey,
  useGetApiAdminOnlineOrderIntegrationsProductMappings,
  useGetApiAdminProducts,
  usePutApiAdminOnlineOrderIntegrationsProductMappings,
} from '@/api/generated/admin/admin';
import type { OnlineOrderProductMappingDto } from '@/api/generated/model/onlineOrderProductMappingDto';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useQueryClient } from '@tanstack/react-query';

import { ProviderConnectSection } from './ProviderConnectSection';
import { ProviderSettingsSection } from './ProviderSettingsSection';

type MappingForm = {
  provider: string;
  externalProductId: string;
  productId?: string;
};

export function ProductMappingScreen() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<MappingForm>();
  const [provider, setProvider] = useState('deliverect');

  const mappings = useGetApiAdminOnlineOrderIntegrationsProductMappings(
    { provider: provider.trim() },
    { query: { enabled: provider.trim().length > 0 } }
  );
  const products = useGetApiAdminProducts({ Page: 1, PageSize: 100, isActive: 'true' });
  const save = usePutApiAdminOnlineOrderIntegrationsProductMappings({
    mutation: {
      onSuccess: async () => {
        notify.successKey('onlineOrders.productMappings.saved');
        form.setFieldsValue({ externalProductId: '', productId: undefined });
        await queryClient.invalidateQueries({
          queryKey: getGetApiAdminOnlineOrderIntegrationsProductMappingsQueryKey({
            provider: provider.trim(),
          }),
        });
      },
      onError: (error) => {
        notify.apiError(error, {
          logContext: 'OnlineOrderProductMappings.save',
          fallbackKey: 'onlineOrders.productMappings.saveFailed',
        });
      },
    },
  });

  const productOptions = (products.data?.items ?? []).flatMap((product) =>
    product.id ? [{ value: product.id, label: product.name || product.id }] : []
  );

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('onlineOrders.productMappings.pageTitle')}
        subtitle={t('onlineOrders.productMappings.pageSubtitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.settingsHub'), href: '/settings' },
          { title: t('onlineOrders.productMappings.pageTitle') },
        ]}
      />
      <ProviderSettingsSection />
      <ProviderConnectSection />
      <Form<MappingForm>
        form={form}
        layout="vertical"
        initialValues={{ provider: 'deliverect' }}
        onValuesChange={(changed) => {
          if (typeof changed.provider === 'string') setProvider(changed.provider);
        }}
        onFinish={(values) => {
          if (!values.productId) return;
          save.mutate({
            data: {
              provider: values.provider.trim(),
              externalProductId: values.externalProductId.trim(),
              productId: values.productId,
            },
          });
        }}
      >
        <Form.Item
          name="provider"
          label={t('onlineOrders.productMappings.provider')}
          rules={[{ required: true, whitespace: true, max: 32 }]}
        >
          <Input maxLength={32} />
        </Form.Item>
        <Form.Item
          name="externalProductId"
          label={t('onlineOrders.productMappings.externalProductId')}
          rules={[{ required: true, whitespace: true, max: 128 }]}
        >
          <Input maxLength={128} />
        </Form.Item>
        <Form.Item
          name="productId"
          label={t('onlineOrders.productMappings.product')}
          rules={[{ required: true }]}
        >
          <Select
            showSearch
            optionFilterProp="label"
            options={productOptions}
            loading={products.isLoading}
          />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>
          {t('onlineOrders.productMappings.save')}
        </Button>
      </Form>
      {mappings.isError ? (
        <Typography.Paragraph type="danger">
          {t('onlineOrders.productMappings.loadFailed')}
        </Typography.Paragraph>
      ) : null}
      <Table<OnlineOrderProductMappingDto>
        rowKey={(row) => row.id || `${row.provider}-${row.externalProductId}`}
        loading={mappings.isLoading}
        dataSource={mappings.data?.items ?? []}
        locale={{ emptyText: t('onlineOrders.productMappings.empty') }}
        pagination={false}
        columns={[
          {
            title: t('onlineOrders.productMappings.columns.provider'),
            dataIndex: 'provider',
          },
          {
            title: t('onlineOrders.productMappings.columns.externalProductId'),
            dataIndex: 'externalProductId',
          },
          {
            title: t('onlineOrders.productMappings.columns.product'),
            render: (_, row) => row.productName || row.productId,
          },
        ]}
      />
    </AdminPageShell>
  );
}
