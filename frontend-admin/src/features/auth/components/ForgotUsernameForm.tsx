'use client';

import React, { FC, useState } from 'react';
import { App, Button, Card, Form, Input, Typography } from 'antd';
import { MailOutlined } from '@ant-design/icons';
import Link from 'next/link';
import { useMutation } from '@tanstack/react-query';

import { requestForgotUsername } from '@/features/auth/api/forgotUsername';
import { useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

const { Title, Text, Paragraph } = Typography;

type ForgotUsernameFormValues = {
    email: string;
};

export const ForgotUsernameForm: FC = () => {
    const { message } = App.useApp();
    const { t } = useI18n();
    const [submitted, setSubmitted] = useState(false);

    const { mutate, isPending } = useMutation({
        mutationFn: (values: ForgotUsernameFormValues) => requestForgotUsername(values.email),
        onSuccess: () => {
            setSubmitted(true);
            message.success(t('common.auth.forgotUsername.successToast'));
        },
        onError: (error: unknown) => {
            message.error(
                getUserFacingApiErrorMessage(t, error, {
                    logContext: 'ForgotUsernameForm',
                    fallbackKey: 'common.auth.forgotUsername.errorGeneric',
                }),
            );
        },
    });

    const onFinish = (values: ForgotUsernameFormValues) => {
        mutate(values);
    };

    return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh', background: '#f0f2f5', padding: 24 }}>
            <Card style={{ width: 400, maxWidth: '100%', boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
                <div style={{ textAlign: 'center', marginBottom: 24 }}>
                    <Title level={3}>{t('common.auth.forgotUsername.title')}</Title>
                    <Text type="secondary">{t('common.auth.forgotUsername.subtitle')}</Text>
                </div>

                {submitted ? (
                    <Paragraph>{t('common.auth.forgotUsername.confirmation')}</Paragraph>
                ) : (
                    <Form<ForgotUsernameFormValues> layout="vertical" size="large" onFinish={onFinish}>
                        <Form.Item
                            name="email"
                            label={t('common.auth.forgotUsername.emailLabel')}
                            rules={[
                                { required: true, message: t('common.auth.forgotUsername.emailRequired') },
                                { type: 'email', message: t('common.auth.forgotUsername.emailInvalid') },
                            ]}
                        >
                            <Input
                                prefix={<MailOutlined />}
                                placeholder={t('common.auth.forgotUsername.emailPlaceholder')}
                                autoComplete="email"
                            />
                        </Form.Item>
                        <Form.Item>
                            <Button type="primary" htmlType="submit" block loading={isPending}>
                                {t('common.auth.forgotUsername.submit')}
                            </Button>
                        </Form.Item>
                    </Form>
                )}

                <div style={{ textAlign: 'center', marginTop: 16 }}>
                    <Link href="/login">{t('common.auth.forgotUsername.backToLogin')}</Link>
                </div>
            </Card>
        </div>
    );
};
