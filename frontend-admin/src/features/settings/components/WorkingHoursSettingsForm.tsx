'use client';

import {
  CalendarOutlined,
  ClockCircleOutlined,
  PlusOutlined,
  SettingOutlined,
} from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Space,
  Switch,
  Table,
  TimePicker,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect } from 'react';

import { SaveButton } from '@/components/SaveButton';
import {
  WORKING_HOURS_DAY_KEYS,
  type WorkingHoursDayKey,
  type WorkingHoursSettings,
} from '@/features/settings/api/workingHoursApi';
import {
  useUpdateWorkingHoursSettings,
  useWorkingHoursSettings,
} from '@/features/settings/hooks/useWorkingHoursSettings';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

const DAY_LABEL_KEYS: Record<WorkingHoursDayKey, string> = {
  monday: 'settings.workingHours.days.monday',
  tuesday: 'settings.workingHours.days.tuesday',
  wednesday: 'settings.workingHours.days.wednesday',
  thursday: 'settings.workingHours.days.thursday',
  friday: 'settings.workingHours.days.friday',
  saturday: 'settings.workingHours.days.saturday',
  sunday: 'settings.workingHours.days.sunday',
};

function toDayjsTime(value: string | null | undefined): Dayjs | null {
  if (!value) return null;
  const parsed = dayjs(value, 'HH:mm', true);
  return parsed.isValid() ? parsed : null;
}

function fromDayjsTime(value: Dayjs | null): string {
  return value ? value.format('HH:mm') : '';
}

function toDayjsDate(value: string | null | undefined): Dayjs | null {
  if (!value) return null;
  const parsed = dayjs(value, 'YYYY-MM-DD', true);
  return parsed.isValid() ? parsed : null;
}

function fromDayjsDate(value: Dayjs | null): string {
  return value ? value.format('YYYY-MM-DD') : '';
}

