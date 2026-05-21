'use client';

import React, { FC } from 'react';
import { Form, Input, Button, Card, Typography, message } from 'antd';
import { UserOutlined, LockOutlined } from '@ant-design/icons';
import { useRouter } from 'next/navigation';
import { usePostApiAuthLogin } from '@/api/generated/auth/auth';
import { useQueryClient } from '@tanstack/react-query';
import type { LoginModel } from '@/api/generated/model';
import { authStorage } from '@/features/auth/services/authStorage';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { AUTH_KEYS, fetchAuthUser } from '../hooks/useAuth';
import { useI18n } from '@/i18n';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

const { Title, Text } = Typography;

export const LoginForm: FC = () => {
    const router = useRouter();
    const queryClient = useQueryClient();
    const { t } = useI18n();

    const { mutate: login, isPending } = usePostApiAuthLogin({
        mutation: {
            onSuccess: async (data) => {
                const loginResponse = data as any;
                const token = loginResponse?.token;
                const refreshToken = loginResponse?.refreshToken;

                if (token) {
                    authStorage.setToken(token);
                    if (refreshToken) {
                        authStorage.setRefreshToken(refreshToken);
                    }
                    const loginUser = loginResponse?.user as
                        | { tenantId?: string | null; tenantSlug?: string | null }
                        | undefined;
                    tenantStorage.persistBootstrap({
                        tenantId: loginUser?.tenantId,
                        tenantSlug: loginUser?.tenantSlug,
                    });
                    if (process.env.NODE_ENV === 'development') {
                        technicalConsole.devLog('[LoginForm] JWT token pair saved to local storage (shared across tabs)');
                    }
                }

                message.success(t('common.auth.loginSuccess'));

                // No AuthContext: session user lives in TanStack Query (AUTH_KEYS.user). fetchQuery always runs /me
                // after token is stored so cache is populated before navigation (avoids stale error state).
                try {
                    await queryClient.fetchQuery({
                        queryKey: AUTH_KEYS.user,
                        queryFn: fetchAuthUser,
                    });
                } catch (bootstrapErr) {
                    technicalConsole.warn('[LoginForm] /me after login failed; aborting redirect', bootstrapErr);
                    message.error(t('common.auth.loginFailedGeneric'));
                    return;
                }

                const cachedUser = queryClient.getQueryData<{ mustChangePasswordOnNextLogin?: boolean }>(
                    AUTH_KEYS.user,
                );
                const mustChange =
                    cachedUser?.mustChangePasswordOnNextLogin === true ||
                    (loginResponse?.user as { mustChangePasswordOnNextLogin?: boolean } | undefined)
                        ?.mustChangePasswordOnNextLogin === true;

                if (process.env.NODE_ENV === 'development') {
                    technicalConsole.devLog(
                        '[LoginForm] user cache set; redirecting to',
                        mustChange ? '/settings (password)' : '/dashboard',
                    );
                }
                queueMicrotask(() => {
                    router.push(mustChange ? '/settings?mustChangePassword=1' : '/dashboard');
                });
            },
            onError: (error: unknown) => {
                message.error(
                    getUserFacingApiErrorMessage(t, error, {
                        logContext: 'LoginForm',
                        loginContext: true,
                        fallbackKey: 'common.auth.loginFailedGeneric',
                    }),
                );
            },
        }
    });

    const onFinish = (values: any) => {
        const payload: LoginModel = {
            email: values.username,
            password: values.password,
            clientApp: 'admin',
        };

        if (process.env.NODE_ENV === 'development') {
            technicalConsole.devLog('[LoginForm] submitting login request');
        }
        login({ data: payload });
    };

    return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', background: '#f0f2f5' }}>
            <Card style={{ width: 400, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
                <div style={{ textAlign: 'center', marginBottom: 24 }}>
                    <Title level={3}>{t('common.auth.appTitle')}</Title>
                    <Text type="secondary">{t('common.auth.subtitle')}</Text>
                </div>

                <Form
                    name="login"
                    initialValues={{ remember: true }}
                    onFinish={onFinish}
                    layout="vertical"
                    size="large"
                >
                    <Form.Item
                        name="username"
                        rules={[{ required: true, message: t('common.auth.validation.usernameRequired') }]}
                    >
                        <Input prefix={<UserOutlined />} placeholder={t('common.auth.username')} />
                    </Form.Item>

                    <Form.Item
                        name="password"
                        rules={[{ required: true, message: t('common.auth.validation.passwordRequired') }]}
                    >
                        <Input.Password prefix={<LockOutlined />} placeholder={t('common.auth.password')} />
                    </Form.Item>

                    <Form.Item>
                        <Button type="primary" htmlType="submit" block loading={isPending}>
                            {isPending ? t('common.auth.loginSubmitLoading') : t('common.auth.login')}
                        </Button>
                    </Form.Item>
                </Form>
            </Card>
        </div>
    );
};
