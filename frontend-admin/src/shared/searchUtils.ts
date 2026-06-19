import { OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';
import {
    SIDEBAR_DOMAIN_GROUP_META,
    SIDEBAR_LAYOUT_ROWS,
    SIDEBAR_NAV_ITEM_CATALOG,
    type SidebarCatalogId,
    type SidebarLayoutBlock,
    type SidebarLayoutRow,
} from '@/shared/adminSidebarRegistry';
import { FISCAL_RKSV_CLOSING_SIDEBAR_LEAVES } from '@/shared/fiscalRksvClosingSidebar';
import { isAdminInventoryNavEnabled } from '@/shared/config/adminInventoryNavUi';
import { buildRksvMenuGroups } from '@/features/rksv/rksvAdminMenuModel';
import type {
    GlobalSearchMatchRank,
    GlobalSearchResultItem,
    MenuSearchIndexSource,
    RankedGlobalSearchResult,
} from '@/components/admin-layout/GlobalSearch.types';

const RANK_SCORE: Record<GlobalSearchMatchRank, number> = {
    exact: 100,
    startsWith: 70,
    contains: 40,
};

function normalizeSearchText(value: string): string {
    return value.trim().toLocaleLowerCase();
}

function pushUnique(target: string[], value: string | undefined): void {
    const v = value?.trim();
    if (!v) return;
    if (!target.includes(v)) target.push(v);
}

function catalogBreadcrumbMap(t: (key: string) => string): Map<SidebarCatalogId, string> {
    const map = new Map<SidebarCatalogId, string>();

    const walkBlock = (domainKey: keyof typeof SIDEBAR_DOMAIN_GROUP_META, block: SidebarLayoutBlock) => {
        const domainLabel = t(SIDEBAR_DOMAIN_GROUP_META[domainKey].labelKey);

        if (block.kind === 'leaves') {
            for (const id of block.catalogIds) {
                map.set(id, domainLabel);
            }
            return;
        }

        if (block.kind === 'nested' || block.kind === 'fiscalRksvClosing') {
            const nestedLabel = t(block.labelKey);
            const trail = `${domainLabel} › ${nestedLabel}`;
            if (block.kind === 'nested') {
                for (const id of block.catalogIds) {
                    map.set(id, trail);
                }
            }
        }
    };

    const walkRow = (row: SidebarLayoutRow) => {
        if (row.kind === 'leaves') {
            for (const id of row.catalogIds) {
                if (!map.has(id)) map.set(id, '');
            }
            return;
        }
        if (row.kind === 'domain') {
            for (const block of row.blocks) {
                walkBlock(row.domain, block);
            }
        }
    };

    for (const row of SIDEBAR_LAYOUT_ROWS) {
        walkRow(row);
    }

    return map;
}

function shouldIncludeCatalogId(catalogId: SidebarCatalogId): boolean {
    if (catalogId === 'inventory' && !isAdminInventoryNavEnabled()) return false;
    return true;
}

function upsertItem(
    byMenuKey: Map<string, GlobalSearchResultItem>,
    item: GlobalSearchResultItem,
): void {
    if (!byMenuKey.has(item.menuKey)) {
        byMenuKey.set(item.menuKey, item);
    }
}

function collectSidebarGroupLabels(t: (key: string) => string): string[] {
    const labels = new Set<string>();

    for (const meta of Object.values(SIDEBAR_DOMAIN_GROUP_META)) {
        labels.add(t(meta.labelKey));
    }

    const walkBlock = (block: SidebarLayoutBlock) => {
        if (block.kind === 'nested' || block.kind === 'fiscalRksvClosing' || block.kind === 'rksvHub') {
            labels.add(t(block.labelKey));
        }
    };

    for (const row of SIDEBAR_LAYOUT_ROWS) {
        if (row.kind !== 'domain') continue;
        for (const block of row.blocks) {
            walkBlock(block);
        }
    }

    for (const group of buildRksvMenuGroups(t, OPERATOR_VERIFICATIONS_COPY.navMenuLabel)) {
        labels.add(group.groupLabel);
    }

    return Array.from(labels);
}

function augmentItemsWithGroupKeywords(
    items: GlobalSearchResultItem[],
    groupLabels: readonly string[],
): GlobalSearchResultItem[] {
    if (groupLabels.length === 0) return items;
    return items.map((item) => {
        const keywords = [...item.keywords];
        for (const groupLabel of groupLabels) {
            pushUnique(keywords, groupLabel);
        }
        return { ...item, keywords };
    });
}

/**
 * Builds the full locale-resolved search catalog from the sidebar registry (no permission filter).
 * Recomputed when `t` / locale changes; registry modules are static at runtime.
 */
export function buildMenuSearchIndexSource(t: (key: string) => string): MenuSearchIndexSource {
    const groupLabels = collectSidebarGroupLabels(t);
    return {
        items: augmentItemsWithGroupKeywords(extractGlobalSearchItems(t), groupLabels),
        groupLabels,
    };
}

/** Restricts the catalog to sidebar-visible route keys (`filterSidebarMenuItems` output). */
export function filterMenuSearchIndexByRouteKeys(
    items: readonly GlobalSearchResultItem[],
    allowedMenuKeys: ReadonlySet<string>,
): GlobalSearchResultItem[] {
    return items.filter((item) => allowedMenuKeys.has(item.menuKey));
}

/**
 * @deprecated Prefer `buildMenuSearchIndexSource` + `filterMenuSearchIndexByRouteKeys`.
 */
export function extractGlobalSearchItems(t: (key: string) => string): GlobalSearchResultItem[] {
    const breadcrumbs = catalogBreadcrumbMap(t);
    const byMenuKey = new Map<string, GlobalSearchResultItem>();

    for (const def of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
        if (!shouldIncludeCatalogId(def.id as SidebarCatalogId)) continue;
        const label = t(def.labelKey);
        const breadcrumb = breadcrumbs.get(def.id as SidebarCatalogId);
        const keywords: string[] = [];
        pushUnique(keywords, label);
        pushUnique(keywords, def.href);
        pushUnique(keywords, def.menuKey);
        pushUnique(keywords, breadcrumb);
        upsertItem(byMenuKey, {
            id: `nav:catalog:${def.id}`,
            menuKey: def.menuKey,
            href: def.href,
            label,
            breadcrumb: breadcrumb || undefined,
            keywords,
        });
    }

    for (const leaf of FISCAL_RKSV_CLOSING_SIDEBAR_LEAVES) {
        const label = t(leaf.labelKey);
        const breadcrumb = `${t(SIDEBAR_DOMAIN_GROUP_META.fiscalCompliance.labelKey)} › ${t('nav.fiscalRksvClosingHub')}`;
        const keywords: string[] = [];
        pushUnique(keywords, label);
        pushUnique(keywords, leaf.href);
        pushUnique(keywords, leaf.menuKey);
        pushUnique(keywords, breadcrumb);
        upsertItem(byMenuKey, {
            id: `nav:fiscal:${leaf.menuKey}`,
            menuKey: leaf.menuKey,
            href: leaf.href,
            label,
            breadcrumb,
            keywords,
        });
    }

    const rksvGroups = buildRksvMenuGroups(t, OPERATOR_VERIFICATIONS_COPY.navMenuLabel);
    const rksvDomain = t(SIDEBAR_DOMAIN_GROUP_META.fiscalCompliance.labelKey);
    const rksvHub = t('adminShell.group.rksv');

    for (const group of rksvGroups) {
        const breadcrumb = `${rksvDomain} › ${rksvHub} › ${group.groupLabel}`;
        for (const leaf of group.items) {
            const keywords: string[] = [];
            pushUnique(keywords, leaf.label);
            pushUnique(keywords, leaf.href);
            pushUnique(keywords, leaf.key);
            pushUnique(keywords, breadcrumb);
            pushUnique(keywords, group.groupLabel);
            upsertItem(byMenuKey, {
                id: `nav:rksv:${leaf.key}`,
                menuKey: leaf.key,
                href: leaf.href,
                label: leaf.label,
                breadcrumb,
                keywords,
            });
        }
    }

    return Array.from(byMenuKey.values()).sort((a, b) => a.label.localeCompare(b.label, 'de'));
}

function rankField(value: string, query: string): { rank: GlobalSearchMatchRank; score: number } | null {
    const haystack = normalizeSearchText(value);
    const needle = normalizeSearchText(query);
    if (!needle) return null;
    if (haystack === needle) return { rank: 'exact', score: RANK_SCORE.exact };
    if (haystack.startsWith(needle)) return { rank: 'startsWith', score: RANK_SCORE.startsWith };
    if (haystack.includes(needle)) return { rank: 'contains', score: RANK_SCORE.contains };
    return null;
}

function rankSearchItem(item: GlobalSearchResultItem, query: string): RankedGlobalSearchResult | null {
    let best: RankedGlobalSearchResult | null = null;

    const consider = (text: string | undefined, bonus = 0) => {
        if (!text) return;
        const match = rankField(text, query);
        if (!match) return;
        const candidate: RankedGlobalSearchResult = {
            item,
            rank: match.rank,
            score: match.score + bonus,
        };
        if (!best || candidate.score > best.score) {
            best = candidate;
        }
    };

    consider(item.label, 10);
    consider(item.breadcrumb, 5);
    consider(item.href, 3);
    for (const keyword of item.keywords) {
        consider(keyword, 0);
    }

    return best;
}

/**
 * Case-insensitive filter: exact label/path match > starts with > contains (label, breadcrumb, path, keywords).
 */
export function filterGlobalSearchItems(
    items: readonly GlobalSearchResultItem[],
    query: string,
    limit = 20,
): GlobalSearchResultItem[] {
    const trimmed = query.trim();
    if (!trimmed) return [];

    const ranked = items
        .map((item) => rankSearchItem(item, trimmed))
        .filter((entry): entry is RankedGlobalSearchResult => entry !== null)
        .sort((a, b) => {
            if (b.score !== a.score) return b.score - a.score;
            return a.item.label.localeCompare(b.item.label, 'de');
        });

    const seen = new Set<string>();
    const out: GlobalSearchResultItem[] = [];
    for (const { item } of ranked) {
        if (seen.has(item.menuKey)) continue;
        seen.add(item.menuKey);
        out.push(item);
        if (out.length >= limit) break;
    }
    return out;
}

export type SearchHighlightPart = { text: string; highlight: boolean };

/** Splits label text for optional match highlighting (first case-insensitive substring). */
export function splitSearchHighlight(text: string, query: string): SearchHighlightPart[] {
    const q = query.trim();
    if (!q) return [{ text, highlight: false }];

    const lower = text.toLocaleLowerCase();
    const qLower = q.toLocaleLowerCase();
    const idx = lower.indexOf(qLower);
    if (idx < 0) return [{ text, highlight: false }];

    const parts: SearchHighlightPart[] = [];
    if (idx > 0) parts.push({ text: text.slice(0, idx), highlight: false });
    parts.push({ text: text.slice(idx, idx + q.length), highlight: true });
    if (idx + q.length < text.length) {
        parts.push({ text: text.slice(idx + q.length), highlight: false });
    }
    return parts;
}
