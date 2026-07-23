'use client';

import { IdcardOutlined, MailOutlined, UserOutlined } from '@ant-design/icons';
import { Avatar, Button, Card, Descriptions, Divider, Form, Input, Space } from 'antd';
import { useEffect, useMemo, useState } from 'react';

import { FormSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { ProfileUsernamePolicyAlert } from '@/features/user/components/ProfileUsernamePolicyAlert';
import { SelfServiceUsernameModal } from '@/features/user/components/SelfServiceUsernameModal';
import { useProfile, useUpdateProfile } from '@/features/user/hooks/useProfile';
import { useUsernameChangePolicy } from '@/features/user/hooks/useUsernameChangePolicy';
import { ProfilePermissionRequestCard } from '@/features/users/components/ProfilePermissionRequestCard';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

type ProfileFormValues = {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
};

function resolveRoleLabel(role: string | null | undefined, t: (key: string) => string): string {
  if (!role) return '—';
  return t(`users.roles.displayNames.${role}`);
}

function displayValue(value: string | null | undefined, emptyLabel: string): string {
  const trimmed = value?.trim();
  return trimmed ? trimmed : emptyLabel;
}

export default function ProfilePage() {
  const { data: profile, isLoading, isError } = useProfile();
  const { data: usernamePolicy, isLoading: isUsernamePolicyLoading } = useUsernameChangePolicy();
  const { mutateAsync: updateProfile, isPending } = useUpdateProfile();
  const { logout } = useAuth();
  const { message } = useAntdApp();
  const { t } = useI18n();
  const [form] = Form.useForm<ProfileFormValues>();
  const [usernameModalOpen, setUsernameModalOpen] = useState(false);

  const breadcrumbs = useMemo(
    () => [adminOverviewCrumb(t), { title: t('profile.pageTitle') }],
    [t]
  );

  useEffect(() => {
    if (!profile) return;
    form.setFieldsValue({
      firstName: profile.firstName ?? '',
      lastName: profile.lastName ?? '',
      email: profile.email ?? '',
      phoneNumber: profile.phoneNumber ?? '',
    });
  }, [profile, form]);

  const onFinish = async (values: ProfileFormValues) => {
    try {
      await updateProfile({
        firstName: values.firstName.trim(),
        lastName: values.lastName.trim(),
        email: values.email.trim(),
        phoneNumber: values.phoneNumber?.trim() || null,
      });
      message.success(t('profile.saveSuccess'));
    } catch (error) {
      openApiErrorMessage(message.open, t, error, {
        fallbackKey: 'profile.saveError',
        logContext: 'ProfilePage.update',
      });
    }
  };

  if (isLoading) {
    return (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        <FormSkeleton fields={5} />
      </>
    );
  }

  if (isError || !profile) {
    return (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
          <AdminPageHeader title={t('profile.pageTitle')} breadcrumbs={breadcrumbs} />
          <Card variant="borderless">{t('profile.loadError')}</Card>
        </div>
      </>
    );
  }

  const fullName =
    `${profile.firstName ?? ''} ${profile.lastName ?? ''}`.trim() || profile.userName || '—';
  const emptyLabel = t('profile.emptyValue');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('profile.pageTitle')} breadcrumbs={breadcrumbs} />

      <Card variant="borderless">
        <div style={{ display: 'flex', marginBottom: 24, alignItems: 'center' }}>
          <Avatar size={80} icon={<UserOutlined />} />
          <div style={{ marginLeft: 24 }}>
            <h2 style={{ margin: 0 }}>{fullName}</h2>
            <p style={{ margin: '8px 0 0' }}>
              <MailOutlined /> {displayValue(profile.email, t('profile.notProvided'))}
            </p>
            <p style={{ margin: '4px 0 0' }}>
              <IdcardOutlined /> {displayValue(profile.employeeNumber, t('profile.notProvided'))}
            </p>
          </div>
        </div>

        <Divider />

        <ProfileUsernamePolicyAlert policy={usernamePolicy} isLoading={isUsernamePolicyLoading} />

        <Descriptions bordered column={{ xs: 1, sm: 2 }} style={{ marginBottom: 24 }}>
          <Descriptions.Item label={t('profile.fields.userName')}>
            <Space>
              <span>{displayValue(profile.userName, emptyLabel)}</span>
              <Button
                size="small"
                onClick={() => setUsernameModalOpen(true)}
                disabled={usernamePolicy?.canChange === false}
              >
                {t('profile.username.changeAction')}
              </Button>
            </Space>
          </Descriptions.Item>
          <Descriptions.Item label={t('profile.fields.role')}>
            {resolveRoleLabel(profile.role, t)}
          </Descriptions.Item>
          <Descriptions.Item label={t('profile.fields.employeeNumber')}>
            {displayValue(profile.employeeNumber, emptyLabel)}
          </Descriptions.Item>
          <Descriptions.Item label={t('profile.fields.phoneNumber')}>
            {displayValue(profile.phoneNumber, emptyLabel)}
          </Descriptions.Item>
        </Descriptions>

        <Divider />

        <Form form={form} layout="vertical" onFinish={onFinish}>
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
              gap: 16,
            }}
          >
            <Form.Item
              name="firstName"
              label={t('profile.fields.firstName')}
              rules={[{ required: true, message: t('profile.validation.firstNameRequired') }]}
            >
              <Input placeholder={t('profile.placeholders.firstName')} />
            </Form.Item>

            <Form.Item
              name="lastName"
              label={t('profile.fields.lastName')}
              rules={[{ required: true, message: t('profile.validation.lastNameRequired') }]}
            >
              <Input placeholder={t('profile.placeholders.lastName')} />
            </Form.Item>
          </div>

          <Form.Item
            name="email"
            label={t('profile.fields.email')}
            rules={[
              { required: true, message: t('profile.validation.emailRequired') },
              { type: 'email', message: t('profile.validation.emailInvalid') },
            ]}
          >
            <Input placeholder={t('profile.placeholders.email')} />
          </Form.Item>

          <Form.Item name="phoneNumber" label={t('profile.fields.phoneNumber')}>
            <Input placeholder={t('profile.placeholders.phoneNumber')} />
          </Form.Item>

          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={isPending}>
                {t('common.buttons.save')}
              </Button>
              <Button onClick={() => form.resetFields()} disabled={isPending}>
                {t('profile.reset')}
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Card>

      <ProfilePermissionRequestCard />

      <SelfServiceUsernameModal
        open={usernameModalOpen}
        currentUsername={profile.userName ?? ''}
        userEmail={profile.email}
        usernamePolicy={usernamePolicy}
        onClose={() => setUsernameModalOpen(false)}
        onSuccess={() => void logout({ redirectTo: '/login' })}
      />
    </div>
  );
}
