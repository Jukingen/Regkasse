'use client';

import { LockOutlined, UserOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Form, Input, Typography } from 'antd';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import React, { FC, useEffect, useState } from 'react';

import { usePostApiAuthLogin } from '@/api/generated/auth/auth';
import type { LoginModel } from '@/api/generated/model';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';
import {
  TwoFactorAuth,
  type TwoFactorChallengeState,
  type TwoFactorLoginSuccess,
} from '@/features/auth/components/TwoFactorAuth';
import { CHANGE_PASSWORD_PATH } from '@/features/auth/constants/changePasswordRoute';
import { buildLoginFormRules } from '@/features/auth/constants/loginValidation';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { writeDevTenantSlug } from '@/features/auth/services/devTenant';
import { useI18n } from '@/i18n';
import { getDefaultLandingPathFromStorage } from '@/lib/personalization/PersonalizationProvider';
import { userPreferencesQueryKey } from '@/lib/personalization/userPreferencesApi';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

import {
  AUTH_KEYS,
  clearStaleAuthBeforeLogin,
  fetchAuthUserWithRetry,
  persistLoginTokensAndSettle,
} from '../hooks/useAuth';

const { Title, Text } = Typography;

type LoginFormValues = {
  loginIdentifier: string;
  password: string;
};

type LoginApiResponse = {
  token?: string;
  refreshToken?: string | null;
  requires2FA?: boolean;
  requires2FASetup?: boolean;
  twoFactorToken?: string;
  authenticatorKey?: string | null;
  authenticatorUri?: string | null;
  isDevelopment?: boolean;
  developmentBypassCode?: string | null;
  user?: {
    tenantId?: string | null;
    tenantSlug?: string | null;
    mustChangePasswordOnNextLogin?: boolean;
  };
};

export const LoginForm: FC = () => {
  const { message } = App.useApp();
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const { t } = useI18n();
  const loginRules = buildLoginFormRules(t);
  const [twoFactorChallenge, setTwoFactorChallenge] = useState<TwoFactorChallengeState | null>(
    null
  );

  useEffect(() => {
    if (searchParams.get('passwordChanged') === '1') {
      message.info(t('common.auth.passwordChangedLoginPrompt'));
    }
  }, [message, searchParams, t]);

  const finishAuthenticatedSession = async (
    loginResponse: TwoFactorLoginSuccess | LoginApiResponse
  ) => {
    const token = loginResponse?.token;
    const refreshToken = loginResponse?.refreshToken;

    if (token) {
      await persistLoginTokensAndSettle(token, refreshToken);
      const loginUser = loginResponse?.user;
      tenantStorage.persistBootstrap({
        tenantId: loginUser?.tenantId,
        tenantSlug: loginUser?.tenantSlug,
      });
      // In development, automatically prefer "dev" when login resolved unused legacy `default` / platform slug.
      // Do not pair legacy JWT tenant id with slug `dev` — HeaderDevTenantSwitch / TenantGuard rebind correctly.
      if (process.env.NODE_ENV === 'development') {
        const slug = loginUser?.tenantSlug?.trim().toLowerCase() ?? '';
        // Silent: must not fire DEV_TENANT_CHANGED_EVENT (that clears JWT via useTenantChangeListener).
        if (!slug || slug === 'platform' || slug === 'admin') {
          writeDevTenantSlug('dev', undefined, { silent: true });
        } else if (slug === 'dev') {
          writeDevTenantSlug('dev', loginUser?.tenantId, { silent: true });
        }
        technicalConsole.devLog(
          '[LoginForm] JWT token pair saved to local storage (shared across tabs)'
        );
      }
    }

    try {
      // Wrap: React Query passes QueryFunctionContext as arg0 — must not bind retry params.
      await queryClient.fetchQuery({
        queryKey: AUTH_KEYS.user,
        queryFn: () => fetchAuthUserWithRetry(),
      });
    } catch (bootstrapErr) {
      technicalConsole.warn('[LoginForm] /me after login failed; aborting redirect', bootstrapErr);
      message.error(t('common.auth.loginFailedGeneric'));
      return;
    }

    message.success(t('common.auth.loginSuccess'));

    const cachedUser = queryClient.getQueryData<{ mustChangePasswordOnNextLogin?: boolean }>(
      AUTH_KEYS.user
    );
    const mustChange =
      cachedUser?.mustChangePasswordOnNextLogin === true ||
      loginResponse?.user?.mustChangePasswordOnNextLogin === true;

    await queryClient.invalidateQueries({ queryKey: userPreferencesQueryKey });
    const landingPath = getDefaultLandingPathFromStorage();
    if (process.env.NODE_ENV === 'development') {
      technicalConsole.devLog(
        '[LoginForm] user cache set; redirecting to',
        mustChange ? CHANGE_PASSWORD_PATH : landingPath
      );
    }
    queueMicrotask(() => {
      router.push(mustChange ? CHANGE_PASSWORD_PATH : landingPath);
    });
  };

  const { mutate: login, isPending } = usePostApiAuthLogin({
    mutation: {
      onSuccess: async (data) => {
        const loginResponse = data as unknown as LoginApiResponse;

        if (loginResponse?.requires2FA && loginResponse.twoFactorToken) {
          setTwoFactorChallenge({
            twoFactorToken: loginResponse.twoFactorToken,
            requires2FASetup: loginResponse.requires2FASetup === true,
            authenticatorKey: loginResponse.authenticatorKey,
            authenticatorUri: loginResponse.authenticatorUri,
            isDevelopment: loginResponse.isDevelopment === true,
            developmentBypassCode: loginResponse.developmentBypassCode,
          });
          return;
        }

        await finishAuthenticatedSession(loginResponse);
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
          })
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
    setTwoFactorChallenge(null);
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
          <Title level={3}>
            {twoFactorChallenge ? t('common.auth.twoFactor.title') : t('common.auth.appTitle')}
          </Title>
          {!twoFactorChallenge ? <Text type="secondary">{t('common.auth.subtitle')}</Text> : null}
        </div>

        {twoFactorChallenge ? (
          <TwoFactorAuth
            challenge={twoFactorChallenge}
            onSuccess={async (data) => {
              setTwoFactorChallenge(null);
              await finishAuthenticatedSession(data);
            }}
            onCancel={() => setTwoFactorChallenge(null)}
          />
        ) : (
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
        )}
      </Card>
    </div>
  );
};
