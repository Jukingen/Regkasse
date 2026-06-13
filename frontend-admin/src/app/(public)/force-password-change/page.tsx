'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Button, Card, Form, Input, Result, Space, Typography } from 'antd';
import { SimpleList as List } from '@/components/ui/SimpleList';
import {
    CheckCircleOutlined,
    CloseCircleOutlined,
    InfoCircleOutlined,
    LoginOutlined,
} from '@ant-design/icons';

import {
    PASSWORD_REQUIREMENT_KEYS,
    PASSWORD_REQUIREMENT_I18N_KEY,
    allPasswordRequirementsMet,
    getMetPasswordRequirementKeys,
    type PasswordRequirementKey,
} from '@/features/auth/lib/passwordRequirements';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

const { Text } = Typography;

const REDIRECT_SECONDS = 5;

const pageShellStyle = {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    minHeight: '100vh',
    background: '#f0f2f5',
} as const;

type ForcePasswordChangeFormValues = {
    currentPassword: string;
    newPassword: string;
    confirmPassword: string;
};

export default function ForcePasswordChangePage() {
    const { message } = useAntdApp();
    const { t } = useI18n();
    const [form] = Form.useForm<ForcePasswordChangeFormValues>();
    const [loading, setLoading] = useState(false);
    const [success, setSuccess] = useState(false);
    const [countdown, setCountdown] = useState(REDIRECT_SECONDS);
    const [metRequirementKeys, setMetRequirementKeys] = useState<PasswordRequirementKey[]>([]);
    const { changePassword, logout } = useAuth();
    const redirectedRef = useRef(false);

    const handleGoToLogin = useCallback(async () => {
        if (redirectedRef.current) {
            return;
        }
        redirectedRef.current = true;
        await logout({ silent: true, redirectTo: '/login?passwordChanged=1' });
    }, [logout]);

    useEffect(() => {
        if (!success) {
            return;
        }

        if (countdown <= 0) {
            void handleGoToLogin();
            return;
        }

        const timer = window.setTimeout(() => {
            setCountdown((current) => current - 1);
        }, 1000);

        return () => window.clearTimeout(timer);
    }, [success, countdown, handleGoToLogin]);

    const updatePasswordStrength = useCallback((password: string) => {
        setMetRequirementKeys(getMetPasswordRequirementKeys(password));
    }, []);

    const onFinish = async (values: ForcePasswordChangeFormValues) => {
        if (values.newPassword !== values.confirmPassword) {
            form.setFields([{ name: 'confirmPassword', errors: [t('settings.changePassword.confirmMismatch')] }]);
            return;
        }

        setLoading(true);
        try {
            await changePassword(values.currentPassword, values.newPassword);
            setSuccess(true);
            setCountdown(REDIRECT_SECONDS);
        } catch (error: unknown) {
            message.error(
                getUserFacingApiErrorMessage(t, error, {
                    fallbackKey: 'settings.changePassword.errorFallback',
                    logContext: 'forcePasswordChange',
                }),
            );
        } finally {
            setLoading(false);
        }
    };

    const validatePassword = (_: unknown, value: string) => {
        if (!value) {
            return Promise.reject(new Error(t('settings.changePassword.newPasswordRequired')));
        }
        if (!allPasswordRequirementsMet(value)) {
            return Promise.reject(new Error(t('settings.changePassword.requirementsNotMet')));
        }
        return Promise.resolve();
    };

    if (success) {
        return (
            <div style={pageShellStyle}>
                <Card style={{ width: 500, textAlign: 'center' }}>
                    <Result
                        status="success"
                        title={t('settings.changePassword.successTitle')}
                        subTitle={t('common.auth.passwordChangedLoginPrompt')}
                        extra={[
                            <Button
                                key="login"
                                type="primary"
                                icon={<LoginOutlined />}
                                onClick={() => void handleGoToLogin()}
                            >
                                {t('settings.changePassword.goToLogin')}
                            </Button>,
                        ]}
                    />
                    <div style={{ marginTop: 24 }}>
                        <Text type="secondary">
                            {t('settings.changePassword.redirectCountdown', { seconds: String(countdown) })}
                        </Text>
                    </div>
                </Card>
            </div>
        );
    }

    return (
        <div style={pageShellStyle}>
            <Card title={t('settings.changePassword.forceChangeTitle')} style={{ width: 500 }}>
                <Alert
                    title={t('settings.changePassword.forceChangeAlertTitle')}
                    description={t('settings.changePassword.forceChangeAlertDescription')}
                    type="warning"
                    showIcon
                    style={{ marginBottom: 24 }}
                />

                <div style={{ marginBottom: 16, background: '#f5f5f5', padding: 12, borderRadius: 8 }}>
                    <Text strong>{t('settings.changePassword.requirementsTitle')}</Text>
                    <List
                        size="small"
                        dataSource={[...PASSWORD_REQUIREMENT_KEYS]}
                        renderItem={(key) => {
                            const met = metRequirementKeys.includes(key);
                            return (
                                <List.Item style={{ padding: '4px 0', borderBlockEnd: 'none' }}>
                                    <Space>
                                        {met ? (
                                            <CheckCircleOutlined style={{ color: '#52c41a' }} />
                                        ) : (
                                            <CloseCircleOutlined style={{ color: '#d9d9d9' }} />
                                        )}
                                        <Text type={met ? 'success' : 'secondary'}>
                                            {t(PASSWORD_REQUIREMENT_I18N_KEY[key])}
                                        </Text>
                                    </Space>
                                </List.Item>
                            );
                        }}
                    />
                </div>

                <Form form={form} layout="vertical" onFinish={onFinish}>
                    <Form.Item
                        name="currentPassword"
                        label={t('settings.changePassword.currentPassword')}
                        rules={[{ required: true, message: t('settings.changePassword.currentPasswordRequired') }]}
                    >
                        <Input.Password
                            placeholder={t('settings.changePassword.temporaryPasswordPlaceholder')}
                            autoComplete="current-password"
                        />
                    </Form.Item>

                    <Form.Item
                        name="newPassword"
                        label={t('settings.changePassword.newPassword')}
                        rules={[
                            { required: true, message: t('settings.changePassword.newPasswordRequired') },
                            { validator: validatePassword },
                        ]}
                    >
                        <Input.Password
                            placeholder={t('settings.changePassword.newPasswordPlaceholder')}
                            autoComplete="new-password"
                            onChange={(e) => updatePasswordStrength(e.target.value)}
                        />
                    </Form.Item>

                    <Form.Item
                        name="confirmPassword"
                        label={t('settings.changePassword.confirmPassword')}
                        dependencies={['newPassword']}
                        rules={[
                            { required: true, message: t('settings.changePassword.confirmRequired') },
                            ({ getFieldValue }) => ({
                                validator(_, value) {
                                    if (!value || getFieldValue('newPassword') === value) {
                                        return Promise.resolve();
                                    }
                                    return Promise.reject(new Error(t('settings.changePassword.confirmMismatch')));
                                },
                            }),
                        ]}
                    >
                        <Input.Password
                            placeholder={t('settings.changePassword.confirmPasswordPlaceholder')}
                            autoComplete="new-password"
                        />
                    </Form.Item>

                    <Form.Item>
                        <Button type="primary" htmlType="submit" loading={loading} block>
                            {t('settings.changePassword.submit')}
                        </Button>
                    </Form.Item>
                </Form>

                <div style={{ marginTop: 16, textAlign: 'center' }}>
                    <Text type="secondary">
                        <InfoCircleOutlined /> {t('settings.changePassword.requirementsHint')}
                    </Text>
                </div>
            </Card>
        </div>
    );
}
