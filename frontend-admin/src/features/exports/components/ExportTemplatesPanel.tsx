'use client';

import {
  ClockCircleOutlined,
  EditOutlined,
  FileTextOutlined,
  PlusOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { Alert, Button, Card, Empty, List, Space, Tag, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import { useState } from 'react';

import { buildExportTemplateHref } from '@/features/exports/applyExportTemplate';
import { ExportTemplateEditorModal } from '@/features/exports/components/ExportTemplateEditorModal';
import type { ExportTemplate } from '@/features/exports/exportTemplateTypes';
import { useExportTemplates } from '@/features/exports/useExportTemplates';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';

function presetName(
  id: string,
  fallback: string,
  t: (key: string, vars?: Record<string, string | number>) => string
): string {
  if (id === 'preset-daily-dep') return t('common.exportTemplates.presets.dailyDep');
  if (id === 'preset-monthly-tagesbericht') {
    return t('common.exportTemplates.presets.monthlyTagesbericht');
  }
  if (id === 'preset-quarterly-backup') return t('common.exportTemplates.presets.quarterlyBackup');
  return fallback;
}

function periodLabel(
  period: string,
  t: (key: string, vars?: Record<string, string | number>) => string
): string {
  switch (period) {
    case 'last24h':
      return t('common.exportTemplates.periods.last24h');
    case 'last7d':
      return t('common.exportTemplates.periods.last7d');
    case 'currentMonth':
      return t('common.exportTemplates.periods.currentMonth');
    case 'lastMonth':
      return t('common.exportTemplates.periods.lastMonth');
    case 'custom':
      return t('common.exportTemplates.periods.custom');
    default:
      return period;
  }
}

function summarizeConfig(
  template: ExportTemplate,
  t: (key: string, vars?: Record<string, string | number>) => string
): string[] {
  const c = template.config;
  if (c.kind === 'dep-export') {
    return [
      t('common.exportTemplates.summary.register', {
        value: c.registerNumberHint || c.cashRegisterId || '—',
      }),
      t('common.exportTemplates.summary.period', {
        value: periodLabel(c.period, t),
      }),
      t('common.exportTemplates.summary.include', {
        value: [
          c.includeSpecialReceipts ? t('common.exportTemplates.include.special') : null,
          c.includeDailyClosings ? t('common.exportTemplates.include.daily') : null,
          t('common.exportTemplates.include.allReceipts'),
        ]
          .filter(Boolean)
          .join(', '),
      }),
    ];
  }
  if (c.kind === 'tagesbericht') {
    return [
      t('common.exportTemplates.summary.period', {
        value: periodLabel(c.period, t),
      }),
      t('common.exportTemplates.summary.format', {
        value: c.formats.map((f) => f.toUpperCase()).join(' + '),
      }),
    ];
  }
  return [
    t('common.exportTemplates.summary.strategy', {
      value:
        c.strategy === 'system'
          ? t('common.exportTemplates.strategies.system')
          : t('common.exportTemplates.strategies.tenant'),
    }),
    t('common.exportTemplates.summary.retention', { days: c.retentionDays }),
  ];
}

/**
 * Preset + custom export templates with use / edit / share / last-used suggestion.
 */
export function ExportTemplatesPanel() {
  const { t, formatLocale } = useI18n();
  const router = useRouter();
  const notify = useNotify();
  const { hydrated, templates, lastUsed, lastUsedAt, upsert, markUsed, userId, userName } =
    useExportTemplates();
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<ExportTemplate | null>(null);

  if (!hydrated) return null;

  const handleUse = (template: ExportTemplate) => {
    markUsed(template.id);
    const href = buildExportTemplateHref(template);
    notify.success(t('common.exportTemplates.applied'));
    router.push(href);
  };

  const openCreate = () => {
    setEditing(null);
    setEditorOpen(true);
  };

  const openEdit = (template: ExportTemplate) => {
    setEditing(template);
    setEditorOpen(true);
  };

  return (
    <>
      <Card
        title={
          <Space>
            <FileTextOutlined />
            <span>{t('common.exportTemplates.title')}</span>
          </Space>
        }
        extra={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('common.exportTemplates.create')}
          </Button>
        }
      >
        {lastUsed ? (
          <Alert
            type="info"
            showIcon
            icon={<ClockCircleOutlined />}
            style={{ marginBottom: 16 }}
            title={t('common.exportTemplates.lastUsedTitle')}
            description={
              <Space wrap>
                <Typography.Text>
                  {presetName(lastUsed.id, lastUsed.name, t)}
                  {lastUsedAt
                    ? ` · ${formatDateTime(lastUsedAt, formatLocale)}`
                    : ''}
                </Typography.Text>
                <Button
                  size="small"
                  type="primary"
                  icon={<ThunderboltOutlined />}
                  onClick={() => handleUse(lastUsed)}
                >
                  {t('common.exportTemplates.useAgain')}
                </Button>
              </Space>
            }
          />
        ) : null}

        {templates.length === 0 ? (
          <Empty description={t('common.exportTemplates.empty')} />
        ) : (
          <List
            dataSource={templates}
            renderItem={(item) => {
              const lines = summarizeConfig(item, t);
              const title = presetName(item.id, item.name, t);
              return (
                <List.Item
                  actions={[
                    <Button
                      key="use"
                      type="primary"
                      size="small"
                      icon={<ThunderboltOutlined />}
                      onClick={() => handleUse(item)}
                    >
                      {t('common.exportTemplates.use')}
                    </Button>,
                    <Button
                      key="edit"
                      size="small"
                      icon={<EditOutlined />}
                      onClick={() => openEdit(item)}
                    >
                      {item.isPreset
                        ? t('common.exportTemplates.customize')
                        : t('common.exportTemplates.edit')}
                    </Button>,
                  ]}
                >
                  <List.Item.Meta
                    title={
                      <Space wrap>
                        <Typography.Text strong>📝 {title}</Typography.Text>
                        {item.isPreset ? (
                          <Tag>{t('common.exportTemplates.badgePreset')}</Tag>
                        ) : null}
                        {item.shared ? (
                          <Tag color="blue">{t('common.exportTemplates.badgeShared')}</Tag>
                        ) : null}
                      </Space>
                    }
                    description={
                      <Space orientation="vertical" size={0}>
                        {lines.map((line) => (
                          <Typography.Text key={line} type="secondary">
                            — {line}
                          </Typography.Text>
                        ))}
                      </Space>
                    }
                  />
                </List.Item>
              );
            }}
          />
        )}
      </Card>

      <ExportTemplateEditorModal
        open={editorOpen}
        initial={editing}
        onCancel={() => {
          setEditorOpen(false);
          setEditing(null);
        }}
        currentUserId={userId}
        currentUserName={userName}
        onSave={(template) => {
          upsert(template);
          notify.success(t('common.exportTemplates.saved'));
          setEditorOpen(false);
          setEditing(null);
        }}
      />
    </>
  );
}
