'use client';

import React, { useMemo } from 'react';
import { Row, Col, Statistic, Typography } from 'antd';
import Link from 'next/link';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useGetApiAdminFinanzonlineReconciliation } from '@/api/generated/admin/admin';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { PERMISSIONS } from '@/shared/auth/permissions';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export function FinanzOnlineStatusWidget({ title, dragHandleProps, onRefresh }: Props) {
    const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.FINANZONLINE_VIEW });
    const queryOptions = {
        enabled: isAuthorized,
        refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
        staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    } as const;
    const pendingQuery = useGetApiAdminFinanzonlineReconciliation(
        { status: 'Pending', limit: 200 },
        { query: queryOptions },
    );
    const failedQuery = useGetApiAdminFinanzonlineReconciliation(
        { status: 'Failed', limit: 200 },
        { query: queryOptions },
    );

    const pending = pendingQuery.data?.total ?? pendingQuery.data?.items?.length ?? 0;
    const failed = failedQuery.data?.total ?? failedQuery.data?.items?.length ?? 0;

    const loading = pendingQuery.isLoading || failedQuery.isLoading;
    const refreshing = pendingQuery.isFetching || failedQuery.isFetching;

    const handleRefresh = () => {
        void pendingQuery.refetch();
        void failedQuery.refetch();
        onRefresh?.();
    };

    const hint = useMemo(() => {
        if (failed > 0) return 'Fehlgeschlagene Übermittlungen prüfen';
        if (pending > 0) return 'Ausstehende Übermittlungen in der Warteschlange';
        return 'Keine offenen Probleme';
    }, [failed, pending]);

    return (
        <WidgetShell
            title={title}
            dragHandleProps={dragHandleProps}
            onRefresh={handleRefresh}
            refreshing={refreshing}
        >
            <Row gutter={16}>
                <Col span={12}>
                    <Statistic title="Ausstehend" value={pending} loading={loading} />
                </Col>
                <Col span={12}>
                    <Statistic
                        title="Fehlgeschlagen"
                        value={failed}
                        loading={loading}
                        styles={failed > 0 ? { content: { color: '#cf1322' } } : undefined}
                    />
                </Col>
            </Row>
            <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
                {hint}.{' '}
                <Link href="/rksv/finanz-online-queue">Details</Link>
            </Typography.Paragraph>
        </WidgetShell>
    );
}
