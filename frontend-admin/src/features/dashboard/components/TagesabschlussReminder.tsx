'use client';

import Link from 'next/link';
import { Button, Card } from 'antd';
import { WarningOutlined } from '@ant-design/icons';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { useTagesabschlussStatus } from '@/hooks/useTagesabschlussStatus';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { useI18n } from '@/i18n/I18nProvider';

export type TagesabschlussReminderProps = {
    cashRegisterId?: string | null;
    register?: AdminCashRegisterListItem | null;
};

/**
 * FA dashboard reminder when Tagesabschluss is still due for the selected register.
 * Never auto-closes — RKSV requires manual cash counting and user-initiated closing.
 */
export function TagesabschlussReminder({
    cashRegisterId,
    register: registerProp,
}: TagesabschlussReminderProps = {}) {
    const { t } = useI18n();
    const { isClosingRequired, register, transactionCount, isLoading } = useTagesabschlussStatus({
        cashRegisterId,
        register: registerProp,
    });
    const canOpenTagesabschluss = useCanAccessPath('/tagesabschluss');

    if (isLoading || !isClosingRequired) return null;

    return (
        <Card
            style={{
                marginBottom: 16,
                borderColor: '#eab308',
                backgroundColor: '#fffbeb',
            }}
            title={
                <span>
                    <WarningOutlined style={{ marginRight: 8, color: '#ca8a04' }} />
                    {t('dashboard.tagesabschlussReminder.title')}
                </span>
            }
            extra={
                canOpenTagesabschluss ? (
                    <Link href="/tagesabschluss">
                        <Button type="primary">{t('dashboard.tagesabschlussReminder.cta')}</Button>
                    </Link>
                ) : null
            }
        >
            <p style={{ margin: '0 0 8px' }}>
                {t('dashboard.tagesabschlussReminder.body', {
                    register: register?.name ?? t('dashboard.manager.noRegister'),
                })}
            </p>
            <p style={{ margin: '0 0 8px' }}>
                {t('dashboard.tagesabschlussReminder.transactionsWaiting', {
                    count: transactionCount,
                })}
            </p>
            <p style={{ margin: 0 }}>{t('dashboard.tagesabschlussReminder.deadlineHint')}</p>
        </Card>
    );
}
