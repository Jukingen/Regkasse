'use client';

import React from 'react';
import { Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useI18n, formatDateTime } from '@/i18n';
import { billingApi } from '@/features/billing/api/billingApi';
import type { BillingAuditLogResponse } from '@/api/generated/model';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function BillingAuditTable({ pageSize = 10 }: { pageSize?: number }) {
    const { t, formatLocale } = useI18n();
    const canAccess = useBillingAccess();

    const auditQuery = billingApi.useAudit(
        { page: 1, pageSize },
        { query: { enabled: canAccess } },
    );

    const columns: ColumnsType<BillingAuditLogResponse> = [
        {
            title: t('billing.audit.columns.timestamp'),
            dataIndex: 'timestampUtc',
            key: 'timestampUtc',
            width: 180,
            render: (value: string | undefined) =>
                value ? formatDateTime(value, formatLocale) : '—',
        },
        {
            title: t('billing.audit.columns.action'),
            dataIndex: 'action',
            key: 'action',
            render: (action: string | null | undefined) => {
                if (!action) return '—';
                const key = `billing.audit.actions.${action}`;
                const translated = t(key);
                return translated === key ? action : translated;
            },
        },
        {
            title: t('billing.audit.columns.tenant'),
            key: 'tenant',
            render: (_, row) => row.tenantName ?? row.tenantSlug ?? '—',
        },
        {
            title: t('billing.audit.columns.user'),
            dataIndex: 'userName',
            key: 'userName',
            render: (value: string | null | undefined) => value ?? '—',
        },
        {
            title: t('billing.audit.columns.details'),
            dataIndex: 'details',
            key: 'details',
            ellipsis: true,
            render: (value: string | null | undefined) => value ?? '—',
        },
    ];

    return (
        <Table<BillingAuditLogResponse>
            rowKey={(row) => row.id ?? `${row.timestampUtc}-${row.action}`}
            columns={columns}
            dataSource={auditQuery.data?.items ?? []}
            loading={auditQuery.isLoading}
            pagination={false}
            size="small"
        />
    );
}
