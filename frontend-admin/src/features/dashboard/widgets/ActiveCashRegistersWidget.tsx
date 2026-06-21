'use client';

import React, { useMemo } from 'react';
import { Statistic, Tag, Typography } from 'antd';
import { SimpleList as List } from '@/components/ui/SimpleList';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import {
    listAdminCashRegisters,
    cashRegisterListQueryKey,
    type AdminCashRegisterListItem,
} from '@/features/cash-registers/api/cashRegisters';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { AppPermissions } from '@/shared/auth/permissions';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { useI18n } from '@/i18n/I18nProvider';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

const OPEN_STATUS = 1;

export function ActiveCashRegistersWidget({ title, dragHandleProps, onRefresh }: Props) {
    const { t } = useI18n();
    const query = useAuthorizedQuery({
        queryKey: [...cashRegisterListQueryKey, 'dashboard'],
        queryFn: () => listAdminCashRegisters({ page: 1, pageSize: 50 }),
        requiredPermission: AppPermissions.CashRegisterView,
        refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
        staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    });

    const items = (query.data?.items ?? []) as AdminCashRegisterListItem[];
    const openCount = useMemo(() => items.filter((r) => r.status === OPEN_STATUS).length, [items]);

    const statusTag = (status: number) => {
        if (status === OPEN_STATUS) {
            return <Tag color="green">{t('dashboard.cashRegistersWidget.status_open')}</Tag>;
        }
        return <Tag>{t('dashboard.cashRegistersWidget.status_closed')}</Tag>;
    };

    const handleRefresh = () => {
        void query.refetch();
        onRefresh?.();
    };

    return (
        <WidgetShell
            title={title}
            dragHandleProps={dragHandleProps}
            onRefresh={handleRefresh}
            refreshing={query.isFetching}
        >
            <Statistic
                title={t('dashboard.cashRegistersWidget.open_count_title')}
                value={openCount}
                suffix={`/ ${items.length}`}
            />
            <List
                style={{ marginTop: 12 }}
                size="small"
                loading={query.isLoading}
                dataSource={items.slice(0, 6)}
                locale={{ emptyText: t('dashboard.cashRegistersWidget.empty') }}
                renderItem={(item) => (
                    <List.Item>
                        <List.Item.Meta
                            title={`${item.registerNumber} — ${item.location}`}
                            description={statusTag(item.status)}
                        />
                    </List.Item>
                )}
            />
            {items.length > 6 ? (
                <Typography.Text type="secondary">
                    {t('dashboard.cashRegistersWidget.more_count', { count: items.length - 6 })}
                </Typography.Text>
            ) : null}
        </WidgetShell>
    );
}
