'use client';

import React from 'react';
import { Menu } from 'antd';
import type { MenuProps } from 'antd';
import { useI18n } from '@/i18n/I18nProvider';

export type ReportCategoryId =
    | 'reconciliation'
    | 'tse'
    | 'offline'
    | 'users'
    | 'peak'
    | 'movement';

export type ReportSelectorProps = {
    activeKey: ReportCategoryId;
    onChange: (key: ReportCategoryId) => void;
};

const REPORT_KEYS: ReportCategoryId[] = [
    'reconciliation',
    'tse',
    'offline',
    'users',
    'peak',
    'movement',
];

export function ReportSelector({ activeKey, onChange }: ReportSelectorProps) {
    const { t } = useI18n();

    const items: MenuProps['items'] = [
        {
            type: 'group',
            label: t('reporting.compliance.groups.compliance'),
            children: [
                { key: 'reconciliation', label: t('reporting.compliance.tabs.reconciliation') },
                { key: 'tse', label: t('reporting.compliance.tabs.tse') },
                { key: 'offline', label: t('reporting.compliance.tabs.offline') },
            ],
        },
        {
            type: 'group',
            label: t('reporting.compliance.groups.operations'),
            children: [
                { key: 'users', label: t('reporting.compliance.tabs.users') },
                { key: 'peak', label: t('reporting.compliance.tabs.peak') },
                { key: 'movement', label: t('reporting.compliance.tabs.movement') },
            ],
        },
    ];

    return (
        <Menu
            mode="inline"
            selectedKeys={[activeKey]}
            items={items}
            onClick={({ key }) => {
                if (REPORT_KEYS.includes(key as ReportCategoryId)) {
                    onChange(key as ReportCategoryId);
                }
            }}
            style={{ borderInlineEnd: 0, minWidth: 220 }}
        />
    );
}
