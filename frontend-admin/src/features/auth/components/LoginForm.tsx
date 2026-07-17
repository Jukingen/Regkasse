'use client';

import React, { FC, useEffect } from 'react';
import { App, Form, Input, Button, Card, Typography } from 'antd';
import { UserOutlined, LockOutlined } from '@ant-design/icons';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { usePostApiAuthLogin } from '@/api/generated/auth/auth';
import { useQueryClient } from '@tanstack/react-query';
import type { LoginModel } from '@/api/generated/model';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import {
    AUTH_KEYS,
    clearStaleAuthBeforeLogin,
    fetchAuthUser,
    persistLoginTokensAndSettle,
} from '../hooks/useAuth';
import { useI18n } from '@/i18n';
import { getDefaultLandingPathFromStorage } from '@/lib/personalization/PersonalizationProvider';
import { CHANGE_PASSWORD_PATH } from '@/features/auth/constants/changePasswordRoute';
import { userPreferencesQueryKey } from '@/lib/personalization/userPreferencesApi';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { buildLoginFormRules } from '@/features/auth/constants/loginValidation';

const { Title, Text } = Typography;

type LoginFormValues = {
    loginIdentifier: string;
    password: string;
};

export const LoginForm: FC = () => {
    const { message } = App.useApp();
    const router = useRouter();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();
    const { t } = useI18n();
    const loginRules = buildLoginFormRules(t);

    useEffect(() => {
        if (searchParams.get('passwordChanged') === '1') {
            message.info(t('common.auth.passwordChangedLoginPrompt'));
        }
    }, [message, searchParams, t]);

    const { mutate: login, isPending } = usePostApiAuthLogin({
        mutation: {
            onSuccess: async (data) => {
                const loginResponse = data as any;
                const token = loginResponse?.token;
                const refreshToken = loginResponse?.refreshToken;

                if (token) {
                    await persistLoginTokensAndSettle(token, refreshToken);
                    const loginUser = loginResponse?.user as
                        | { tenantId?: string | null; tenantSlug?: string | null }
                        | undefined;
                    // Bootstrap slug is enough for X-Tenant-Id (resolveTenantSlugForApiRequest).
                    // Do not call writeDevTenantSlug here — it dispatches DEV_TENANT_CHANGED_EVENT,
                    // which clears the JWT and reloads mid-login.
                    tenantStorage.persistBootstrap({
                        tenantId: loginUser?.tenantId,
                        tenantSlug: loginUser?.tenantSlug,
                    });
                    if (process.env.NODE_ENV === 'development') {
                        technicalConsole.devLog('[LoginForm] JWT token pair saved to local storage (shared across tabs)');
                    }
                }

                message.success(t('common.auth.loginSuccess'));

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

                await queryClient.invalidateQueries({ queryKey: userPreferencesQueryKey });
                const landingPath = getDefaultLandingPathFromStorage();
                if (process.env.NODE_ENV === 'development') {
                    technicalConsole.devLog(
                        '[LoginForm] user cache set; redirecting to',
                        mustChange ? CHANGE_PASSWORD_PATH : landingPath,
                    );
                }
                queueMicrotask(() => {
                    router.push(mustChange ? CHANGE_PASSWORD_PATH : landingPath);
                });
            },
            onError: (error: unknown) => {
                const err = error as { code?: string; message?: string; name?: string };
                if (
                    err.code === 'ERR_CANCELED' ||
                    err.name === 'CanceledError' ||
                    err.name === 'AbortError' ||
                    err.message === 'canceled' ||
                    err.message === 'Query was cancelled'
                ) {
                    return;
                }
                message.error(
                    getUserFacingApiErrorMessage(t, error, {
                        logContext: 'LoginForm',
                        loginContext: true,
                        fallbackKey: 'common.auth.invalidCredentials',
                    }),
                );
            },
        },
    });

    const onFinish = (values: LoginFormValues) => {
        const loginIdentifier = values.loginIdentifier.trim();
        const payload: LoginModel = {
            loginIdentifier,
            email: loginIdentifier,
            password: values.password,
            clientApp: 'admin',
        };

        if (process.env.NODE_ENV === 'development') {
            technicalConsole.devLog('[LoginForm] submitting login request');
        }
        clearStaleAuthBeforeLogin(queryClient);
        login({ data: payload });
    };

    return (
        <div
            style={{
                position: 'relative',
                display: 'flex',
                justifyContent: 'center',
                alignItems: 'center',
                height: '100vh',
                background: '#f0f2f5',
            }}
        >
            <div style={{ position: 'absolute', top: 16, right: 16 }}>
                <LanguageSwitcher data-testid="login-language-switcher" />
            </div>
            <Card style={{ width: 400, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
                <div style={{ textAlign: 'center', marginBottom: 24 }}>
                    <Title level={3}>{t('common.auth.appTitle')}</Title>
                    <Text type="secondary">{t('common.auth.subtitle')}</Text>
                </div>

                <Form<LoginFormValues>
                    name="login"
                    initialValues={{ remember: true }}
                    onFinish={onFinish}
                    layout="vertical"
                    size="large"
                >
                    <Form.Item
                        name="loginIdentifier"
                        label={t('common.auth.loginIdentifierLabel')}
                        tooltip={t('common.auth.loginIdentifierTooltip')}
                        extra={
                            <Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 4 }}>
                                {t('common.auth.loginIdentifierCaseHint')}
                            </Text>
                        }
                        rules={loginRules.loginIdentifier}
                    >
                        <Input
                            prefix={<UserOutlined />}
                            placeholder={t('common.auth.loginIdentifierPlaceholder')}
                            autoComplete="username"
                        />
                    </Form.Item>

                    <Form.Item
                        name="password"
                        label={t('common.auth.password')}
                        rules={loginRules.password}
                    >
                        <Input.Password
                            prefix={<LockOutlined />}
                            placeholder={t('common.auth.passwordPlaceholder')}
                            autoComplete="current-password"
                        />
                    </Form.Item>

                    <Form.Item>
                        <Button type="primary" htmlType="submit" block loading={isPending}>
                            {isPending ? t('common.auth.loginSubmitLoading') : t('common.auth.login')}
                        </Button>
                    </Form.Item>

                    <div style={{ textAlign: 'center' }}>
                        <Link href="/login/forgot-username">{t('common.auth.forgotUsernameLink')}</Link>
                    </div>
                </Form>
            </Card>
        </div>
    );
};
