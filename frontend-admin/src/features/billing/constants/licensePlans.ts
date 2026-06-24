export const LICENSE_SALE_PLAN_VALUES = {
    sixMonths: '6_months',
    twelveMonths: '12_months',
    custom: 'custom',
} as const;

export type LicenseSalePlanValue =
    (typeof LICENSE_SALE_PLAN_VALUES)[keyof typeof LICENSE_SALE_PLAN_VALUES];

export const LICENSE_SALE_PLAN_OPTIONS: ReadonlyArray<{
    value: LicenseSalePlanValue;
    labelKey: string;
}> = [
    { value: LICENSE_SALE_PLAN_VALUES.sixMonths, labelKey: 'billing.plans.sixMonths' },
    { value: LICENSE_SALE_PLAN_VALUES.twelveMonths, labelKey: 'billing.plans.twelveMonths' },
    { value: LICENSE_SALE_PLAN_VALUES.custom, labelKey: 'billing.plans.custom' },
];

export const DEFAULT_LICENSE_VAT_RATE = 20;
