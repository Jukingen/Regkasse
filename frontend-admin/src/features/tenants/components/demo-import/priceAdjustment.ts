import type { DemoImportRequest } from '@/api/admin/products';

export type DemoImportPriceAdjustmentMode =
    | 'none'
    | 'increasePercent'
    | 'decreasePercent'
    | 'roundUpToIncrement';

export type DemoImportPriceAdjustmentState = {
    mode: DemoImportPriceAdjustmentMode;
    percent: number;
    roundIncrement: number;
};

export const DEFAULT_PRICE_ADJUSTMENT: DemoImportPriceAdjustmentState = {
    mode: 'increasePercent',
    percent: 10,
    roundIncrement: 0.5,
};

export function isPriceAdjustmentActive(state: DemoImportPriceAdjustmentState): boolean {
    return state.mode !== 'none';
}

export function applyPriceAdjustment(price: number, state: DemoImportPriceAdjustmentState): number {
    const base = Number(price);
    if (!Number.isFinite(base) || state.mode === 'none') {
        return roundPrice(base);
    }

    switch (state.mode) {
        case 'increasePercent': {
            const pct = clampPercent(state.percent);
            return roundPrice(base * (1 + pct / 100));
        }
        case 'decreasePercent': {
            const pct = clampPercent(state.percent);
            return roundPrice(Math.max(0, base * (1 - pct / 100)));
        }
        case 'roundUpToIncrement': {
            const inc = state.roundIncrement > 0 ? state.roundIncrement : 0.5;
            return roundPrice(Math.ceil(base / inc) * inc);
        }
        default:
            return roundPrice(base);
    }
}

export function sumAdjustedSelectedValue(
    products: Array<{ id: string; price: number }>,
    selectedIds: Set<string>,
    adjustment: DemoImportPriceAdjustmentState,
): number {
    return products
        .filter((p) => selectedIds.has(p.id))
        .reduce((sum, p) => sum + applyPriceAdjustment(Number(p.price), adjustment), 0);
}

export function toPriceAdjustmentRequest(
    state: DemoImportPriceAdjustmentState,
): Pick<DemoImportRequest, 'priceAdjustmentMode' | 'priceAdjustmentPercent' | 'priceRoundIncrement'> {
    if (state.mode === 'none') {
        return {};
    }

    return {
        priceAdjustmentMode: state.mode,
        priceAdjustmentPercent: state.percent,
        priceRoundIncrement: state.roundIncrement,
    };
}

function roundPrice(value: number): number {
    return Math.round((value + Number.EPSILON) * 100) / 100;
}

function clampPercent(value: number): number {
    if (!Number.isFinite(value)) return 0;
    return Math.min(1000, Math.max(0, value));
}