export function WorkingHoursSettingsForm() {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const [form] = Form.useForm<WorkingHoursSettings>();
  const { data: settings, isLoading, refetch } = useWorkingHoursSettings();
  const updateSettings = useUpdateWorkingHoursSettings();

  useEffect(() => {
    if (settings) {
      form.setFieldsValue(settings);
    }
  }, [settings, form]);

  const handleSubmit = async (values: WorkingHoursSettings) => {
    try {
      await updateSettings.mutateAsync({
        ...values,
        specialDays: (values.specialDays ?? []).filter((d) => d?.date),
      });
      message.success(t('settings.workingHours.saveSuccess'));
      void refetch();
    } catch {
      message.error(t('settings.workingHours.saveFailed'));
    }
  };

  return (
    <Form form={form} layout="vertical" onFinish={handleSubmit} disabled={isLoading}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t('settings.workingHours.description')}
      </Typography.Paragraph>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('settings.workingHours.protectionNote')}
      />

      <Card
        title={
          <span>
            <ClockCircleOutlined style={{ marginRight: 8 }} />
            {t('settings.workingHours.weeklyTitle')}
          </span>
        }
        loading={isLoading}
      >
        {WORKING_HOURS_DAY_KEYS.map((day) => (
          <DayRow key={day} day={day} label={t(DAY_LABEL_KEYS[day])} form={form} t={t} />
        ))}
      </Card>

      <Card
        title={
          <span>
            <SettingOutlined style={{ marginRight: 8 }} />
            {t('settings.workingHours.autoSettingsTitle')}
          </span>
        }
        style={{ marginTop: 16 }}
        loading={isLoading}
      >
        <Form.Item
          name="reminderHoursBeforeClosing"
          label={t('settings.workingHours.reminderLabel')}
          tooltip={t('settings.workingHours.reminderTooltip')}
          rules={[
            { required: true, message: t('settings.workingHours.reminderRequired') },
            {
              type: 'number',
              min: 0,
              max: 12,
              message: t('settings.workingHours.reminderRange'),
            },
          ]}
        >
          <InputNumber min={0} max={12} style={{ width: 140 }} />
        </Form.Item>

        <Form.Item
          name="stopOnlineOrdersMinutesBeforeClose"
          label={t('settings.workingHours.stopOnlineOrdersLabel')}
          tooltip={t('settings.workingHours.stopOnlineOrdersTooltip')}
          rules={[
            {
              required: true,
              message: t('settings.workingHours.stopOnlineOrdersRequired'),
            },
            {
              type: 'number',
              min: 0,
              max: 180,
              message: t('settings.workingHours.stopOnlineOrdersRange'),
            },
          ]}
        >
          <InputNumber min={0} max={180} style={{ width: 140 }} />
        </Form.Item>

        <Form.Item
          name="autoClosePOSAtClosing"
          label={t('settings.workingHours.autoClosePosLabel')}
          tooltip={t('settings.workingHours.autoClosePosTooltip')}
          valuePropName="checked"
        >
          <Switch
            checkedChildren={t('settings.workingHours.closedOn')}
            unCheckedChildren={t('settings.workingHours.closedOff')}
          />
        </Form.Item>

        <Form.Item
          name="closedDayMessage"
          label={t('settings.workingHours.closedDayMessageLabel')}
          tooltip={t('settings.workingHours.closedDayMessageTooltip')}
          rules={[
            {
              required: true,
              message: t('settings.workingHours.closedDayMessageRequired'),
            },
            { max: 200, message: t('settings.workingHours.closedDayMessageMax') },
          ]}
        >
          <Input maxLength={200} placeholder="Heute geschlossen" />
        </Form.Item>
      </Card>

      <Card
        title={
          <span>
            <CalendarOutlined style={{ marginRight: 8 }} />
            {t('settings.workingHours.specialDaysTitle')}
          </span>
        }
        style={{ marginTop: 16 }}
        loading={isLoading}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('settings.workingHours.specialDaysDescription')}
        </Typography.Paragraph>

        <Form.List name="specialDays">
          {(fields, { add, remove }) => {
            const columns: ColumnsType<(typeof fields)[number]> = [
              {
                title: t('settings.workingHours.specialDayDate'),
                key: 'date',
                width: 180,
                render: (_, field) => (
                  <Form.Item
                    name={[field.name, 'date']}
                    rules={[
                      {
                        required: true,
                        message: t('settings.workingHours.specialDayDateRequired'),
                      },
                    ]}
                    getValueProps={(value: string | null | undefined) => ({
                      value: toDayjsDate(value),
                    })}
                    getValueFromEvent={(value: Dayjs | null) => fromDayjsDate(value)}
                    style={{ marginBottom: 0 }}
                  >
                    <DatePicker format="YYYY-MM-DD" style={{ width: '100%' }} />
                  </Form.Item>
                ),
              },
              {
                title: t('settings.workingHours.closed'),
                key: 'isClosed',
                width: 120,
                render: (_, field) => (
                  <Form.Item
                    name={[field.name, 'isClosed']}
                    valuePropName="checked"
                    style={{ marginBottom: 0 }}
                  >
                    <Switch
                      checkedChildren={t('settings.workingHours.statusClosed')}
                      unCheckedChildren={t('settings.workingHours.statusOpen')}
                    />
                  </Form.Item>
                ),
              },
              {
                title: t('settings.workingHours.openTime'),
                key: 'openTime',
                width: 140,
                render: (_, field) => (
                  <SpecialDayTimeCell
                    fieldName={field.name}
                    timeField="openTime"
                    form={form}
                    t={t}
                  />
                ),
              },
              {
                title: t('settings.workingHours.closeTime'),
                key: 'closeTime',
                width: 140,
                render: (_, field) => (
                  <SpecialDayTimeCell
                    fieldName={field.name}
                    timeField="closeTime"
                    form={form}
                    t={t}
                  />
                ),
              },
              {
                title: t('settings.workingHours.actions'),
                key: 'action',
                width: 100,
                render: (_, field) => (
                  <Button danger type="link" onClick={() => remove(field.name)}>
                    {t('settings.workingHours.removeSpecialDay')}
                  </Button>
                ),
              },
            ];

            return (
              <>
                <Table
                  size="small"
                  pagination={false}
                  rowKey="key"
                  dataSource={fields}
                  columns={columns}
                  locale={{
                    emptyText: t('settings.workingHours.specialDaysEmpty'),
                  }}
                />
                <Button
                  type="dashed"
                  onClick={() =>
                    add({
                      date: '',
                      isClosed: true,
                      openTime: null,
                      closeTime: null,
                    })
                  }
                  icon={<PlusOutlined />}
                  block
                  style={{ marginTop: 16 }}
                >
                  {t('settings.workingHours.addSpecialDay')}
                </Button>
              </>
            );
          }}
        </Form.List>
      </Card>

      <Form.Item style={{ marginTop: 16, marginBottom: 0 }}>
        <Space>
          <SaveButton
            htmlType="submit"
            loading={updateSettings.isPending}
            showShortcutInLabel={false}
          >
            {t('settings.page.saveChanges')}
          </SaveButton>
          <Button onClick={() => settings && form.setFieldsValue(settings)} disabled={!settings}>
            {t('settings.workingHours.reset')}
          </Button>
        </Space>
      </Form.Item>
    </Form>
  );
}

