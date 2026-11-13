'use client';

import { QuestionCircleOutlined } from '@ant-design/icons';
import { Card, Descriptions, Space, Tooltip, Typography } from 'antd';

type Flag = {
  key: string;
  label: string;
  tooltip: string;
  value: string;
};

type Props = {
  title: string;
  flags: Flag[];
  warnings?: string[];
  warningTooltip?: string;
};

export function TagesberichtReconciliationFlags({ title, flags, warnings, warningTooltip }: Props) {
  return (
    <Card title={title} style={{ marginBottom: 16 }} data-testid="tagesbericht-reconciliation">
      <Descriptions column={1} size="small" bordered>
        {flags.map((flag) => (
          <Descriptions.Item
            key={flag.key}
            label={
              <Space size={6}>
                <span>{flag.label}</span>
                <Tooltip title={flag.tooltip}>
                  <QuestionCircleOutlined aria-label={flag.tooltip} />
                </Tooltip>
              </Space>
            }
          >
            {flag.value}
          </Descriptions.Item>
        ))}
      </Descriptions>
      {warnings?.length ? (
        <ul>
          {warnings.map((w) => (
            <li key={w}>
              <Typography.Text type="warning" title={warningTooltip}>
                {w}
              </Typography.Text>
            </li>
          ))}
        </ul>
      ) : null}
    </Card>
  );
}
