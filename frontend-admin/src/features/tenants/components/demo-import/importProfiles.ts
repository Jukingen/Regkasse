import type { CategoryGroup } from '@/features/tenants/components/demo-import/categoryGroups';
import type { DemoImportImageMode } from '@/features/tenants/components/demo-import/demoImportImage';
import type { DemoImportPriceAdjustmentState } from '@/features/tenants/components/demo-import/priceAdjustment';
import type { CatalogProduct } from '@/features/tenants/components/demo-import/utils';
import { getGroupProductIds } from '@/features/tenants/components/demo-import/utils';

export type DemoImportProfileSource = 'builtin' | 'saved';

export type DemoImportProfile = {
    id: string;
    name: string;
    description?: string;
    groupNames: string[];
    selectedProductIds?: string[];
    priceAdjustment?: DemoImportPriceAdjustmentState;
    imageMode?: DemoImportImageMode;
    overwrite?: boolean;
    source: DemoImportProfileSource;
};

export const BUILTIN_DEMO_IMPORT_PROFILES: DemoImportProfile[] = [
    {
        id: 'restaurant-standard',
        name: 'Restaurant Standard',
        description: 'Pizzen, Salate, Pasta, Getränke',
        groupNames: ['pizzas', 'salads', 'pasta', 'drinks'],
        source: 'builtin',
    },
    {
        id: 'kebap-shop',
        name: 'Kebap Shop',
        description: 'Kebap, Burger, Getränke',
        groupNames: ['kebap', 'burgers', 'drinks'],
        source: 'builtin',
    },
    {
        id: 'cafe',
        name: 'Café',
        description: 'Desserts, Getränke, Snacks',
        groupNames: ['desserts', 'drinks', 'snacks'],
        source: 'builtin',
    },
    {
        id: 'pizzeria',
        name: 'Pizzeria',
        description: 'Alle Pizzen, Salate, Getränke',
        groupNames: ['pizzas', 'salads', 'drinks'],
        source: 'builtin',
    },
];

export type AppliedDemoImportProfile = {
    groupNames: Set<string>;
    productIds: Set<string>;
    expandedGroups: string[];
    priceAdjustment?: DemoImportPriceAdjustmentState;
    imageMode?: DemoImportImageMode;
    overwrite?: boolean;
};

export function applyDemoImportProfile(
    profile: DemoImportProfile,
    categoryGroups: CategoryGroup[],
    catalogProducts: CatalogProduct[],
): AppliedDemoImportProfile {
    const validGroupNames = new Set(categoryGroups.map((g) => g.name));
    const groupNames = new Set(
        profile.groupNames.filter((name) => validGroupNames.has(name)),
    );

    let productIds: Set<string>;
    if (profile.selectedProductIds?.length) {
        const catalogIds = new Set(catalogProducts.map((p) => p.id));
        productIds = new Set(
            profile.selectedProductIds.filter((id) => catalogIds.has(id)),
        );
    } else {
        productIds = new Set<string>();
        for (const group of categoryGroups) {
            if (!groupNames.has(group.name)) continue;
            for (const id of getGroupProductIds(group, catalogProducts)) {
                productIds.add(id);
            }
        }
    }

    const expandedGroups = [...groupNames];

    return {
        groupNames,
        productIds,
        expandedGroups,
        priceAdjustment: profile.priceAdjustment,
        imageMode: profile.imageMode,
        overwrite: profile.overwrite,
    };
}

export function buildProfileFromWizardState(input: {
    name: string;
    description?: string;
    groupNames: Set<string>;
    selectedProductIds: Set<string>;
    priceAdjustment?: DemoImportPriceAdjustmentState;
    imageMode?: DemoImportImageMode;
    overwrite?: boolean;
}): DemoImportProfile {
    return {
        id: crypto.randomUUID(),
        name: input.name.trim(),
        description: input.description?.trim() || undefined,
        groupNames: [...input.groupNames],
        selectedProductIds: [...input.selectedProductIds],
        priceAdjustment: input.priceAdjustment,
        imageMode: input.imageMode,
        overwrite: input.overwrite,
        source: 'saved',
    };
}
