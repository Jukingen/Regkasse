'use client';

import { SafetyCertificateOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Input, Space, Typography } from 'antd';
import React, { useCallback, useState } from 'react';

import { extractApiErrorMessage } from '@/api/admin-rksv/client';
import {
  type RksvSignatureVerifyResponse,
  useRksvSignatureVerify,
} from '@/features/rksv/hooks/useRksvSignatureVerify';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n/I18nProvider';

const { TextArea } = Input;

export function SingleSignatureVerifyCard() {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const tp = useCallback((path: string) => t(`rksvHub.signatureVerifyPage.${path}`), [t]);

  const [signature, setSignature] = useState('');
  const [certificateThumbprint, setCertificateThumbprint] = useState('');
  const [verificationResult, setVerificationResult] = useState<RksvSignatureVerifyResponse | null>(
    null
  );

  const { mutate: verifySignature, isPending: verifying } = useRksvSignatureVerify();

  const handleVerify = () => {
    const trimmed = signature.trim();
    if (!trimmed) {
      message.warning(tp('emptySignatureWarning'));
      return;
    }

    setVerificationResult(null);
    verifySignature(
      {
        signature: trimmed,
        certificateThumbprint: certificateThumbprint.trim() || null,
      },
      {
        onSuccess: (data) => setVerificationResult(data),
        onError: (error) => {
          const msg = extractApiErrorMessage(error, tp('verifyFailed'));
          message.error(msg);
        },
      }
    );
  };

  return (
    <Card size="small">
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <TextArea
          rows={6}
          placeholder={tp('signaturePlaceholder')}
          value={signature}
          onChange={(event) => setSignature(event.target.value)}
        />
        <Input
          placeholder={tp('thumbprintPlaceholder')}
          value={certificateThumbprint}
          onChange={(event) => setCertificateThumbprint(event.target.value)}
          allowClear
        />
        <Button
          type="primary"
          icon={<SafetyCertificateOutlined />}
          onClick={handleVerify}
          loading={verifying}
        >
          {tp('verifyButton')}
        </Button>

        {verificationResult ? (
          <Alert
            type={verificationResult.valid ? 'success' : 'error'}
            title={verificationResult.valid ? tp('validTitle') : tp('invalidTitle')}
            description={
              <Space orientation="vertical" size={4}>
                <span>{verificationResult.details}</span>
                {verificationResult.certificateThumbprintUsed ? (
                  <Typography.Text type="secondary" code style={{ fontSize: 12 }}>
                    {verificationResult.certificateThumbprintUsed}
                  </Typography.Text>
                ) : null}
              </Space>
            }
            showIcon
          />
        ) : null}
      </Space>
    </Card>
  );
}
