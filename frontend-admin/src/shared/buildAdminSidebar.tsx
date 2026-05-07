'use client';

/**
 * Builds Ant Design `Menu` items from `adminSidebarRegistry` + composed RKSV groups.
 * Layout stays a thin shell: call `buildAdminSidebarMenuItems` and pass to `filterSidebarMenuItems`.
 */

import React from 'react';
import type { MenuProps } from 'antd';
import * as Icons from '@ant-design/icons';
import { AdminSidebarLeafLink } from '@/components/admin-layout/AdminSidebarLeafLink';
import type { RksvMenuGroup } from '@/shared/rksvMenuModel';
import {
    SIDEBAR_DOMAIN_GROUP_META,
    SIDEBAR_LAYOUT_ROWS,
    SIDEBAR_NAV_ITEM_CATALOG,
    composeAdminSidebarData,
    type SidebarCatalogId,
    type SidebarIconToken,
    type SidebarLayoutBlock,
} from '@/shared/adminSidebarRegistry';
import { filterCatalogIdsForInventoryNav } from '@/shared/config/adminInventoryNavUi';

const ICON_MAP: Record<SidebarIconToken, React.ComponentType> = {
    ThunderboltOutlined: Icons.ThunderboltOutlined,
    ShoppingCartOutlined: Icons.ShoppingCartOutlined,
    AppstoreOutlined: Icons.AppstoreOutlined,
    UsergroupAddOutlined: Icons.UsergroupAddOutlined,
    LineChartOutlined: Icons.LineChartOutlined,
    AuditOutlined: Icons.AuditOutlined,
    ToolOutlined: Icons.ToolOutlined,
    DashboardOutlined: Icons.DashboardOutlined,
    PieChartOutlined: Icons.PieChartOutlined,
    FundOutlined: Icons.FundOutlined,
    TeamOutlined: Icons.TeamOutlined,
    FileTextOutlined: Icons.FileTextOutlined,
    FileDoneOutlined: Icons.FileDoneOutlined,
    BarChartOutlined: Icons.BarChartOutlined,
    AreaChartOutlined: Icons.AreaChartOutlined,
    ControlOutlined: Icons.ControlOutlined,
    TableOutlined: Icons.TableOutlined,
    CalendarOutlined: Icons.CalendarOutlined,
    FileSearchOutlined: Icons.FileSearchOutlined,
    CreditCardOutlined: Icons.CreditCardOutlined,
    SnippetsOutlined: Icons.SnippetsOutlined,
    EyeOutlined: Icons.EyeOutlined,
    ShoppingOutlined: Icons.ShoppingOutlined,
    FolderOutlined: Icons.FolderOutlined,
    GroupOutlined: Icons.GroupOutlined,
    TagOutlined: Icons.TagOutlined,
    InboxOutlined: Icons.InboxOutlined,
    UserOutlined: Icons.UserOutlined,
    GiftOutlined: Icons.GiftOutlined,
    SafetyCertificateOutlined: Icons.SafetyCertificateOutlined,
    SafetyOutlined: Icons.SafetyOutlined,
    SettingOutlined: Icons.SettingOutlined,
    ShopOutlined: Icons.ShopOutlined,
    CloudServerOutlined: Icons.CloudServerOutlined,
    WalletOutlined: Icons.WalletOutlined,
    ClockCircleOutlined: Icons.ClockCircleOutlined,
    CloudDownloadOutlined: Icons.CloudDownloadOutlined,
};

function iconEl(token?: SidebarIconToken): React.ReactNode {
    if (!token) return undefined;
    const C = ICON_MAP[token];
    return <C />;
}

function catalogLeaf(
    t: (key: string) => string,
    catalogId: keyof typeof SIDEBAR_NAV_ITEM_CATALOG,
): NonNullable<MenuProps['items']>[number] {
    const def = SIDEBAR_NAV_ITEM_CATALOG[catalogId];
    const text = t(def.labelKey);
    return {
        key: def.menuKey,
        icon: iconEl(def.icon),
        title: text,
        label: <AdminSidebarLeafLink href={def.href}>{text}</AdminSidebarLeafLink>,
    };
}

function buildDomainBlocks(
    t: (key: string) => string,
    blocks: SidebarLayoutBlock[],
    rksvMenuGroups: RksvMenuGroup[],
): MenuProps['items'] {
    const out: MenuProps['items'] = [];
    for (const block of blocks) {
        if (block.kind === 'leaves') {
            const leafIds = filterCatalogIdsForInventoryNav(block.catalogIds as readonly SidebarCatalogId[]);
            for (const id of leafIds) {
                out.push(catalogLeaf(t, id));
            }
            continue;
        }
        if (block.kind === 'nested') {
            const text = t(block.labelKey);
            const nestedIds = filterCatalogIdsForInventoryNav(block.catalogIds as readonly SidebarCatalogId[]);
            out.push({
                key: block.menuKey,
                icon: iconEl(block.icon),
                label: text,
                title: text,
                children: nestedIds.map((id) => catalogLeaf(t, id)),
            });
            continue;
        }
        if (block.kind === 'rksvHub') {
            const hubText = t(block.labelKey);
            const rksvSubtree = rksvMenuGroups.map((g) => ({
                key: `rksv-grp-${g.id}`,
                label: g.groupLabel,
                title: g.groupLabel,
                children: g.items.map((item) => ({
                    key: item.key,
                    title: item.label,
                    label: <AdminSidebarLeafLink href={item.href}>{item.label}</AdminSidebarLeafLink>,
                })),
            }));
            out.push({
                key: block.menuKey,
                icon: iconEl(block.icon),
                label: hubText,
                title: hubText,
                children: rksvSubtree,
            });
        }
    }
    return out;
}

export type BuildAdminSidebarMenuItemsResult = {
    menuItems: MenuProps['items'];
    rksvMenuGroups: RksvMenuGroup[];
};

/**
 * Full pipeline: compose RKSV plugin + build Ant Design menu tree from registry layout.
 */
export function buildAdminSidebarMenuItems(params: {
    t: (key: string) => string;
    verificationNavLabel: string;
}): BuildAdminSidebarMenuItemsResult {
    const composed = composeAdminSidebarData(params.t, params.verificationNavLabel);
    const { rksvMenuGroups } = composed;
    const { t } = params;

    const menuItems: MenuProps['items'] = [];

    for (const row of SIDEBAR_LAYOUT_ROWS) {
        if (row.kind === 'divider') {
            menuItems.push({ type: 'divider', key: row.key });
            continue;
        }

        const meta = SIDEBAR_DOMAIN_GROUP_META[row.domain];
        const groupLabel = t(meta.labelKey);
        menuItems.push({
            key: meta.menuKey,
            icon: iconEl(meta.icon),
            label: groupLabel,
            title: groupLabel,
            children: buildDomainBlocks(t, row.blocks, rksvMenuGroups),
        });
    }

    return { menuItems, rksvMenuGroups };
}
