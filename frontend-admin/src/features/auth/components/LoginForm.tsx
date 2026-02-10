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

const { Title, Text } = Typography;

export const LoginForm: FC = () => {
    const router = useRouter();
    const queryClient = useQueryClient();

    const { mutate: login, isPending } = usePostApiAuthLogin({
        mutation: {
            onSuccess: async (data) => {
                const loginResponse = data as any;
                const token = loginResponse?.token;

                if (token) {
                    authStorage.setToken(token);
                    if (process.env.NODE_ENV === 'development') {
                        console.log('✅ [LoginForm] JWT Token saved to local storage');
                    }
                }

                message.success('Login successful');

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
                let apiMessage = responseData?.message || responseData?.title || error?.message || 'Unknown error';

                // If there are validation errors, append them
                if (responseData?.errors) {
                    const validationErrors = Object.values(responseData.errors).flat().join(', ');
                    if (validationErrors) {
                        apiMessage += `: ${validationErrors}`;
                    }
                }

                console.error('❌ [Login Failed] Full Error Object:', error);
                message.error(`Login failed (${status}): ${apiMessage}`);
            },
        }
    });

    const onFinish = (values: any) => {
        // Explicitly construct the payload matching LoginModel
        const payload: LoginModel = {
            email: values.username, // UI says 'Username' but backend expects 'email'
            password: values.password
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
                    <Text type="secondary">Sign in to manage your POS system</Text>
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
                        rules={[{ required: true, message: 'Please input your Username!' }]}
                    >
                        <Input prefix={<UserOutlined />} placeholder="Username" />
                    </Form.Item>

                    <Form.Item
                        name="password"
                        rules={[{ required: true, message: 'Please input your Password!' }]}
                    >
                        <Input.Password prefix={<LockOutlined />} placeholder="Password" />
                    </Form.Item>

                    <Form.Item>
                        <Button type="primary" htmlType="submit" block loading={isPending}>
                            Log in
                        </Button>
                    </Form.Item>
                </Form>
            </Card>
        </div>
    );
};
