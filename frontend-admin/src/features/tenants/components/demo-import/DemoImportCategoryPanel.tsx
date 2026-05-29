'use client';

import { useMemo, useState } from 'react';
import { Avatar, Button, Checkbox, Input, Space, Typography } from 'antd';

import type { CategoryGroup } from './categoryGroups';
import type { DemoImportImageMode } from './demoImportImage';
import {
    applyPriceAdjustment,
    categoryAvatarColor,
    categoryAvatarLabel,
} from './demoImportImage';
import {
    isPriceAdjustmentActive,
    type DemoImportPriceAdjustmentState,
} from './priceAdjustment';
import {
    formatDemoProductName,
    formatEuro,
    PRODUCTS_PAGE_SIZE,
    type CatalogProduct,
} from './utils';

const { Text } = Typography;

export type DemoImportCategoryPanelProps = {
    group: CategoryGroup;
    products: CatalogProduct[];
    selectedProductIds: Set<string>;
    priceAdjustment?: DemoImportPriceAdjustmentState;
    imageMode?: DemoImportImageMode;
    onToggleProduct: (productId: string, selected: boolean) => void;
    onSetProductIds: (productIds: string[], selected: boolean) => void;
};

export function DemoImportCategoryPanel({
    group,
    products,
    selectedProductIds,
    priceAdjustment,
    imageMode = 'none',
    onToggleProduct,
    onSetProductIds,
}: DemoImportCategoryPanelProps) {
    const showAdjustedPrices =
        priceAdjustment != null && isPriceAdjustmentActive(priceAdjustment);
    const showPlaceholderPreview = imageMode === 'categoryPlaceholder';
    const [searchQuery, setSearchQuery] = useState('');
    const [visibleCount, setVisibleCount] = useState(PRODUCTS_PAGE_SIZE);

    const filteredProducts = useMemo(() => {
        const q = searchQuery.trim().toLowerCase();
        if (!q) return products;
        return products.filter(
            (p) =>
                p.name.toLowerCase().includes(q) ||
                p.category.toLowerCase().includes(q),
        );
    }, [products, searchQuery]);

    const visibleProducts = filteredProducts.slice(0, visibleCount);
    const hasMore = visibleCount < filteredProducts.length;

    return (
        <div style={{ paddingLeft: 8 }}>
            <Input.Search
                placeholder="In Kategorie suchen…"
                allowClear
                size="small"
                value={searchQuery}
                onChange={(e) => {
                    setSearchQuery(e.target.value);
                    setVisibleCount(PRODUCTS_PAGE_SIZE);
                }}
                style={{ marginBottom: 8, maxWidth: 320 }}
            />

            <Text type="secondary" style={{ display: 'block', marginBottom: 8, fontSize: 12 }}>
                {group.description}
            </Text>

            {filteredProducts.length === 0 ? (
                <Text type="secondary" style={{ fontSize: 12 }}>
                    Keine Produkte gefunden.
                </Text>
            ) : (
                <div
                    style={{
                        border: '1px solid #f0f0f0',
                        borderRadius: 6,
                        overflow: 'hidden',
                    }}
                >
                    {visibleProducts.map((product, index) => {
                        const checked = selectedProductIds.has(product.id);
                        const catalogPrice = Number(product.price);
                        const displayPrice = showAdjustedPrices
                            ? applyPriceAdjustment(catalogPrice, priceAdjustment!)
                            : catalogPrice;
                        const priceChanged = showAdjustedPrices && displayPrice !== catalogPrice;
                        return (
                            <div
                                key={product.id}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: 10,
                                    padding: '8px 12px',
                                    borderTop: index > 0 ? '1px solid #f0f0f0' : undefined,
                                    background: checked ? '#f6ffed' : undefined,
                                }}
                            >
                                <Checkbox
                                    checked={checked}
                                    onChange={(e) => onToggleProduct(product.id, e.target.checked)}
                                />
                                <Avatar
                                    size={32}
                                    style={{
                                        background: showPlaceholderPreview
                                            ? categoryAvatarColor(product.category)
                                            : '#fff7e6',
                                        color: showPlaceholderPreview ? '#fff' : '#d48806',
                                        flexShrink: 0,
                                        fontSize: showPlaceholderPreview ? 10 : 16,
                                        fontWeight: 600,
                                    }}
                                >
                                    {showPlaceholderPreview
                                        ? categoryAvatarLabel(product.category)
                                        : group.icon}
                                </Avatar>
                                <div style={{ flex: 1, minWidth: 0 }}>
                                    <Text ellipsis style={{ display: 'block' }}>
                                        {formatDemoProductName(product.name)}
                                    </Text>
                                </div>
                                <Space size={4} style={{ whiteSpace: 'nowrap' }}>
                                    {priceChanged ? (
                                        <Text
                                            delete
                                            type="secondary"
                                            style={{ fontVariantNumeric: 'tabular-nums', fontSize: 12 }}
                                        >
                                            {formatEuro(catalogPrice)}
                                        </Text>
                                    ) : null}
                                    <Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
                                        {formatEuro(displayPrice)}
                                    </Text>
                                </Space>
                                <Text type="secondary" style={{ fontSize: 12, whiteSpace: 'nowrap', minWidth: 72 }}>
                                    ({Number(product.taxRate)}%)
                                </Text>
                            </div>
                        );
                    })}
                </div>
            )}

            {hasMore ? (
                <div style={{ textAlign: 'right', marginTop: 8 }}>
                    <Button
                        type="link"
                        size="small"
                        onClick={() => setVisibleCount((c) => c + PRODUCTS_PAGE_SIZE)}
                    >
                        Mehr laden
                    </Button>
                </div>
            ) : null}
        </div>
    );
}