function DayRow({
  day,
  label,
  form,
  t,
}: {
  day: WorkingHoursDayKey;
  label: string;
  form: ReturnType<typeof Form.useForm<WorkingHoursSettings>>[0];
  t: (key: string) => string;
}) {
  const isClosed = Form.useWatch([day, 'isClosed'], form) ?? false;

  return (
    <div
      style={{
        display: 'flex',
        flexWrap: 'wrap',
        gap: 16,
        alignItems: 'center',
        marginBottom: 12,
      }}
    >
      <Typography.Text strong style={{ width: 120 }}>
        {label}
      </Typography.Text>

      <Form.Item
        name={[day, 'isClosed']}
        getValueProps={(closed: boolean | undefined) => ({ checked: !(closed ?? false) })}
        getValueFromEvent={(checked: boolean) => !checked}
        style={{ marginBottom: 0 }}
      >
        <Switch
          checkedChildren={t('settings.workingHours.statusOpen')}
          unCheckedChildren={t('settings.workingHours.statusClosed')}
        />
      </Form.Item>

      <Form.Item
        name={[day, 'openTime']}
        rules={[
          {
            required: !isClosed,
            message: t('settings.workingHours.timeRequired'),
          },
        ]}
        getValueProps={(value: string | null | undefined) => ({
          value: toDayjsTime(value),
        })}
        getValueFromEvent={(value: Dayjs | null) => fromDayjsTime(value)}
        style={{ marginBottom: 0 }}
      >
        <TimePicker
          format="HH:mm"
          placeholder={t('settings.workingHours.openTime')}
          disabled={isClosed}
          needConfirm={false}
          style={{ width: 120 }}
        />
      </Form.Item>

      <Form.Item
        name={[day, 'closeTime']}
        rules={[
          {
            required: !isClosed,
            message: t('settings.workingHours.timeRequired'),
          },
        ]}
        getValueProps={(value: string | null | undefined) => ({
          value: toDayjsTime(value),
        })}
        getValueFromEvent={(value: Dayjs | null) => fromDayjsTime(value)}
        style={{ marginBottom: 0 }}
      >
        <TimePicker
          format="HH:mm"
          placeholder={t('settings.workingHours.closeTime')}
          disabled={isClosed}
          needConfirm={false}
          style={{ width: 120 }}
        />
      </Form.Item>
    </div>
  );
}

function SpecialDayTimeCell({
  fieldName,
  timeField,
  form,
  t,
}: {
  fieldName: number;
  timeField: 'openTime' | 'closeTime';
  form: ReturnType<typeof Form.useForm<WorkingHoursSettings>>[0];
  t: (key: string) => string;
}) {
  const isClosed = Form.useWatch(['specialDays', fieldName, 'isClosed'], form) ?? false;

  return (
    <Form.Item
      name={[fieldName, timeField]}
      rules={[
        {
          required: !isClosed,
          message: t('settings.workingHours.timeRequired'),
        },
      ]}
      getValueProps={(value: string | null | undefined) => ({
        value: toDayjsTime(value),
      })}
      getValueFromEvent={(value: Dayjs | null) => fromDayjsTime(value) || null}
      style={{ marginBottom: 0 }}
    >
      <TimePicker
        format="HH:mm"
        disabled={isClosed}
        needConfirm={false}
        style={{ width: '100%' }}
      />
    </Form.Item>
  );
}
