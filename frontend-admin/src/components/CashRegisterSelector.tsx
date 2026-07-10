'use client';

import { Alert, Form, Select, Skeleton, Tag } from 'antd';
import type { SelectProps } from 'antd';

import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { useI18n } from '@/i18n';

export type CashRegisterSelectorProps = {
    value?: string;
    onChange?: (value: string | undefined) => void;
    label?: string;
    required?: boolean;
    disabled?: boolean;
    placeholder?: string;
    /** Allow clearing selection (default: inverse of `required`). */
    allowClear?: boolean;
    /** Preselect default / sole register and notify parent (default: same as `required`). */
    autoSelect?: boolean;
    /** Remember selection in session storage for cross-page consistency (default: true). */
    persistSelection?: boolean;
    /** Wrap the select in `Form.Item` when a label is shown (default: true). */
    showFormItem?: boolean;
    style?: SelectProps['style'];
    className?: string;
};

/**
 * Operational/reporting cash register picker for tenant managers.
 * Uses canonical admin register list + auto-selection via {@link useCashRegisterSelection}.
 *
 * For Super Admin mandant picking and rich register metadata, use
 * `@/features/cash-registers/components/CashRegisterSelector` instead.
 */
export function CashRegisterSelector({
    value,
    onChange,
    label,
    required = true,
    disabled = false,
    placeholder,
    allowClear,
    autoSelect,
    persistSelection = true,
    showFormItem = true,
    style,
    className,
}: CashRegisterSelectorProps) {
    const { t } = useI18n();
    const resolvedLabel = label ?? t('cashRegisters.selector.label');
    const resolvedPlaceholder = placeholder ?? t('cashRegisters.selector.placeholder');
    const resolvedAllowClear = allowClear ?? !required;
    const resolvedAutoSelect = autoSelect ?? required;

    const {
        registerOptions,
        selectedRegisterId,
        setSelectedRegisterId,
        registers,
        isLoading,
        error,
        isSingleRegister,
    } = useCashRegisterSelection({
        value,
        onChange,
        controlled: onChange !== undefined,
        autoSelect: resolvedAutoSelect,
        persistSelection,
    });

    if (isLoading) {
        return <Skeleton.Input active size="small" style={{ width: 200, ...style }} />;
    }

    if (error) {
        return (
            <Alert
                title={t('cashRegisters.selector.loadErrorTitle')}
                type="error"
                showIcon
            />
        );
    }

    if (registers.length === 0) {
        return (
            <Alert
                title={t('cashRegisters.selector.empty')}
                description={t('cashRegisters.emptyContactAdmin')}
                type="warning"
                showIcon
            />
        );
    }

    if (isSingleRegister) {
        const register = registers[0];
        return (
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                {showFormItem && resolvedLabel ? (
                    <span style={{ fontWeight: 500, color: '#1e293b' }}>{resolvedLabel}:</span>
                ) : null}
                <span style={{ color: '#475569' }}>{register.registerNumber}</span>
                <Tag color="blue" variant="filled">
                    {t('cashRegisters.selector.autoSelectedTag')}
                </Tag>
            </div>
        );
    }

    const select = (
        <Select
            className={className}
            style={{ minWidth: 200, ...style }}
            value={selectedRegisterId}
            onChange={(next) => setSelectedRegisterId(next)}
            disabled={disabled}
            allowClear={resolvedAllowClear}
            placeholder={resolvedPlaceholder}
            options={registerOptions}
        />
    );

    if (showFormItem && resolvedLabel) {
        return (
            <Form.Item label={resolvedLabel} required={required} style={{ marginBottom: 0 }}>
                {select}
            </Form.Item>
        );
    }

    return select;
}
