'use client';

import { Button, Card, Select, Space, Typography } from 'antd';
import { useState } from 'react';

import {
  DEV_LIMIT_FIELD_META,
  DEV_LIMIT_KEYS,
  DEV_LIMIT_SCENARIO_LABEL_KEYS,
  DEV_LIMIT_SCENARIOS,
  type DevLimitKey,
  type DevLimitScenario,
} from '@/features/dev-limits/constants/limitKeys';
import { useI18n } from '@/i18n';

const { Text } = Typography;

const SCENARIO_BUTTON: Record<DevLimitScenario, { type?: 'primary'; danger?: boolean }> = {
  near: {},
  at: { type: 'primary' },
  tiny: { danger: true },
  reset: {},
};

type LimitTestScenariosProps = {
  disabled?: boolean;
  loading?: boolean;
  onTrigger: (scenario: DevLimitScenario, limitKey?: DevLimitKey) => Promise<void>;
};

export function LimitTestScenarios({ disabled, loading, onTrigger }: LimitTestScenariosProps) {
  const { t } = useI18n();
  const [limitKey, setLimitKey] = useState<DevLimitKey | 'all'>('all');

  const run = (scenario: DevLimitScenario) =>
    onTrigger(scenario, limitKey === 'all' ? undefined : limitKey);

  return (
    <Card title={t('tenants.limits.devPanel.scenariosTitle')}>
      <Space orientation="vertical" size={12} style={{ width: '100%' }}>
        <div>
          <Text type="secondary">{t('tenants.limits.devPanel.limit')}</Text>
          <Select
            style={{ display: 'block', marginTop: 8, maxWidth: 420 }}
            value={limitKey}
            disabled={disabled}
            onChange={(value) => setLimitKey(value)}
            options={[
              { value: 'all', label: t('tenants.limits.devPanel.allKeys') },
              ...DEV_LIMIT_KEYS.map((key) => ({
                value: key,
                label: t(DEV_LIMIT_FIELD_META[key].labelKey),
              })),
            ]}
          />
        </div>
        <Space wrap>
          {DEV_LIMIT_SCENARIOS.map((scenario) => (
            <Button
              key={scenario}
              {...SCENARIO_BUTTON[scenario]}
              disabled={disabled}
              loading={loading}
              onClick={() => void run(scenario)}
            >
              {t(DEV_LIMIT_SCENARIO_LABEL_KEYS[scenario])}
            </Button>
          ))}
        </Space>
        <Text type="secondary">{t('tenants.limits.devPanel.scenariosHint')}</Text>
      </Space>
    </Card>
  );
}
