'use client';

import { Alert, Card, Col, InputNumber, Row, Typography } from 'antd';
import React, { useMemo, useState } from 'react';

import {
  calculateTaxFromNet,
  grossDifference,
} from '@/features/tax/utils/taxPreviewMath';
import { useI18n } from '@/i18n';
import { formatCurrency } from '@/i18n/formatting';

export type TaxPreviewProps = {
  currentTaxRate: number | null | undefined;
  newTaxRate: number | null | undefined;
  /** Sample net amount in EUR (default 10). */
  defaultNetAmount?: number;
  /** Hide net amount editor when parent controls simulation inputs only via rates. */
  allowNetEdit?: boolean;
  style?: React.CSSProperties;
};

function PreviewPanel({
  title,
  background,
  netLabel,
  taxLabel,
  grossLabel,
}: {
  title: string;
  background: string;
  netLabel: string;
  taxLabel: string;
  grossLabel: string;
}) {
  return (
    <div>
      <Typography.Title level={5} style={{ marginTop: 0 }}>
        {title}
      </Typography.Title>
      <div
        style={{
          padding: 16,
          borderRadius: 8,
          background,
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
        }}
      >
        <div>{netLabel}</div>
        <div>{taxLabel}</div>
        <div style={{ fontWeight: 600 }}>{grossLabel}</div>
      </div>
    </div>
  );
}

/**
 * Side-by-side net/tax/gross preview for comparing two MwSt rates on a sample net price.
 */
export function TaxPreview({
  currentTaxRate,
  newTaxRate,
  defaultNetAmount = 10,
  allowNetEdit = true,
  style,
}: TaxPreviewProps) {
  const { t, formatLocale } = useI18n();
  const [netAmount, setNetAmount] = useState(defaultNetAmount);

  const current = useMemo(
    () => calculateTaxFromNet(netAmount, Number(currentTaxRate ?? 0)),
    [netAmount, currentTaxRate]
  );
  const next = useMemo(
    () => calculateTaxFromNet(netAmount, Number(newTaxRate ?? 0)),
    [netAmount, newTaxRate]
  );

  const priceDifference = grossDifference(current.gross, next.gross);
  const hasBothRates =
    currentTaxRate != null &&
    newTaxRate != null &&
    Number.isFinite(Number(currentTaxRate)) &&
    Number.isFinite(Number(newTaxRate));

  const money = (value: number) => formatCurrency(value, formatLocale, { currency: 'EUR' });

  let alertNode: React.ReactNode = null;
  if (hasBothRates) {
    if (Math.abs(priceDifference) < 0.005) {
      alertNode = (
        <Alert type="info" showIcon title={t('settings.taxGroups.preview.noChange')} />
      );
    } else if (priceDifference > 0) {
      alertNode = (
        <Alert
          type="warning"
          showIcon
          title={t('settings.taxGroups.preview.priceIncreases', {
            amount: money(priceDifference),
          })}
        />
      );
    } else {
      alertNode = (
        <Alert
          type="info"
          showIcon
          title={t('settings.taxGroups.preview.priceDecreases', {
            amount: money(Math.abs(priceDifference)),
          })}
        />
      );
    }
  }

  return (
    <Card title={t('settings.taxGroups.preview.cardTitle')} style={style}>
      {allowNetEdit ? (
        <div style={{ marginBottom: 16 }}>
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
            {t('settings.taxGroups.preview.netSample')}
          </Typography.Text>
          <InputNumber
            min={0}
            step={0.5}
            precision={2}
            value={netAmount}
            onChange={(v) => setNetAmount(typeof v === 'number' ? v : 0)}
            addonBefore="€"
            style={{ width: 180 }}
          />
        </div>
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} md={12}>
          <PreviewPanel
            title={t('settings.taxGroups.preview.current')}
            background="rgba(0,0,0,0.04)"
            netLabel={`${t('settings.taxGroups.preview.net')}: ${money(current.net)}`}
            taxLabel={`${t('settings.taxGroups.preview.tax', { rate: current.ratePercent })}: ${money(current.tax)}`}
            grossLabel={`${t('settings.taxGroups.preview.gross')}: ${money(current.gross)}`}
          />
        </Col>
        <Col xs={24} md={12}>
          <PreviewPanel
            title={t('settings.taxGroups.preview.next')}
            background="rgba(22,119,255,0.08)"
            netLabel={`${t('settings.taxGroups.preview.net')}: ${money(next.net)}`}
            taxLabel={`${t('settings.taxGroups.preview.tax', { rate: next.ratePercent })}: ${money(next.tax)}`}
            grossLabel={`${t('settings.taxGroups.preview.gross')}: ${money(next.gross)}`}
          />
        </Col>
      </Row>

      {alertNode ? <div style={{ marginTop: 16 }}>{alertNode}</div> : null}
    </Card>
  );
}
