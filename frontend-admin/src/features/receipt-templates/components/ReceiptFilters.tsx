'use client';

import React from 'react';
import { Select, Input, Space } from 'antd';
import { useReceiptTemplateFilters } from '../hooks/useReceiptTemplates';

export default function ReceiptFilters() {
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
                <Select.Option value="all">All Templates</Select.Option>
                <Select.Option value="language">By Language</Select.Option>
                <Select.Option value="type">By Type</Select.Option>
            </Select>

            {mode === 'language' && (
                <Input
                    placeholder="e.g. en, de, tr"
                    value={value}
                    onChange={(e) => setParam('value', e.target.value)}
                    style={{ width: 200 }}
                />
            )}

            {mode === 'type' && (
                <Input
                    placeholder="e.g. sale, refund"
                    value={value}
                    onChange={(e) => setParam('value', e.target.value)}
                    style={{ width: 200 }}
                />
            )}
        </Space>
    );
}
