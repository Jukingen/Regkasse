'use client';

import { DownloadOutlined, StarFilled, ThunderboltOutlined } from '@ant-design/icons';
import { Button, Card, Empty, Flex, Space, Typography } from 'antd';
import Link from 'next/link';

import { useExportFavorites } from '@/features/exports/useExportFavorites';
import { useI18n } from '@/i18n/I18nProvider';

/**
 * Quick actions derived from starred export types (ordered).
 * Uses Flex instead of deprecated antd List (Listy successor not shipped yet).
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
      <Flex vertical>
        {favorites.map((item, index) => (
          <Flex
            key={item.id}
            justify="space-between"
            align="center"
            gap={12}
            style={{
              paddingBlock: 12,
              borderBottom:
                index < favorites.length - 1 ? '1px solid rgba(5, 5, 5, 0.06)' : undefined,
            }}
          >
            <Flex align="center" gap={12} style={{ minWidth: 0, flex: 1 }}>
              <StarFilled style={{ color: '#faad14', fontSize: 18 }} aria-hidden />
              <Typography.Text ellipsis>{t(item.quickActionKey)}</Typography.Text>
            </Flex>
            <Link href={item.href}>
              <Button type="primary" size="small" icon={<DownloadOutlined />}>
                {t('common.exportFavorites.open')}
              </Button>
            </Link>
          </Flex>
        ))}
      </Flex>
    </Card>
  );
}
