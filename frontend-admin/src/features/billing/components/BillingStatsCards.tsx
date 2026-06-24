'use client';

import React from 'react';
import { Card, Col, Row, Skeleton, Statistic } from 'antd';
import { useI18n, formatCurrency } from '@/i18n';
import type { LicenseSaleStatsResponse } from '@/api/generated/model';

type BillingStatsCardsProps = {
    stats?: LicenseSaleStatsResponse;
    loading?: boolean;
};

const CARD_KEYS = [
    'totalSales',
    'totalRevenueGross',
    'totalRevenueNet',
    'totalVat',
    'activeLicenses',
    'expiringSoon',
    'expiredLicenses',
    'cancelledSales',
    'tenantsWithLicense',
    'averagePriceNet',
] as const;

export function BillingStatsCards({ stats, loading }: BillingStatsCardsProps) {
    const { t, formatLocale } = useI18n();

    const formatValue = (key: (typeof CARD_KEYS)[number], value: number | undefined) => {
        if (value == null) return '—';
        if (key === 'totalRevenueGross' || key === 'totalRevenueNet' || key === 'totalVat' || key === 'averagePriceNet') {
            return formatCurrency(value, formatLocale, { currency: 'EUR' });
        }
        return value.toLocaleString(formatLocale);
    };

    const resolveStat = (key: (typeof CARD_KEYS)[number]): number | undefined => {
        switch (key) {
            case 'totalSales':
                return stats?.totalSales;
            case 'totalRevenueGross':
                return stats?.totalRevenueGross;
            case 'totalRevenueNet':
                return stats?.totalRevenueNet;
            case 'totalVat':
                return stats?.totalVat;
            case 'activeLicenses':
                return stats?.activeLicenses;
            case 'expiringSoon':
                return stats?.expiringSoonLicenses;
            case 'expiredLicenses':
                return stats?.expiredLicenses;
            case 'cancelledSales':
                return stats?.cancelledSales;
            case 'tenantsWithLicense':
                return stats?.totalTenantsWithLicense;
            case 'averagePriceNet':
                return stats?.averagePriceNet;
            default:
                return undefined;
        }
    };

    return (
        <Row gutter={[16, 16]}>
            {CARD_KEYS.map((key) => (
                <Col key={key} xs={24} sm={12} md={8} lg={6}>
                    <Card variant="borderless">
                        {loading ? (
                            <Skeleton active paragraph={false} />
                        ) : (
                            <Statistic
                                title={t(`billing.stats.cards.${key}`)}
                                value={formatValue(key, resolveStat(key))}
                            />
                        )}
                    </Card>
                </Col>
            ))}
        </Row>
    );
}
