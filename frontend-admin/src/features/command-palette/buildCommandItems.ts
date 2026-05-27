import type { AppRouterInstance } from 'next/dist/shared/lib/app-router-context.shared-runtime';
import { OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';
import {
    SIDEBAR_NAV_ITEM_CATALOG,
    type SidebarNavCatalogItem,
} from '@/shared/adminSidebarRegistry';
import { FISCAL_RKSV_CLOSING_SIDEBAR_LEAVES } from '@/shared/fiscalRksvClosingSidebar';
import { buildRksvMenuGroups } from '@/shared/rksvMenuModel';
import { buildDefaultCommands, type DefaultCommandsContext } from '@/features/command-palette/defaultCommands';
import type { CommandItem } from '@/features/command-palette/types';

function catalogPageItem(
    ctx: DefaultCommandsContext,
    def: SidebarNavCatalogItem,
): CommandItem {
    const label = ctx.t(def.labelKey);
    return {
        id: `page:catalog:${def.id}`,
        type: 'page',
        label,
        description: def.href,
        group: 'Navigation',
        keywords: [def.id, def.menuKey, def.href, label],
        menuKey: def.menuKey,
        action: () => {
            ctx.closePalette();
            ctx.router.push(def.href);
        },
    };
}

function dedupeById(items: CommandItem[]): CommandItem[] {
    const seen = new Set<string>();
    const out: CommandItem[] = [];
    for (const item of items) {
        if (seen.has(item.id)) continue;
        seen.add(item.id);
        out.push(item);
    }
    return out;
}

/** Default shortcuts + full sidebar route catalog (permissions applied separately). */
export function buildCommandItems(
    t: (key: string) => string,
    router: AppRouterInstance,
    closePalette: () => void,
    triggerBackup: () => void,
): CommandItem[] {
    const ctx: DefaultCommandsContext = { t, router, closePalette, triggerBackup };
    const pinned = buildDefaultCommands(ctx);
    const pinnedMenuKeys = new Set(pinned.map((p) => p.menuKey).filter(Boolean));

    const extended: CommandItem[] = [];

    for (const def of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
        if (pinnedMenuKeys.has(def.menuKey)) continue;
        extended.push(catalogPageItem(ctx, def));
    }

    for (const leaf of FISCAL_RKSV_CLOSING_SIDEBAR_LEAVES) {
        if (pinnedMenuKeys.has(leaf.menuKey)) continue;
        const label = t(leaf.labelKey);
        extended.push({
            id: `page:fiscal:${leaf.menuKey}`,
            type: 'page',
            label,
            description: leaf.href,
            group: 'Navigation',
            keywords: [leaf.menuKey, leaf.href, label],
            menuKey: leaf.menuKey,
            action: () => {
                ctx.closePalette();
                ctx.router.push(leaf.href);
            },
        });
    }

    const rksvGroups = buildRksvMenuGroups(t, OPERATOR_VERIFICATIONS_COPY.navMenuLabel);
    for (const group of rksvGroups) {
        for (const leaf of group.items) {
            if (pinnedMenuKeys.has(leaf.key)) continue;
            extended.push({
                id: `page:rksv:${leaf.key}`,
                type: 'page',
                label: leaf.label,
                description: leaf.href,
                group: 'Navigation',
                keywords: [leaf.key, leaf.href, leaf.label, group.groupLabel],
                menuKey: leaf.key,
                action: () => {
                    ctx.closePalette();
                    ctx.router.push(leaf.href);
                },
            });
        }
    }

    return dedupeById([...pinned, ...extended]);
}
