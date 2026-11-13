'use client';

import { BellOutlined, SaveOutlined } from '@ant-design/icons';
import { Alert, Button, Card, List, Space, Switch, Typography } from 'antd';
import React, { useEffect, useState } from 'react';

import {
  defaultDepExportMobilePushSettings,
  useDepExportPushSettings,
  useSaveDepExportPushSettings,
  type DepExportMobilePushSettings,
} from '@/features/rksv/hooks/useDepExportPushSettings';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

type Props = {
  style?: React.CSSProperties;
};

type ToggleKey = keyof Omit<DepExportMobilePushSettings, 'pushEnabled'>;

const TOGGLE_ITEMS: { key: ToggleKey; titleKey: string; descKey: string }[] = [
  {
    key: 'thirtyDayReminder',
    titleKey: 'rksvHub.depExportPushSettings.thirtyDayTitle',
    descKey: 'rksvHub.depExportPushSettings.thirtyDayDesc',
  },
  {
    key: 'sevenDayReminder',
    titleKey: 'rksvHub.depExportPushSettings.sevenDayTitle',
    descKey: 'rksvHub.depExportPushSettings.sevenDayDesc',
  },
  {
    key: 'oneDayReminder',
    titleKey: 'rksvHub.depExportPushSettings.oneDayTitle',
    descKey: 'rksvHub.depExportPushSettings.oneDayDesc',
  },
  {
    key: 'overdueAlert',
    titleKey: 'rksvHub.depExportPushSettings.overdueTitle',
    descKey: 'rksvHub.depExportPushSettings.overdueDesc',
  },
  {
    key: 'successNotification',
    titleKey: 'rksvHub.depExportPushSettings.successTitle',
    descKey: 'rksvHub.depExportPushSettings.successDesc',
  },
];

export function DepExportPushSettingsCard({ style }: Props) {
  const { t } = useI18n();
  const notify = useNotify();
  const { data, isLoading, isFetching, error, refetch } = useDepExportPushSettings();
  const save = useSaveDepExportPushSettings();
  const [draft, setDraft] = useState<DepExportMobilePushSettings>(defaultDepExportMobilePushSettings());
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    if (!data || dirty) return;
    setDraft(data);
  }, [data, dirty]);

  const setToggle = (key: keyof DepExportMobilePushSettings, value: boolean) => {
    setDirty(true);
    setDraft((prev) => ({ ...prev, [key]: value }));
  };

  const onSave = () => {
    save.mutate(draft, {
      onSuccess: (saved) => {
        setDraft(saved);
        setDirty(false);
        notify.successKey('rksvHub.depExportPushSettings.saved');
      },
      onError: (err) => {
        notify.apiError(err, {
          logContext: 'DepExportPushSettings.save',
          fallbackKey: 'rksvHub.depExportPushSettings.saveFailed',
        });
      },
    });
  };

  return (
    <Card
      title={
        <Space>
          <BellOutlined />
          <span>{t('rksvHub.depExportPushSettings.title')}</span>
        </Space>
      }
      loading={isLoading}
      style={style}
      extra={
        <Space>
          <Button
            onClick={() => {
              setDirty(false);
              void refetch();
            }}
            loading={isFetching}
          >
            {t('rksvHub.depExportPushSettings.refresh')}
          </Button>
          <Button
            type="primary"
            icon={<SaveOutlined />}
            loading={save.isPending}
            disabled={!dirty}
            onClick={onSave}
          >
            {t('rksvHub.depExportPushSettings.save')}
          </Button>
        </Space>
      }
    >
      {error ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('rksvHub.depExportPushSettings.loadFailed')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={error}
              logContext="DepExportPushSettings.load"
              fallbackKey="rksvHub.depExportPushSettings.loadFailed"
            />
          }
        />
      ) : null}

      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: 12,
          gap: 16,
        }}
      >
        <div>
          <Typography.Text strong>{t('rksvHub.depExportPushSettings.masterTitle')}</Typography.Text>
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
              {t('rksvHub.depExportPushSettings.masterDesc')}
            </Typography.Text>
          </div>
        </div>
        <Switch
          checked={draft.pushEnabled}
          onChange={(checked) => setToggle('pushEnabled', checked)}
        />
      </div>

      <List
        bordered
        dataSource={TOGGLE_ITEMS}
        renderItem={(item) => (
          <List.Item
            actions={[
              <Switch
                key="sw"
                checked={draft[item.key]}
                disabled={!draft.pushEnabled}
                onChange={(checked) => setToggle(item.key, checked)}
              />,
            ]}
          >
            <List.Item.Meta title={t(item.titleKey)} description={t(item.descKey)} />
          </List.Item>
        )}
      />

      <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0, fontSize: 12 }}>
        {t('rksvHub.depExportPushSettings.disclaimer')}
      </Typography.Paragraph>
    </Card>
  );
}
