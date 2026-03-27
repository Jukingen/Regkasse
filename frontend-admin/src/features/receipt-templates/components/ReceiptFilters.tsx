'use client';

import React from 'react';
import { Select, Input, Space } from 'antd';
import { useReceiptTemplateFilters } from '../hooks/useReceiptTemplates';
import { useI18n } from '@/i18n';

export default function ReceiptFilters() {
    const { t } = useI18n();
    const { filters, setParams, setParam } = useReceiptTemplateFilters();

    // Default to 'all' if undefined
    const mode = filters.mode || 'all';
    const value = filters.value || '';

    const handleModeChange = (newMode: 'all' | 'language' | 'type') => {
        setParams({ mode: newMode, value: '' });
    };

    return (
        <Space style={{ marginBottom: 16 }}>
            <Select
                value={mode}
                onChange={handleModeChange}
                style={{ width: 150 }}
            >
                <Select.Option value="all">{t('receiptTemplates.filters.all')}</Select.Option>
                <Select.Option value="language">{t('receiptTemplates.filters.byLanguage')}</Select.Option>
                <Select.Option value="type">{t('receiptTemplates.filters.byType')}</Select.Option>
            </Select>

            {mode === 'language' && (
                <Input
                    placeholder={t('receiptTemplates.filters.placeholderLang')}
                    value={value}
                    onChange={(e) => setParam('value', e.target.value)}
                    style={{ width: 200 }}
                />
            )}

            {mode === 'type' && (
                <Input
                    placeholder={t('receiptTemplates.filters.placeholderType')}
                    value={value}
                    onChange={(e) => setParam('value', e.target.value)}
                    style={{ width: 200 }}
                />
            )}
        </Space>
    );
}
