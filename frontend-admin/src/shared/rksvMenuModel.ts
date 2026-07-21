/**
 * Re-export RKSV admin menu model from the RKSV feature module.
 * Import from here for backward compatibility, or from `@/features/rksv/rksvAdminMenuModel` directly.
 */

export {
  buildRksvMenuGroups,
  collectRksvMenuLeafKeys,
  getRksvOpenSubgroupKeys,
  rksvGroupSubMenuKey,
  type RksvMenuGroup,
  type RksvMenuLeaf,
} from '@/features/rksv/rksvAdminMenuModel';
