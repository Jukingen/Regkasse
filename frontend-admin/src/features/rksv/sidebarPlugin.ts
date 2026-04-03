/**
 * RKSV sidebar plugin: attaches RKSV menu groups to the composed admin sidebar registry.
 * Replace `registerRksvSidebar` body to swap RKSV IA without touching core layout.
 */

import type { RksvMenuGroup } from '@/features/rksv/rksvAdminMenuModel';
import { buildRksvMenuGroups } from '@/features/rksv/rksvAdminMenuModel';

/** Target mutated by `registerRksvSidebar` (composition root lives in `adminSidebarRegistry`). */
export type RksvSidebarRegistryAttachment = {
    rksvMenuGroups: RksvMenuGroup[];
};

/**
 * Plugin hook: fills `target.rksvMenuGroups` from i18n + operator verification label.
 */
export function registerRksvSidebar(
    target: RksvSidebarRegistryAttachment,
    t: (key: string) => string,
    verificationNavLabel: string,
): void {
    target.rksvMenuGroups = buildRksvMenuGroups(t, verificationNavLabel);
}

/** Convenience for callers that only need groups (same as plugin registration). */
export function getRksvSidebarMenuGroups(
    t: (key: string) => string,
    verificationNavLabel: string,
): RksvMenuGroup[] {
    return buildRksvMenuGroups(t, verificationNavLabel);
}
