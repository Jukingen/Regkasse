'use client';

import { Card, Typography } from 'antd';
import type { CSSProperties } from 'react';

import { useI18n } from '@/i18n';

const { Paragraph, Text, Title } = Typography;

const PRIVACY_EMAIL = 'privacy@regkasse.at';

type Props = {
  /** Optional card style override. */
  style?: CSSProperties;
};

/**
 * GDPR / RKSV retention policy summary for mandant account & data-management surfaces.
 * Content mirrors backend purge rules (non-RKSV deleted; fiscal/RKSV retained ≥7 years).
 */
export function DataRetentionPolicyCard({ style }: Props) {
  const { t } = useI18n();

  return (
    <Card title={t('dataManagement.policy.cardTitle')} style={style}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <section>
          <Title level={5} style={{ marginTop: 0, marginBottom: 8 }}>
            {t('dataManagement.policy.legalTitle')}
          </Title>
          <ul style={{ margin: 0, paddingLeft: 20 }}>
            <li>
              <Text type="secondary">{t('dataManagement.policy.legal.rksv')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('dataManagement.policy.legal.gdpr')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('dataManagement.policy.legal.business')}</Text>
            </li>
          </ul>
        </section>

        <section>
          <Title level={5} style={{ marginTop: 0, marginBottom: 8 }}>
            {t('dataManagement.policy.deletedTitle')}
          </Title>
          <ul style={{ margin: 0, paddingLeft: 20 }}>
            <li>
              <Text type="secondary">{t('dataManagement.policy.deleted.products')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('dataManagement.policy.deleted.customers')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('dataManagement.policy.deleted.nonFiscal')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('dataManagement.policy.deleted.users')}</Text>
            </li>
          </ul>
        </section>

        <section>
          <Title level={5} style={{ marginTop: 0, marginBottom: 8 }}>
            {t('dataManagement.policy.retainedTitle')}
          </Title>
          <ul style={{ margin: 0, paddingLeft: 20 }}>
            <li>
              <Text type="secondary">{t('dataManagement.policy.retained.payments')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('dataManagement.policy.retained.audit')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('dataManagement.policy.retained.tse')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('dataManagement.policy.retained.ordersVouchers')}</Text>
            </li>
          </ul>
        </section>

        <div
          style={{
            marginTop: 4,
            padding: 16,
            borderRadius: 8,
            background: 'var(--ant-color-info-bg, #e6f4ff)',
          }}
        >
          <Paragraph style={{ marginBottom: 0 }} type="secondary">
            {t('dataManagement.policy.footerNote')}{' '}
            <a href={`mailto:${PRIVACY_EMAIL}`}>{PRIVACY_EMAIL}</a>
          </Paragraph>
        </div>
      </div>
    </Card>
  );
}
