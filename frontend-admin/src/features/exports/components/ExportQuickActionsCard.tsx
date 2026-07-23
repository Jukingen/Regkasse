'use client';

import { DownloadOutlined, StarFilled, ThunderboltOutlined } from '@ant-design/icons';
import { Button, Card, Empty, List, Space } from 'antd';
import Link from 'next/link';

import { useExportFavorites } from '@/features/exports/useExportFavorites';
import { useI18n } from '@/i18n/I18nProvider';

/**
 * Quick actions derived from starred export types (ordered).
 */
export function ExportQuickActionsCard() {
  const { t } = useI18n();
  const { hydrated, favorites } = useExportFavorites();

  if (!hydrated) return null;
  if (favorites.length === 0) {
    return (
      <Card
        title={
          <Space>
            <ThunderboltOutlined />
            <span>{t('common.exportFavorites.quickTitle')}</span>
          </Space>
        }
      >
        <Empty description={t('common.exportFavorites.quickEmpty')} />
      </Card>
    );
  }

  return (
    <Card
      title={
        <Space>
          <ThunderboltOutlined />
          <span>{t('common.exportFavorites.quickTitle')}</span>
        </Space>
      }
    >
      <List
        dataSource={favorites}
        renderItem={(item) => (
          <List.Item
            actions={[
              <Link key="go" href={item.href}>
                <Button type="primary" size="small" icon={<DownloadOutlined />}>
                  {t('common.exportFavorites.open')}
                </Button>
              </Link>,
            ]}
          >
            <List.Item.Meta
              avatar={<StarFilled style={{ color: '#faad14', fontSize: 18 }} />}
              title={t(item.quickActionKey)}
            />
          </List.Item>
        )}
      />
    </Card>
  );
}
