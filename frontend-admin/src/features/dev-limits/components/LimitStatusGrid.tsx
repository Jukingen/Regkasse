'use client';

import { Card, Col, Progress, Row, Typography } from 'antd';

import {
  DEV_LIMIT_FIELD_META,
  DEV_LIMIT_KEYS,
  readDevLimitUsage,
} from '@/features/dev-limits/constants/limitKeys';
import {
  limitProgressStatus,
  limitProgressStroke,
  limitUsagePercent,
  limitUsageTone,
} from '@/features/dev-limits/utils/limitUsage';
import type { TenantLimitUsageDto } from '@/features/tenants/api/tenantLimits';
import { useI18n } from '@/i18n';

const { Text } = Typography;

type LimitStatusGridProps = {
  usage: TenantLimitUsageDto | undefined;
  loading?: boolean;
};

export function LimitStatusGrid({ usage, loading }: LimitStatusGridProps) {
  const { t } = useI18n();

  return (
    <Card title={t('tenants.limits.devPanel.statusTitle')} loading={loading && !usage}>
      <Row gutter={[16, 16]}>
        {DEV_LIMIT_KEYS.map((key) => {
          const pair = usage ? readDevLimitUsage(key, usage) : { current: 0, limit: 0 };
          const percent = limitUsagePercent(pair.current, pair.limit);
          const tone = limitUsageTone(percent);
          const money = DEV_LIMIT_FIELD_META[key].money;
          return (
            <Col xs={24} sm={12} lg={8} key={key}>
              <Text strong>{t(DEV_LIMIT_FIELD_META[key].labelKey)}</Text>
              <div style={{ marginTop: 4, marginBottom: 8 }}>
                <Text type="secondary">
                  {t('tenants.limits.devPanel.usage', {
                    current: money ? pair.current.toFixed(2) : pair.current,
                    limit: money ? pair.limit.toFixed(2) : pair.limit,
                  })}
                </Text>
              </div>
              <Progress
                percent={Math.min(100, percent)}
                status={limitProgressStatus(tone)}
                strokeColor={limitProgressStroke(tone)}
                format={() => `${percent}%`}
              />
            </Col>
          );
        })}
      </Row>
    </Card>
  );
}
