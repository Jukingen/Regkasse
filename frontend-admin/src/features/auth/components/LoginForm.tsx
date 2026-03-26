'use client';

import React, { FC } from 'react';
import { Form, Input, Button, Card, Typography, message } from 'antd';
import { UserOutlined, LockOutlined } from '@ant-design/icons';
import { useRouter } from 'next/navigation';
import { usePostApiAuthLogin } from '@/api/generated/auth/auth';
import { useQueryClient } from '@tanstack/react-query';
import type { LoginModel } from '@/api/generated/model';
import { authStorage } from '@/features/auth/services/authStorage';
import { AUTH_KEYS } from '../hooks/useAuth';
import { useI18n } from '@/i18n';

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
                    if (process.env.NODE_ENV === 'development') {
                        console.log('✅ [LoginForm] JWT token pair saved to session storage');
                    }
                }

                message.success(t('common.auth.loginSuccess'));

                // Invalidate 'me' query to fetch user profile and update auth state
                await queryClient.invalidateQueries({ queryKey: AUTH_KEYS.user });

                if (process.env.NODE_ENV === 'development') {
                    console.log('✅ [LoginForm] Redirecting to /dashboard...');
                }
                router.replace('/dashboard');
            },
            onError: (error: any) => {
                const responseData = error?.response?.data;
                const status = error?.response?.status;

                // Try to extract validation errors common in .NET Core (title, errors object)
                let apiMessage = responseData?.message || responseData?.title || error?.message || t('common.messages.unknownError');

                // If there are validation errors, append them
                if (responseData?.errors) {
                    const validationErrors = Object.values(responseData.errors).flat().join(', ');
                    if (validationErrors) {
                        apiMessage += `: ${validationErrors}`;
                    }
                }

                if (process.env.NODE_ENV === 'development') {
                    console.error('❌ [Login Failed]', {
                        status,
                        message: error?.message,
                        responseMessage: responseData?.message ?? responseData?.title
                    });
                }
                message.error(t('common.auth.loginFailedWithStatus', { status: status ?? '-', details: apiMessage }));
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
            console.log('🔵 [LoginForm] Submitting login request...');
        }
        login({ data: payload });
    };

    return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', background: '#f0f2f5' }}>
            <Card style={{ width: 400, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
                <div style={{ textAlign: 'center', marginBottom: 24 }}>
                    <Title level={3}>Regkasse Admin</Title>
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
                            {t('common.auth.login')}
                        </Button>
                    </Form.Item>
                </Form>
            </Card>
        </div>
    );
};
