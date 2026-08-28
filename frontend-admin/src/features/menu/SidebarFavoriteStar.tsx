'use client';

import { StarFilled, StarOutlined } from '@ant-design/icons';
import { Button } from 'antd';
import type { MouseEvent, ReactNode } from 'react';

import sidebarStyles from '@/app/(protected)/protected-layout-sidebar.module.css';
import { useI18n } from '@/i18n';

export type SidebarFavoriteStarProps = {
  pinned: boolean;
  onToggle: () => void;
  /** When true, hide the control (collapsed sider). */
  hidden?: boolean;
};

/**
 * Pin/unpin control. Visible on row hover, or always when already favorited.
 */
export function SidebarFavoriteStar({ pinned, onToggle, hidden }: SidebarFavoriteStarProps) {
  const { t } = useI18n();

  const stop = (event: MouseEvent) => {
    event.preventDefault();
    event.stopPropagation();
  };

  if (hidden) return null;

  return (
    <Button
      type="text"
      size="small"
      className={`${sidebarStyles.favoriteStar} ${pinned ? sidebarStyles.favoriteStarPinned : ''}`}
      icon={
        pinned ? (
          <StarFilled style={{ color: '#faad14' }} />
        ) : (
          <StarOutlined />
        )
      }
      aria-label={
        pinned ? t('adminShell.sidebar.favorites.unpin') : t('adminShell.sidebar.favorites.pin')
      }
      aria-pressed={pinned}
      onMouseDown={stop}
      onClick={(event) => {
        stop(event);
        onToggle();
      }}
    />
  );
}

export function SidebarFavoriteLabel({
  pinned,
  onToggle,
  hideStar,
  children,
}: {
  pinned: boolean;
  onToggle: () => void;
  hideStar?: boolean;
  children: ReactNode;
}) {
  return (
    <span className={sidebarStyles.favoriteLabelRow}>
      <span className={sidebarStyles.favoriteLabelMain}>{children}</span>
      <SidebarFavoriteStar pinned={pinned} onToggle={onToggle} hidden={hideStar} />
    </span>
  );
}
