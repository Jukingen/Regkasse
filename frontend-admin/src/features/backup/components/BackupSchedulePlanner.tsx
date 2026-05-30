'use client';

import React from 'react';
import { Input, Radio, Select, Space, TimePicker, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import utc from 'dayjs/plugin/utc';
import { useI18n } from '@/i18n';
import type { BackupSchedulePlannerState } from '@/features/backup/logic/backupScheduleCronCodec';

dayjs.extend(utc);

export interface BackupSchedulePlannerProps {
  value: BackupSchedulePlannerState;
  onChange: (next: BackupSchedulePlannerState) => void;
  disabled?: boolean;
  showCustomCronInvalid?: boolean;
}

const DAY_OF_WEEK_OPTIONS = [0, 1, 2, 3, 4, 5, 6] as const;
const DAY_OF_MONTH_OPTIONS = Array.from({ length: 31 }, (_, i) => i + 1);

export function BackupSchedulePlanner({
  value,
  onChange,
  disabled = false,
  showCustomCronInvalid = false,
}: BackupSchedulePlannerProps) {
  const { t } = useI18n();

  const timeValue = dayjs
    .utc()
    .startOf('day')
    .add(value.hourUtc, 'hour')
    .add(value.minuteUtc, 'minute');

  const onTimeChange = (d: Dayjs | null) => {
    if (!d) return;
    const utcTime = d.utc();
    onChange({
      ...value,
      hourUtc: utcTime.hour(),
      minuteUtc: utcTime.minute(),
    });
  };

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <div>
        <Typography.Text strong>{t('backupDr.scheduleSettings.frequencyLabel')}</Typography.Text>
        <Radio.Group
          style={{ display: 'block', marginTop: 8 }}
          value={value.frequency}
          onChange={(e) =>
            onChange({
              ...value,
              frequency: e.target.value,
            })
          }
          disabled={disabled}
        >
          <Space direction="vertical">
            <Radio value="Daily">{t('backupDr.scheduleSettings.frequencyDaily')}</Radio>
            <Radio value="Weekly">{t('backupDr.scheduleSettings.frequencyWeekly')}</Radio>
            <Radio value="Monthly">{t('backupDr.scheduleSettings.frequencyMonthly')}</Radio>
            <Radio value="Custom">{t('backupDr.scheduleSettings.presetCustom')}</Radio>
          </Space>
        </Radio.Group>
      </div>

      {value.frequency !== 'Custom' ? (
        <Space wrap align="center">
          <Typography.Text>{t('backupDr.scheduleSettings.timeOfDayUtc')}</Typography.Text>
          <TimePicker
            value={timeValue}
            onChange={onTimeChange}
            format="HH:mm"
            disabled={disabled}
            showNow={false}
            needConfirm={false}
          />
        </Space>
      ) : null}

      {value.frequency === 'Weekly' ? (
        <div>
          <Typography.Text>{t('backupDr.scheduleSettings.dayOfWeek')}</Typography.Text>
          <Select
            style={{ width: '100%', marginTop: 8, maxWidth: 320 }}
            value={value.dayOfWeek}
            disabled={disabled}
            onChange={(dow) => onChange({ ...value, dayOfWeek: dow })}
            options={DAY_OF_WEEK_OPTIONS.map((dow) => ({
              value: dow,
              label: t(`backupDr.scheduleSettings.weekday.${dow}`),
            }))}
          />
        </div>
      ) : null}

      {value.frequency === 'Monthly' ? (
        <div>
          <Typography.Text>{t('backupDr.scheduleSettings.dayOfMonth')}</Typography.Text>
          <Select
            style={{ width: '100%', marginTop: 8, maxWidth: 160 }}
            value={value.dayOfMonth}
            disabled={disabled}
            onChange={(dom) => onChange({ ...value, dayOfMonth: dom })}
            options={DAY_OF_MONTH_OPTIONS.map((dom) => ({
              value: dom,
              label: String(dom),
            }))}
          />
        </div>
      ) : null}

      {value.frequency === 'Custom' ? (
        <div style={{ maxWidth: 480 }}>
          <Input
            value={value.customCron}
            onChange={(e) => onChange({ ...value, customCron: e.target.value })}
            placeholder={t('backupDr.scheduleSettings.customCronPlaceholder')}
            disabled={disabled}
            status={showCustomCronInvalid ? 'error' : undefined}
          />
          {showCustomCronInvalid ? (
            <Typography.Text type="danger" style={{ display: 'block', marginTop: 4 }}>
              {t('backupDr.scheduleSettings.customCronInvalid')}
            </Typography.Text>
          ) : null}
        </div>
      ) : null}

      <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
        {t('backupDr.scheduleSettings.utcHint')}
      </Typography.Paragraph>
    </Space>
  );
}
