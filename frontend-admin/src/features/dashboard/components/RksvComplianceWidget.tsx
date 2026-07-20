'use client';

import React, { useMemo } from 'react';
import Link from 'next/link';
import { Button, Card, List, Space, Tag, Typography } from 'antd';
import { CardSkeleton } from '@/components/Skeleton';
import { useMonatsbelegStatus } from '@/features/rksv/hooks/useMonatsbeleg';
import { aggregateMissingMonatsbelegeForCompliance } from '@/features/rksv/utils/monatsbelegMissingMonths';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import { useI18n } from '@/i18n/I18nProvider';

const { Text } = Typography;

type RksvComplianceWidgetProps = {
    enabled?: boolean;
};

export function RksvComplianceWidget({ enabled = true }: RksvComplianceWidgetProps) {
    const { t } = useI18n();
    const { data, isLoading, isFetching } = useMonatsbelegStatus({ enabled });

    const missingMonths = useMemo(
        () => aggregateMissingMonatsbelegeForCompliance(data),
        [data],
    );

    if (!enabled) return null;

    if (isLoading) {
        return <CardSkeleton count={1} />;
    }

    if (missingMonths.length === 0) return null;

    return (
        <Card
            title={t('dashboard.rksvCompliance.missingTitle', { count: missingMonths.length })}
            extra={
                <Link href={RKSV_SONDERBELEGE_PATH}>
                    <Button type="link">{t('dashboard.rksvCompliance.catchUpNow')}</Button>
                </Link>
            }
            style={{ marginBottom: 16, borderColor: '#eab308' }}
            loading={isFetching && !isLoading}
        >
            <Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
                {t('dashboard.rksvCompliance.missingHint')}
            </Text>
            <List
                dataSource={missingMonths}
                renderItem={(item) => (
                    <List.Item>
                        <Space wrap>
                            <Text strong>{item.monthName}</Text>
                            <Text type="secondary">{item.year}</Text>
                            {item.daysLate > 0 ? (
                                <Tag color="red">
                                    {t('dashboard.rksvCompliance.daysLate', { count: item.daysLate })}
                                </Tag>
                            ) : item.isOverdue ? (
                                <Tag color="orange">{t('dashboard.rksvCompliance.overdue')}</Tag>
                            ) : (
                                <Tag color="gold">{t('dashboard.rksvCompliance.missing')}</Tag>
                            )}
                        </Space>
                    </List.Item>
                )}
            />
        </Card>
    );
}
