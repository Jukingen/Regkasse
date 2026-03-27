'use client';

// Collapsible panel describing settings domain boundaries without a large refactor.

import React from 'react';
import { Collapse, List, Space, Typography, Button } from 'antd';
import Link from 'next/link';
import { useI18n } from '@/i18n/I18nProvider';
import { RKSV_HUB_MENU_LEAF_KEY } from '@/shared/adminSidebarNavigation';

export function SettingsHubContextPanel() {
  const { t } = useI18n();

  return (
    <Collapse
      bordered={false}
      defaultActiveKey={[]}
      items={[
        {
          key: 'settings-ia',
          label: t('settings.hub.collapseTitle'),
          children: (
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('settings.hub.intro')}
              </Typography.Paragraph>
              <List
                size="small"
                dataSource={[
                  t('settings.hub.rowSystem'),
                  t('settings.hub.rowCompany'),
                  t('settings.hub.rowUser'),
                  t('settings.hub.rowReceipt'),
                  t('settings.hub.rowRksv'),
                ]}
                renderItem={(item) => <List.Item style={{ paddingLeft: 0 }}>{item}</List.Item>}
              />
              <Space wrap>
                <Link href="/receipt-templates">
                  <Button size="small">{t('settings.hub.linkReceiptTemplates')}</Button>
                </Link>
                <Link href={RKSV_HUB_MENU_LEAF_KEY}>
                  <Button size="small">{t('settings.hub.linkRksv')}</Button>
                </Link>
              </Space>
            </Space>
          ),
        },
      ]}
    />
  );
}
