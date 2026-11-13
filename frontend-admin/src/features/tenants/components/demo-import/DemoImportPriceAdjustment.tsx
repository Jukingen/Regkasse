'use client';

import { Button, InputNumber, Radio, Space, Typography } from 'antd';

import {
  DEFAULT_PRICE_ADJUSTMENT,
  type DemoImportPriceAdjustmentState,
  isPriceAdjustmentActive,
} from './priceAdjustment';

const { Text, Title } = Typography;

export type DemoImportPriceAdjustmentProps = {
  value: DemoImportPriceAdjustmentState;
  onChange: (value: DemoImportPriceAdjustmentState) => void;
  selectedProductCount: number;
};

export function DemoImportPriceAdjustmentSection({
  value,
  onChange,
  selectedProductCount,
}: DemoImportPriceAdjustmentProps) {
  const active = isPriceAdjustmentActive(value);

  const setMode = (mode: DemoImportPriceAdjustmentState['mode']) => {
    onChange({ ...value, mode });
  };

  return (
    <div
      style={{
        border: '1px solid #f0f0f0',
        borderRadius: 8,
        padding: 16,
        marginBottom: 16,
        background: '#fafafa',
      }}
    >
      <Title level={5} style={{ marginTop: 0, marginBottom: 12 }}>
        Preis Anpassung
      </Title>

      <Radio.Group
        value={value.mode}
        onChange={(e) => setMode(e.target.value)}
        style={{ width: '100%' }}
      >
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Radio value="none">Keine Anpassung</Radio>

          <Radio value="increasePercent">
            <Space wrap align="center">
              <span>Alle Preise um</span>
              <InputNumber
                size="small"
                min={0}
                max={1000}
                step={1}
                value={value.percent}
                disabled={value.mode !== 'increasePercent'}
                onChange={(v) => onChange({ ...value, percent: typeof v === 'number' ? v : 0 })}
                addonAfter="%"
                style={{ width: 100 }}
              />
              <span>erhöhen</span>
              <Button
                size="small"
                type={
                  value.mode === 'increasePercent' && value.percent === 10 ? 'primary' : 'default'
                }
                onClick={(e) => {
                  e.stopPropagation();
                  onChange({
                    ...DEFAULT_PRICE_ADJUSTMENT,
                    mode: 'increasePercent',
                    percent: 10,
                  });
                }}
              >
                +10%
              </Button>
            </Space>
          </Radio>

          <Radio value="decreasePercent">
            <Space wrap align="center">
              <span>Alle Preise um</span>
              <InputNumber
                size="small"
                min={0}
                max={100}
                step={1}
                value={value.percent}
                disabled={value.mode !== 'decreasePercent'}
                onChange={(v) => onChange({ ...value, percent: typeof v === 'number' ? v : 0 })}
                addonAfter="%"
                style={{ width: 100 }}
              />
              <span>reduzieren</span>
              <Button
                size="small"
                onClick={(e) => {
                  e.stopPropagation();
                  onChange({
                    ...value,
                    mode: 'decreasePercent',
                    percent: 5,
                  });
                }}
              >
                -5%
              </Button>
            </Space>
          </Radio>

          <Radio value="roundUpToIncrement">
            <Space wrap align="center">
              <span>Aufrunden auf</span>
              <InputNumber
                size="small"
                min={0.01}
                max={10}
                step={0.1}
                value={value.roundIncrement}
                disabled={value.mode !== 'roundUpToIncrement'}
                onChange={(v) =>
                  onChange({
                    ...value,
                    roundIncrement: typeof v === 'number' ? v : 0.5,
                  })
                }
                prefix="€"
                style={{ width: 110 }}
              />
              <Button
                size="small"
                onClick={(e) => {
                  e.stopPropagation();
                  onChange({
                    ...value,
                    mode: 'roundUpToIncrement',
                    roundIncrement: 0.5,
                  });
                }}
              >
                €0,50
              </Button>
            </Space>
          </Radio>
        </Space>
      </Radio.Group>

      <Text type="secondary" style={{ display: 'block', marginTop: 12, fontSize: 12 }}>
        Vorschau:{' '}
        <Text strong>
          {selectedProductCount} Produkt{selectedProductCount === 1 ? '' : 'e'}
        </Text>{' '}
        {active
          ? 'werden mit angepassten Preisen importiert'
          : 'werden mit Katalogpreisen importiert'}
      </Text>
    </div>
  );
}
