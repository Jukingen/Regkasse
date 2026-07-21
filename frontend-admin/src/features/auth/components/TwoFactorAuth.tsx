'use client';

import { SafetyCertificateOutlined } from '@ant-design/icons';
import { Alert, App, Button, Form, Input, Typography } from 'antd';
import React, { FC, useState } from 'react';

import { useEnvironment } from '@/hooks/useEnvironment';
import { useI18n } from '@/i18n';
import { customInstance } from '@/lib/axios';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

const { Text, Paragraph } = Typography;

/** Matches backend `ITwoFactorService.DevelopmentBypassToken`. */
const DEV_2FA_BYPASS = 'DEV-2FA-BYPASS';

export type TwoFactorChallengeState = {
  twoFactorToken: string;
  requires2FASetup: boolean;
  authenticatorKey?: string | null;
  authenticatorUri?: string | null;
  isDevelopment?: boolean;
  developmentBypassCode?: string | null;
};

export type TwoFactorLoginSuccess = {
  token: string;
  refreshToken?: string | null;
  user?: {
    tenantId?: string | null;
    tenantSlug?: string | null;
    mustChangePasswordOnNextLogin?: boolean;
  };
};

type TwoFactorAuthProps = {
  challenge: TwoFactorChallengeState;
  onSuccess: (data: TwoFactorLoginSuccess) => void | Promise<void>;
  onCancel: () => void;
};

type FormValues = {
  code: string;
};

async function verifyTwoFactorCode(
  twoFactorToken: string,
  code: string
): Promise<TwoFactorLoginSuccess> {
  return customInstance<TwoFactorLoginSuccess>({
    url: '/api/Auth/verify-2fa',
    method: 'POST',
    data: {
      twoFactorToken,
      code,
    },
  });
}

/**
 * SuperAdmin 2FA step after password login.
 * Development: info alert + one-click bypass (no authenticator).
 * Production: real TOTP form.
 */
export const TwoFactorAuth: FC<TwoFactorAuthProps> = (props) => {
  const { isDevelopment } = useEnvironment();
  const showDevBypass = isDevelopment || props.challenge.isDevelopment === true;

  if (showDevBypass) {
    return <DevelopmentTwoFactorBypass {...props} />;
  }

  return <RealTwoFactorAuth {...props} />;
};

const DevelopmentTwoFactorBypass: FC<TwoFactorAuthProps> = ({ challenge, onSuccess, onCancel }) => {
  const { message } = App.useApp();
  const { t } = useI18n();
  const [pending, setPending] = useState(false);

  const bypass2FA = async () => {
    setPending(true);
    try {
      const code = challenge.developmentBypassCode?.trim() || DEV_2FA_BYPASS;
      const data = await verifyTwoFactorCode(challenge.twoFactorToken, code);
      if (!data?.token) {
        message.error(t('common.auth.twoFactor.invalidCode'));
        return;
      }
      await onSuccess(data);
    } catch (error: unknown) {
      message.error(
        getUserFacingApiErrorMessage(t, error, {
          logContext: 'TwoFactorAuth.devBypass',
          loginContext: true,
          fallbackKey: 'common.auth.twoFactor.invalidCode',
        })
      );
      technicalConsole.warn('[TwoFactorAuth] development bypass failed', error);
    } finally {
      setPending(false);
    }
  };

  return (
    <>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={t('common.auth.twoFactor.devModeTitle')}
        description={t('common.auth.twoFactor.devModeDescription')}
        action={
          <Button size="small" type="primary" loading={pending} onClick={() => void bypass2FA()}>
            {t('common.auth.twoFactor.devContinue')}
          </Button>
        }
      />
      <Button type="link" block onClick={onCancel} disabled={pending}>
        {t('common.auth.twoFactor.backToLogin')}
      </Button>
    </>
  );
};

const RealTwoFactorAuth: FC<TwoFactorAuthProps> = ({ challenge, onSuccess, onCancel }) => {
  const { message } = App.useApp();
  const { t } = useI18n();
  const [pending, setPending] = useState(false);

  const onFinish = async (values: FormValues) => {
    setPending(true);
    try {
      const data = await verifyTwoFactorCode(challenge.twoFactorToken, values.code.trim());
      if (!data?.token) {
        message.error(t('common.auth.twoFactor.invalidCode'));
        return;
      }
      await onSuccess(data);
    } catch (error: unknown) {
      message.error(
        getUserFacingApiErrorMessage(t, error, {
          logContext: 'TwoFactorAuth',
          loginContext: true,
          fallbackKey: 'common.auth.twoFactor.invalidCode',
        })
      );
      technicalConsole.warn('[TwoFactorAuth] verify failed', error);
    } finally {
      setPending(false);
    }
  };

  return (
    <>
      <div style={{ textAlign: 'center', marginBottom: 16 }}>
        <SafetyCertificateOutlined style={{ fontSize: 32, color: '#1677ff' }} />
        <Paragraph style={{ marginTop: 12, marginBottom: 0 }}>
          {challenge.requires2FASetup
            ? t('common.auth.twoFactor.setupSubtitle')
            : t('common.auth.twoFactor.verifySubtitle')}
        </Paragraph>
      </div>

      {challenge.requires2FASetup && challenge.authenticatorKey ? (
        <div
          style={{
            marginBottom: 16,
            padding: 12,
            background: '#fafafa',
            borderRadius: 8,
            border: '1px solid #f0f0f0',
          }}
        >
          <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
            {t('common.auth.twoFactor.setupHint')}
          </Text>
          <Text code copyable style={{ wordBreak: 'break-all' }}>
            {challenge.authenticatorKey}
          </Text>
        </div>
      ) : null}

      <Form<FormValues> layout="vertical" size="large" onFinish={(v) => void onFinish(v)}>
        <Form.Item
          name="code"
          label={t('common.auth.twoFactor.codeLabel')}
          rules={[
            { required: true, message: t('common.auth.twoFactor.codeRequired') },
            {
              pattern: /^\s*\d{6}\s*$/,
              message: t('common.auth.twoFactor.codePattern'),
            },
          ]}
        >
          <Input
            prefix={<SafetyCertificateOutlined />}
            placeholder={t('common.auth.twoFactor.codePlaceholder')}
            autoComplete="one-time-code"
            inputMode="numeric"
            maxLength={8}
            autoFocus
          />
        </Form.Item>

        <Form.Item style={{ marginBottom: 8 }}>
          <Button type="primary" htmlType="submit" block loading={pending}>
            {t('common.auth.twoFactor.verify')}
          </Button>
        </Form.Item>

        <Button type="link" block onClick={onCancel} disabled={pending}>
          {t('common.auth.twoFactor.backToLogin')}
        </Button>
      </Form>
    </>
  );
};
