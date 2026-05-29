'use client';

import { useMemo, useState } from 'react';
import { Button, Dropdown, InputNumber, Space, Typography, type MenuProps } from 'antd';
import { DownOutlined } from '@ant-design/icons';

import {
    formatTaxRateLabel,
    taxRatesInProducts,
    type CatalogProduct,
} from './utils';

const { Text } = Typography;

export type DemoImportBulkMenuProps = {
    products: CatalogProduct[];
    onSetProductIds: (productIds: string[], selected: boolean) => void;
    size?: 'small' | 'middle';
};

export function DemoImportBulkMenu({ products, onSetProductIds, size = 'small' }: DemoImportBulkMenuProps) {
    const [priceMin, setPriceMin] = useState<number | null>(null);
    const [priceMax, setPriceMax] = useState<number | null>(null);

    const availableTaxRates = useMemo(() => taxRatesInProducts(products), [products]);

    const selectByPriceRange = () => {
        const min = priceMin ?? 0;
        const max = priceMax ?? Number.POSITIVE_INFINITY;
        const ids = products
            .filter((p) => {
                const price = Number(p.price);
                return price >= min && price <= max;
            })
            .map((p) => p.id);
        onSetProductIds(ids, true);
    };

    const menuItems: MenuProps['items'] = [
        {
            key: 'select-all',
            label: 'Alle auswählen',
            onClick: () => onSetProductIds(products.map((p) => p.id), true),
        },
        {
            key: 'deselect-all',
            label: 'Alle abwählen',
            onClick: () => onSetProductIds(products.map((p) => p.id), false),
        },
        { type: 'divider' },
        {
            key: 'price-range',
            label: (
                <div onClick={(e) => e.stopPropagation()} style={{ minWidth: 220, padding: '4px 0' }}>
                    <Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 8 }}>
                        Nach Preisbereich
                    </Text>
                    <Space size="small" style={{ marginBottom: 8 }}>
                        <InputNumber
                            size="small"
                            min={0}
                            step={0.1}
                            placeholder="Min €"
                            value={priceMin}
                            onChange={(v) => setPriceMin(typeof v === 'number' ? v : null)}
                            style={{ width: 96 }}
                        />
                        <span>–</span>
                        <InputNumber
                            size="small"
                            min={0}
                            step={0.1}
                            placeholder="Max €"
                            value={priceMax}
                            onChange={(v) => setPriceMax(typeof v === 'number' ? v : null)}
                            style={{ width: 96 }}
                        />
                    </Space>
                    <Button type="link" size="small" onClick={selectByPriceRange} style={{ padding: 0 }}>
                        Anwenden
                    </Button>
                </div>
            ),
        },
        ...(availableTaxRates.length > 0
            ? [
                  { type: 'divider' as const },
                  {
                      key: 'tax-header',
                      label: <Text type="secondary" style={{ fontSize: 12 }}>Nach Steuersatz</Text>,
                      disabled: true,
                  },
                  ...availableTaxRates.map((rate) => ({
                      key: `tax-${rate}`,
                      label: formatTaxRateLabel(rate),
                      onClick: () => {
                          const ids = products.filter((p) => Number(p.taxRate) === rate).map((p) => p.id);
                          onSetProductIds(ids, true);
                      },
                  })),
              ]
            : []),
    ];

    return (
        <Dropdown menu={{ items: menuItems }} trigger={['click']}>
            <Button size={size} onClick={(e) => e.stopPropagation()}>
                Alle <DownOutlined />
            </Button>
        </Dropdown>
    );
}
