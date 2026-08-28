'use client';

import type { MenuProps } from 'antd';
import type { ReactNode } from 'react';

import { SidebarFavoriteLabel } from '@/features/menu/SidebarFavoriteStar';

export type InjectFavoriteStarsContext = {
  isFavorite: (menuKey: string) => boolean;
  onToggle: (menuKey: string) => void;
  collapsed: boolean;
};

/**
 * Wrap Ant Design menu labels with a hover star without changing menu keys or filter logic.
 */
export function injectSidebarFavoriteStars(
  items: MenuProps['items'] | undefined,
  ctx: InjectFavoriteStarsContext
): MenuProps['items'] {
  if (!items?.length || ctx.collapsed) return items;

  return items.map((it) => {
    if (!it || typeof it !== 'object') return it;
    if ('type' in it && it.type === 'divider') return it;

    const node = it as {
      key?: string | number;
      label?: unknown;
      children?: MenuProps['items'];
    };
    const nextChildren =
      node.children && node.children.length > 0
        ? injectSidebarFavoriteStars(node.children, ctx)
        : undefined;
    const menuKey = typeof node.key === 'string' ? node.key : null;
    if (!menuKey) {
      return nextChildren ? { ...it, children: nextChildren } : it;
    }

    return {
      ...it,
      ...(nextChildren ? { children: nextChildren } : {}),
      label: (
        <SidebarFavoriteLabel
          pinned={ctx.isFavorite(menuKey)}
          onToggle={() => ctx.onToggle(menuKey)}
        >
          {node.label as ReactNode}
        </SidebarFavoriteLabel>
      ),
    };
  });
}
