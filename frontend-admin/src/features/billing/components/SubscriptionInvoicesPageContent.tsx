'use client';

import { FilePdfOutlined, ReloadOutlined, ThunderboltOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button,
  DatePicker,
  Descriptions,
  Drawer,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import React, { useMemo, useState } from 'react';

import { dateColumnRender, DateColumn } from '@/components/DateColumn';
import { EmptyState } from '@/components/EmptyState';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import {
  downloadSubscriptionInvoicePdf,
  generateMonthlySubscriptionInvoices,
  listSubscriptionInvoices,
  markSubscriptionInvoicePaid,
  type SubscriptionInvoiceDto,
  voidSubscriptionInvoice,
} from '@/features/billing/api/subscriptionInvoicesApi';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { formatCurrency, useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

const { RangePicker } = DatePicker;

type PaidFormValues = {
  paidAt?: Dayjs;
  paymentMethod?: string;
  reference?: string;
};

type VoidFormValues = {
  reason?: string;
};

function statusColor(status: string): string {
  switch (status) {
    case 'paid':
      return 'green';
    case 'void':
      return 'default';
    case 'draft':
      return 'gold';
    default:
      return 'blue';
  }
}

export function SubscriptionInvoicesPageContent() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const canAccess = useBillingAccess();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<string | undefined>();
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [pdfLoadingId, setPdfLoadingId] = useState<string | null>(null);
  const [detail, setDetail] = useState<SubscriptionInvoiceDto | null>(null);
  const [paidTarget, setPaidTarget] = useState<SubscriptionInvoiceDto | null>(null);
  const [voidTarget, setVoidTarget] = useState<SubscriptionInvoiceDto | null>(null);
  const [paidForm] = Form.useForm<PaidFormValues>();
  const [voidForm] = Form.useForm<VoidFormValues>();

  const listParams = useMemo(
    () => ({
      page: 1,
      pageSize: 100,
      status,
      tenantId,
      fromUtc: range?.[0]?.startOf('day').toISOString(),
      toUtc: range?.[1]?.endOf('day').toISOString(),
    }),
    [status, tenantId, range]
  );

  const listQuery = useQuery({
    queryKey: ['admin-subscription-invoices', listParams],
    queryFn: () => listSubscriptionInvoices(listParams),
    enabled: canAccess,
  });

  const tenantsQuery = useQuery({
    queryKey: ['admin-tenants', 'subscription-invoices-filter'],
    queryFn: () => listAdminTenants(false),
    enabled: canAccess,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['admin-subscription-invoices'] });
  };

  const generateMutation = useMutation({
    mutationFn: generateMonthlySubscriptionInvoices,
    onSuccess: (result) => {
      notify.success(
        t('billing.subscriptionInvoices.generateSuccess', {
          created: result.created,
          skipped: result.skipped,
          failed: result.failed,
        })
      );
      invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'SubscriptionInvoices.generateMonthly',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const markPaidMutation = useMutation({
    mutationFn: ({ id, values }: { id: string; values: PaidFormValues }) =>
      markSubscriptionInvoicePaid(id, {
        paidAt: values.paidAt?.toISOString() ?? null,
        paymentMethod: values.paymentMethod ?? 'bank_transfer',
        reference: values.reference ?? null,
      }),
    onSuccess: () => {
      notify.success(t('billing.subscriptionInvoices.markPaidSuccess'));
      setPaidTarget(null);
      paidForm.resetFields();
      invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'SubscriptionInvoices.markPaid',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const voidMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      voidSubscriptionInvoice(id, { reason }),
    onSuccess: () => {
      notify.success(t('billing.subscriptionInvoices.voidSuccess'));
      setVoidTarget(null);
      voidForm.resetFields();
      invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'SubscriptionInvoices.void',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const tenantOptions = useMemo(
    () =>
      (tenantsQuery.data ?? []).map((tenant) => ({
        value: tenant.id,
        label: `${tenant.name} (${tenant.slug})`,
      })),
    [tenantsQuery.data]
  );

  const statusLabel = (value: string) => {
    switch (value) {
      case 'paid':
        return t('billing.subscriptionInvoices.statusPaid');
      case 'void':
        return t('billing.subscriptionInvoices.statusVoid');
      case 'draft':
        return t('billing.subscriptionInvoices.statusDraft');
      default:
        return t('billing.subscriptionInvoices.statusIssued');
    }
  };

  const columns: ColumnsType<SubscriptionInvoiceDto> = [
    {
      title: t('billing.subscriptionInvoices.invoiceNumber'),
      dataIndex: 'invoiceNumber',
      ellipsis: true,
    },
    {
      title: t('billing.subscriptionInvoices.tenant'),
      key: 'tenant',
      render: (_, row) => `${row.tenantName} (${row.tenantSlug})`,
    },
    {
      title: t('billing.subscriptionInvoices.period'),
      key: 'period',
      render: (_, row) => (
        <span>
          <DateColumn date={row.periodStartUtc} format="short" /> –{' '}
          <DateColumn date={row.periodEndUtc} format="short" />
        </span>
      ),
    },
    {
      title: t('billing.subscriptionInvoices.amount'),
      dataIndex: 'amountGross',
      render: (value: number, row) =>
        formatCurrency(value, formatLocale, { currency: row.currency }),
    },
    {
      title: t('billing.subscriptionInvoices.status'),
      dataIndex: 'status',
      width: 120,
      render: (value: string) => <Tag color={statusColor(value)}>{statusLabel(value)}</Tag>,
    },
    {
      title: t('billing.subscriptionInvoices.issuedAt'),
      dataIndex: 'issuedAtUtc',
      width: 160,
      render: dateColumnRender('datetime'),
    },
    {
      title: t('billing.subscriptionInvoices.actions'),
      key: 'actions',
      width: 280,
      render: (_, row) => (
        <Space wrap>
          <Button type="link" onClick={() => setDetail(row)}>
            {t('billing.subscriptionInvoices.view')}
          </Button>
          <Button
            type="link"
            icon={<FilePdfOutlined />}
            loading={pdfLoadingId === row.id}
            onClick={async () => {
              setPdfLoadingId(row.id);
              try {
                await downloadSubscriptionInvoicePdf(row.id, `${row.invoiceNumber}.pdf`);
              } catch (err) {
                notify.apiError(err, {
                  logContext: 'SubscriptionInvoices.downloadPdf',
                  fallbackKey: 'common.errorGeneric',
                });
              } finally {
                setPdfLoadingId(null);
              }
            }}
          >
            {t('billing.subscriptionInvoices.downloadPdf')}
          </Button>
          {row.status === 'issued' || row.status === 'draft' ? (
            <>
              <Button type="link" onClick={() => setPaidTarget(row)}>
                {t('billing.subscriptionInvoices.markPaid')}
              </Button>
              <Button type="link" danger onClick={() => setVoidTarget(row)}>
                {t('billing.subscriptionInvoices.void')}
              </Button>
            </>
          ) : null}
        </Space>
      ),
    },
  ];

  return (
    <BillingAccessGate>
      <AdminPageShell>
        <AdminPageHeader
          title={t('billing.subscriptionInvoices.title')}
          subtitle={t('billing.subscriptionInvoices.subtitle')}
          breadcrumbs={[
            adminOverviewCrumb(t),
            { title: t('nav.licenseManagement'), href: '/admin/license-management' },
            { title: t('nav.subscriptionInvoices') },
          ]}
          actions={
            <Space wrap>
              <Button
                icon={<ThunderboltOutlined />}
                onClick={() => generateMutation.mutate()}
                loading={generateMutation.isPending}
              >
                {t('billing.subscriptionInvoices.generateMonthly')}
              </Button>
              <Button icon={<ReloadOutlined />} onClick={() => void listQuery.refetch()}>
                {t('billing.subscriptionInvoices.refresh')}
              </Button>
            </Space>
          }
        />

        <Space wrap style={{ marginBottom: 16 }}>
          <Select
            allowClear
            placeholder={t('billing.subscriptionInvoices.allStatus')}
            style={{ minWidth: 160 }}
            value={status}
            onChange={(value) => setStatus(value)}
            options={[
              { value: 'issued', label: t('billing.subscriptionInvoices.statusIssued') },
              { value: 'paid', label: t('billing.subscriptionInvoices.statusPaid') },
              { value: 'void', label: t('billing.subscriptionInvoices.statusVoid') },
              { value: 'draft', label: t('billing.subscriptionInvoices.statusDraft') },
            ]}
          />
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('billing.subscriptionInvoices.allTenants')}
            style={{ minWidth: 240 }}
            value={tenantId}
            onChange={(value) => setTenantId(value)}
            options={tenantOptions}
          />
          <RangePicker
            value={range}
            onChange={(next) => setRange(next)}
          />
        </Space>

        <Table<SubscriptionInvoiceDto>
          rowKey="id"
          loading={listQuery.isLoading || listQuery.isFetching}
          columns={columns}
          dataSource={listQuery.data ?? []}
          pagination={{ pageSize: 20 }}
          locale={{
            emptyText: (
              <EmptyState title={t('billing.subscriptionInvoices.noResults')} />
            ),
          }}
        />

        <Drawer
          title={t('billing.subscriptionInvoices.detailTitle')}
          open={detail != null}
          onClose={() => setDetail(null)}
          destroyOnHidden
          width={480}
        >
          {detail ? (
            <Descriptions column={1} size="small">
              <Descriptions.Item label={t('billing.subscriptionInvoices.invoiceNumber')}>
                {detail.invoiceNumber}
              </Descriptions.Item>
              <Descriptions.Item label={t('billing.subscriptionInvoices.tenant')}>
                {detail.tenantName} ({detail.tenantSlug})
              </Descriptions.Item>
              <Descriptions.Item label={t('billing.subscriptionInvoices.amount')}>
                {formatCurrency(detail.amountGross, formatLocale, { currency: detail.currency })}
              </Descriptions.Item>
              <Descriptions.Item label={t('billing.subscriptionInvoices.status')}>
                {statusLabel(detail.status)}
              </Descriptions.Item>
              <Descriptions.Item label={t('billing.subscriptionInvoices.paidAt')}>
                {detail.paidAtUtc ? dateColumnRender('datetime')(detail.paidAtUtc) : '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('billing.subscriptionInvoices.paymentMethod')}>
                {detail.paymentMethod ?? '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('billing.subscriptionInvoices.reference')}>
                {detail.paymentReference ?? '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('billing.subscriptionInvoices.voidReason')}>
                {detail.voidReason ?? '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('billing.subscriptionInvoices.emailSentAt')}>
                {detail.emailSentAtUtc ? dateColumnRender('datetime')(detail.emailSentAtUtc) : '—'}
              </Descriptions.Item>
            </Descriptions>
          ) : null}
        </Drawer>

        <Modal
          title={t('billing.subscriptionInvoices.markPaid')}
          open={paidTarget != null}
          onCancel={() => {
            setPaidTarget(null);
            paidForm.resetFields();
          }}
          onOk={() => paidForm.submit()}
          confirmLoading={markPaidMutation.isPending}
          destroyOnHidden
        >
          <Form
            form={paidForm}
            layout="vertical"
            onFinish={(values) => {
              if (!paidTarget) return;
              markPaidMutation.mutate({ id: paidTarget.id, values });
            }}
          >
            <Form.Item name="paidAt" label={t('billing.subscriptionInvoices.paidAt')}>
              <DatePicker showTime style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item
              name="paymentMethod"
              label={t('billing.subscriptionInvoices.paymentMethod')}
              initialValue="bank_transfer"
            >
              <Select
                options={[
                  { value: 'bank_transfer', label: t('billing.subscriptionInvoices.paymentMethodBank') },
                  { value: 'card', label: t('billing.subscriptionInvoices.paymentMethodCard') },
                  { value: 'cash', label: t('billing.subscriptionInvoices.paymentMethodCash') },
                ]}
              />
            </Form.Item>
            <Form.Item name="reference" label={t('billing.subscriptionInvoices.reference')}>
              <Input maxLength={100} />
            </Form.Item>
          </Form>
        </Modal>

        <Modal
          title={t('billing.subscriptionInvoices.voidConfirmTitle')}
          open={voidTarget != null}
          onCancel={() => {
            setVoidTarget(null);
            voidForm.resetFields();
          }}
          onOk={() => voidForm.submit()}
          confirmLoading={voidMutation.isPending}
          okButtonProps={{ danger: true }}
          destroyOnHidden
        >
          <Form
            form={voidForm}
            layout="vertical"
            onFinish={(values) => {
              if (!voidTarget) return;
              const reason = values.reason?.trim() ?? '';
              if (!reason) {
                notify.error(t('billing.subscriptionInvoices.voidReasonRequired'));
                return;
              }
              voidMutation.mutate({ id: voidTarget.id, reason });
            }}
          >
            <Form.Item
              name="reason"
              label={t('billing.subscriptionInvoices.voidReason')}
              rules={[{ required: true, message: t('billing.subscriptionInvoices.voidReasonRequired') }]}
            >
              <Input.TextArea rows={3} maxLength={500} />
            </Form.Item>
          </Form>
        </Modal>
      </AdminPageShell>
    </BillingAccessGate>
  );
}
