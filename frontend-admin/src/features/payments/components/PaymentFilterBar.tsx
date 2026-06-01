'use client';

import { useCallback, useMemo, useState } from 'react';
import {
    Badge,
    Button,
    Card,
    Checkbox,
    Col,
    DatePicker,
    Divider,
    Drawer,
    Input,
    InputNumber,
    Row,
    Select,
    Space,
    Tag,
} from 'antd';
import { ClearOutlined, FilterOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import type { CashRegister } from '@/api/generated/model';
import { UserFilterSelect } from '@/features/audit-logs/components/UserFilterSelect';
import type { PaymentFilters } from '@/features/payments/types/paymentFilters';
import { countActivePaymentFilters } from '@/features/payments/utils/countActivePaymentFilters';
import { useI18n } from '@/i18n';

const { RangePicker } = DatePicker;

export interface PaymentFilterBarProps {
    filters: PaymentFilters;
    onFilterChange: (filters: PaymentFilters) => void;
    availableMethods: string[];
    availableStatuses: string[];
    cashRegisters: CashRegister[];
}

type FilterKey = keyof PaymentFilters;

export function PaymentFilterBar({
    filters,
    onFilterChange,
    availableMethods,
    availableStatuses,
    cashRegisters,
}: PaymentFilterBarProps) {
    const { t } = useI18n();
    const [drawerOpen, setDrawerOpen] = useState(false);

    const activeFilterCount = useMemo(() => countActivePaymentFilters(filters), [filters]);

    const patchFilters = useCallback(
        (patch: Partial<PaymentFilters>) => {
            onFilterChange({ ...filters, ...patch });
        },
        [filters, onFilterChange],
    );

    const handleFilterChange = useCallback(
        (key: FilterKey, value: unknown) => {
            const next: PaymentFilters = { ...filters, [key]: value as PaymentFilters[FilterKey] };
            const emptyArray = Array.isArray(value) && value.length === 0;
            const emptyString = typeof value === 'string' && value.trim() === '';
            const emptyBool = key === 'isStorno' || key === 'isRefund' ? value === false : false;
            if (
                value == null ||
                emptyArray ||
                emptyString ||
                emptyBool ||
                (key === 'dateRange' && value == null)
            ) {
                delete next[key];
            }
            onFilterChange(next);
        },
        [filters, onFilterChange],
    );

    const clearAllFilters = useCallback(() => {
        onFilterChange({});
    }, [onFilterChange]);

    const methodOptions = useMemo(
        () => availableMethods.map((m) => ({ label: m, value: m })),
        [availableMethods],
    );

    const statusOptions = useMemo(
        () =>
            availableStatuses.map((s) => {
                const key = `payments.statusLabels.payment.${s}` as const;
                const label = t(key);
                return {
                    value: s,
                    label: label === key ? s : label,
                };
            }),
        [availableStatuses, t],
    );

    const registerOptions = useMemo(
        () =>
            cashRegisters
                .filter((r) => r.id)
                .map((r) => ({
                    value: r.id as string,
                    label: r.registerNumber ?? r.location ?? r.id,
                })),
        [cashRegisters],
    );

    const dateRangeValue = filters.dateRange ?? undefined;

    return (
        <>
            <Card size="small" style={{ marginBottom: 16 }}>
                <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
                    <Space wrap>
                        <RangePicker
                            value={dateRangeValue}
                            placeholder={[
                                t('payments.filtersBar.dateStart'),
                                t('payments.filtersBar.dateEnd'),
                            ]}
                            format="DD.MM.YYYY"
                            onChange={(dates) =>
                                handleFilterChange(
                                    'dateRange',
                                    dates?.[0] && dates[1] ? ([dates[0], dates[1]] as [Dayjs, Dayjs]) : null,
                                )
                            }
                            allowClear
                        />
                        <Select
                            mode="multiple"
                            placeholder={t('payments.filtersBar.paymentMethods')}
                            style={{ minWidth: 200 }}
                            options={methodOptions}
                            value={filters.paymentMethods}
                            onChange={(v) => handleFilterChange('paymentMethods', v)}
                            allowClear
                        />
                        <Select
                            mode="multiple"
                            placeholder={t('payments.filtersBar.statuses')}
                            style={{ minWidth: 180 }}
                            options={statusOptions}
                            value={filters.statuses}
                            onChange={(v) => handleFilterChange('statuses', v)}
                            allowClear
                        />
                        <Input.Search
                            placeholder={t('payments.filtersBar.receiptNumber')}
                            style={{ width: 160 }}
                            value={filters.receiptNumber}
                            onChange={(e) => patchFilters({ receiptNumber: e.target.value || undefined })}
                            onSearch={(v) => handleFilterChange('receiptNumber', v.trim() || undefined)}
                            allowClear
                        />
                    </Space>

                    <Space>
                        <Badge count={activeFilterCount} offset={[10, 0]}>
                            <Button icon={<FilterOutlined />} onClick={() => setDrawerOpen(true)}>
                                {t('payments.filtersBar.advanced')}
                            </Button>
                        </Badge>
                        {activeFilterCount > 0 ? (
                            <Button icon={<ClearOutlined />} onClick={clearAllFilters}>
                                {t('payments.filtersBar.clearAll')}
                            </Button>
                        ) : null}
                    </Space>
                </Space>

                {activeFilterCount > 0 ? (
                    <div style={{ marginTop: 12, paddingTop: 12, borderTop: '1px solid #f0f0f0' }}>
                        <Space wrap size={[8, 8]}>
                            {filters.dateRange?.[0] && filters.dateRange[1] ? (
                                <Tag
                                    closable
                                    onClose={() => handleFilterChange('dateRange', null)}
                                >
                                    {t('payments.filtersBar.chipDate', {
                                        from: filters.dateRange[0].format('DD.MM.YYYY'),
                                        to: filters.dateRange[1].format('DD.MM.YYYY'),
                                    })}
                                </Tag>
                            ) : null}
                            {filters.paymentMethods?.map((m) => (
                                <Tag
                                    key={`method-${m}`}
                                    closable
                                    onClose={() => {
                                        const next = filters.paymentMethods?.filter((pm) => pm !== m);
                                        handleFilterChange('paymentMethods', next?.length ? next : undefined);
                                    }}
                                >
                                    {t('payments.filtersBar.chipMethod', { value: m })}
                                </Tag>
                            ))}
                            {filters.statuses?.map((s) => (
                                <Tag
                                    key={`status-${s}`}
                                    closable
                                    onClose={() => {
                                        const next = filters.statuses?.filter((st) => st !== s);
                                        handleFilterChange('statuses', next?.length ? next : undefined);
                                    }}
                                >
                                    {t('payments.filtersBar.chipStatus', {
                                        value: (() => {
                                            const key = `payments.statusLabels.payment.${s}` as const;
                                            const label = t(key);
                                            return label === key ? s : label;
                                        })(),
                                    })}
                                </Tag>
                            ))}
                            {filters.receiptNumber?.trim() ? (
                                <Tag closable onClose={() => handleFilterChange('receiptNumber', undefined)}>
                                    {t('payments.filtersBar.chipReceipt', { value: filters.receiptNumber.trim() })}
                                </Tag>
                            ) : null}
                            {filters.minAmount != null ? (
                                <Tag closable onClose={() => handleFilterChange('minAmount', undefined)}>
                                    {t('payments.filtersBar.chipMinAmount', { value: filters.minAmount })}
                                </Tag>
                            ) : null}
                            {filters.maxAmount != null ? (
                                <Tag closable onClose={() => handleFilterChange('maxAmount', undefined)}>
                                    {t('payments.filtersBar.chipMaxAmount', { value: filters.maxAmount })}
                                </Tag>
                            ) : null}
                            {filters.cashRegisterId ? (
                                <Tag closable onClose={() => handleFilterChange('cashRegisterId', undefined)}>
                                    {t('payments.filtersBar.chipRegister', {
                                        value:
                                            registerOptions.find((o) => o.value === filters.cashRegisterId)?.label ??
                                            filters.cashRegisterId,
                                    })}
                                </Tag>
                            ) : null}
                            {filters.customerName?.trim() ? (
                                <Tag closable onClose={() => handleFilterChange('customerName', undefined)}>
                                    {t('payments.filtersBar.chipCustomer', { value: filters.customerName.trim() })}
                                </Tag>
                            ) : null}
                            {filters.customerEmail?.trim() ? (
                                <Tag closable onClose={() => handleFilterChange('customerEmail', undefined)}>
                                    {t('payments.filtersBar.chipCustomerEmail', {
                                        value: filters.customerEmail.trim(),
                                    })}
                                </Tag>
                            ) : null}
                            {filters.cashierId ? (
                                <Tag closable onClose={() => handleFilterChange('cashierId', undefined)}>
                                    {t('payments.filtersBar.chipCashier', { value: filters.cashierId })}
                                </Tag>
                            ) : null}
                            {filters.isStorno ? (
                                <Tag closable onClose={() => handleFilterChange('isStorno', undefined)}>
                                    {t('payments.filtersBar.chipStorno')}
                                </Tag>
                            ) : null}
                            {filters.isRefund ? (
                                <Tag closable onClose={() => handleFilterChange('isRefund', undefined)}>
                                    {t('payments.filtersBar.chipRefund')}
                                </Tag>
                            ) : null}
                        </Space>
                    </div>
                ) : null}
            </Card>

            <Drawer
                title={t('payments.filtersBar.drawerTitle')}
                placement="right"
                size="default"
                open={drawerOpen}
                onClose={() => setDrawerOpen(false)}
                extra={
                    <Button onClick={clearAllFilters} icon={<ClearOutlined />}>
                        {t('payments.filtersBar.resetDrawer')}
                    </Button>
                }
            >
                <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                    <div>
                        <div style={{ fontWeight: 500, marginBottom: 8 }}>{t('payments.filtersBar.amountSection')}</div>
                        <Row gutter={16}>
                            <Col span={12}>
                                <InputNumber
                                    placeholder={t('payments.filtersBar.minAmount')}
                                    style={{ width: '100%' }}
                                    min={0}
                                    precision={2}
                                    value={filters.minAmount}
                                    onChange={(v) =>
                                        handleFilterChange(
                                            'minAmount',
                                            typeof v === 'number' ? v : undefined,
                                        )
                                    }
                                />
                            </Col>
                            <Col span={12}>
                                <InputNumber
                                    placeholder={t('payments.filtersBar.maxAmount')}
                                    style={{ width: '100%' }}
                                    min={0}
                                    precision={2}
                                    value={filters.maxAmount}
                                    onChange={(v) =>
                                        handleFilterChange(
                                            'maxAmount',
                                            typeof v === 'number' ? v : undefined,
                                        )
                                    }
                                />
                            </Col>
                        </Row>
                    </div>

                    <Divider style={{ margin: 0 }} />

                    <div>
                        <div style={{ fontWeight: 500, marginBottom: 8 }}>{t('payments.filtersBar.registerSection')}</div>
                        <Select
                            placeholder={t('payments.filtersBar.allRegisters')}
                            style={{ width: '100%' }}
                            options={registerOptions}
                            value={filters.cashRegisterId}
                            onChange={(v) => handleFilterChange('cashRegisterId', v)}
                            allowClear
                        />
                    </div>

                    <Divider style={{ margin: 0 }} />

                    <div>
                        <div style={{ fontWeight: 500, marginBottom: 8 }}>{t('payments.filtersBar.customerSection')}</div>
                        <Space orientation="vertical" style={{ width: '100%' }}>
                            <Input
                                placeholder={t('payments.filtersBar.customerName')}
                                value={filters.customerName}
                                onChange={(e) => patchFilters({ customerName: e.target.value || undefined })}
                            />
                            <Input
                                placeholder={t('payments.filtersBar.customerEmail')}
                                value={filters.customerEmail}
                                onChange={(e) => patchFilters({ customerEmail: e.target.value || undefined })}
                            />
                        </Space>
                    </div>

                    <Divider style={{ margin: 0 }} />

                    <div>
                        <div style={{ fontWeight: 500, marginBottom: 8 }}>{t('payments.filtersBar.cashierSection')}</div>
                        <UserFilterSelect
                            value={filters.cashierId}
                            onChange={(v) => handleFilterChange('cashierId', v)}
                            placeholder={t('payments.filtersBar.allCashiers')}
                            style={{ width: '100%' }}
                        />
                    </div>

                    <Divider style={{ margin: 0 }} />

                    <div>
                        <div style={{ fontWeight: 500, marginBottom: 8 }}>{t('payments.filtersBar.transactionTypeSection')}</div>
                        <Space orientation="vertical">
                            <Checkbox
                                checked={filters.isStorno === true}
                                onChange={(e) =>
                                    handleFilterChange('isStorno', e.target.checked ? true : undefined)
                                }
                            >
                                {t('payments.filtersBar.stornoOnly')}
                            </Checkbox>
                            <Checkbox
                                checked={filters.isRefund === true}
                                onChange={(e) =>
                                    handleFilterChange('isRefund', e.target.checked ? true : undefined)
                                }
                            >
                                {t('payments.filtersBar.refundOnly')}
                            </Checkbox>
                        </Space>
                    </div>
                </Space>
            </Drawer>
        </>
    );
}
