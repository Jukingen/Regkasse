'use client';

import type { CSSProperties } from 'react';
import { Card, Space, Tag, Typography } from 'antd';
import { CheckCircleOutlined, ShopOutlined } from '@ant-design/icons';

import { CashRegisterDetailsTooltip } from '@/components/CashRegisterDetailsTooltip';
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { CashRegisterStatusBadge } from '@/features/cash-registers/components/CashRegisterStatusBadge';
import { TseHealthBadge } from '@/features/cash-registers/components/TseHealthBadge';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';
import { useI18n } from '@/i18n';

export type SelectedCashRegisterCardProps = {
    register: AdminCashRegisterListItem;
    /** Show “auto-selected” tag (single-register default). Default true. */
    showAutoSelectedTag?: boolean;
    style?: CSSProperties;
    className?: string;
};

function formatLastActivity(value: string | undefined, locale: string): string | null {
    if (!value?.trim()) {
        return null;
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return null;
    }
    return date.toLocaleDateString(locale, {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
    });
}

/**
 * Prominent banner for the currently scoped cash register (FA operational pages + header context).
 */
export function SelectedCashRegisterCard({
    register,
    showAutoSelectedTag = true,
    style,
    className,
}: SelectedCashRegisterCardProps) {
    const { t, formatLocale } = useI18n();
    const title = register.location?.trim()
        ? `${formatRegisterDisplayLabel(register.registerNumber)} — ${register.location.trim()}`
        : formatRegisterDisplayLabel(register.registerNumber);
    const shortId = register.id?.length >= 8 ? `${register.id.slice(0, 8)}…` : register.id;
    const lastActivity =
        formatLastActivity(register.lastSyncAtUtc, formatLocale) ??
        formatLastActivity(register.lastBalanceUpdate, formatLocale);

    return (
        <CashRegisterDetailsTooltip register={register} placement="top">
            <Card
                size="small"
                className={className}
                data-testid="selected-cash-register-card"
                styles={{
                    body: { padding: '12px 16px' },
                }}
                style={{
                    borderColor: '#16a34a',
                    borderWidth: 2,
                    backgroundColor: '#f0fdf4',
                    boxShadow: '0 1px 2px rgba(22, 163, 74, 0.12)',
                    cursor: 'help',
                    ...style,
                }}
            >
                <Space orientation="vertical" size={6} style={{ width: '100%' }}>
                    <Typography.Text
                        type="secondary"
                        style={{
                            fontSize: 11,
                            fontWeight: 600,
                            letterSpacing: '0.04em',
                            textTransform: 'uppercase',
                            color: '#15803d',
                        }}
                    >
                        {t('cashRegisters.selector.activeContextLabel')}
                    </Typography.Text>
                    <Space size={8} wrap style={{ width: '100%' }} align="center">
                        <ShopOutlined style={{ color: '#16a34a', fontSize: 20 }} aria-hidden />
                        <Typography.Text strong style={{ fontSize: 17, color: '#14532d', lineHeight: 1.3 }}>
                            {title}
                        </Typography.Text>
                        <Tag color="green" variant="filled" icon={<CheckCircleOutlined />}>
                            {t('cashRegisters.selector.activeTag')}
                        </Tag>
                        {showAutoSelectedTag ? (
                            <Tag color="cyan" variant="filled">
                                {t('cashRegisters.selector.autoSelectedTag')}
                            </Tag>
                        ) : (
                            <Tag color="blue" variant="filled">
                                {t('cashRegisters.selector.currentTag')}
                            </Tag>
                        )}
                        <CashRegisterStatusBadge register={register as never} useIcon />
                    </Space>
                    <Space size={16} wrap>
                        {shortId ? (
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                {t('cashRegisters.selector.idLabel', { shortId })}
                            </Typography.Text>
                        ) : null}
                        {register.tseHealthStatus ? (
                            <Space size={4}>
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    {t('cashRegisters.selector.tsePrefix')}
                                </Typography.Text>
                                <TseHealthBadge status={register.tseHealthStatus} showDetails={false} />
                            </Space>
                        ) : null}
                        {lastActivity ? (
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                {t('cashRegisters.selector.lastActivityLabel', { date: lastActivity })}
                            </Typography.Text>
                        ) : null}
                    </Space>
                    {showAutoSelectedTag ? (
                        <Typography.Text style={{ fontSize: 12, color: '#166534' }}>
                            {t('cashRegisters.selector.autoSelectedHint')}
                        </Typography.Text>
                    ) : null}
                </Space>
            </Card>
        </CashRegisterDetailsTooltip>
    );
}
