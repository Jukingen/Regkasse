'use client';

import { CopyOutlined } from '@ant-design/icons';
import { Button, Space, Typography } from 'antd';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { copyTextToClipboard } from '@/lib/clipboard';

export type CredentialCopyRowProps = {
  label: string;
  value: string;
  monospace?: boolean;
};

export function CredentialCopyRow({ label, value, monospace = true }: CredentialCopyRowProps) {
  const { message } = useAntdApp();

  const { t } = useI18n();

  const handleCopy = async () => {
    if (!value) {
      message.error(t('tenants.provisioning.copyFailed'));
      return;
    }
    const copied = await copyTextToClipboard(value);
    if (copied) {
      message.success(t('tenants.provisioning.copySuccess'));
    } else {
      message.error(t('tenants.provisioning.copyFailed'));
    }
  };

  return (
    <Space wrap align="center" style={{ width: '100%', justifyContent: 'space-between' }}>
      <Typography.Text>
        <Typography.Text strong>{label} </Typography.Text>
        <Typography.Text code={monospace} style={monospace ? { fontSize: 14 } : undefined}>
          {value}
        </Typography.Text>
      </Typography.Text>
      <Button size="small" icon={<CopyOutlined />} onClick={() => void handleCopy()}>
        {t('tenants.users.quick.result.copy')}
      </Button>
    </Space>
  );
}
