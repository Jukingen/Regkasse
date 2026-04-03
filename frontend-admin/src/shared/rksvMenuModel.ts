/**
 * Re-export RKSV admin menu model from the RKSV feature module.
 * Import from here for backward compatibility, or from `@/features/rksv/rksvAdminMenuModel` directly.
 */

export {
    type RksvMenuLeaf,
    type RksvMenuGroup,
    rksvGroupSubMenuKey,
    getRksvOpenSubgroupKeys,
    collectRksvMenuLeafKeys,
    buildRksvMenuGroups,
} from '@/features/rksv/rksvAdminMenuModel';
