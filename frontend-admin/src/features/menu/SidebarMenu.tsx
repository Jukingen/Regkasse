'use client';

/**
 * Favorites strip at the top of the FA sidebar (Sık Kullanılanlar).
 * Wired from `AdminSidebarMenuPanel`; does not replace the registry menu tree.
 */

import { HolderOutlined, StarFilled } from '@ant-design/icons';
import {
  DndContext,
  type DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { Button } from 'antd';
import Link from 'next/link';
import { useMemo, type CSSProperties, type ReactNode } from 'react';

import sidebarStyles from '@/app/(protected)/protected-layout-sidebar.module.css';
import { resolveSidebarFavorite } from '@/features/menu/resolveSidebarFavorite';
import { useFavorites } from '@/features/menu/useFavorites';
import { useI18n } from '@/i18n';
import { resolveSidebarIconElement } from '@/shared/buildAdminSidebar';

export type SidebarMenuProps = {
  visibleMenuKeys: ReadonlySet<string>;
  collapsed: boolean;
  onNavigate?: () => void;
};

function SortableFavoriteItem({
  menuKey,
  href,
  label,
  icon,
  onRemove,
  onNavigate,
}: {
  menuKey: string;
  href: string;
  label: string;
  icon?: ReactNode;
  onRemove: () => void;
  onNavigate?: () => void;
}) {
  const { t } = useI18n();
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: menuKey,
  });

  const style: CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.85 : 1,
  };

  return (
    <li ref={setNodeRef} style={style} className={sidebarStyles.favoriteItem}>
      <Button
        type="text"
        size="small"
        className={sidebarStyles.favoriteDragHandle}
        icon={<HolderOutlined />}
        aria-label={t('adminShell.sidebar.favorites.reorderHandle')}
        {...attributes}
        {...listeners}
      />
      <Link href={href} className={sidebarStyles.favoriteLink} onClick={() => onNavigate?.()}>
        <span className={sidebarStyles.favoriteItemIcon} aria-hidden>
          {icon}
        </span>
        <span className={sidebarStyles.favoriteItemLabel}>{label}</span>
      </Link>
      <Button
        type="text"
        size="small"
        className={sidebarStyles.favoriteUnpin}
        icon={<StarFilled style={{ color: '#faad14' }} />}
        aria-label={t('adminShell.sidebar.favorites.unpin')}
        onClick={onRemove}
      />
      <Button
        type="text"
        size="small"
        className={sidebarStyles.favoriteRemove}
        onClick={onRemove}
      >
        {t('adminShell.sidebar.favorites.remove')}
      </Button>
    </li>
  );
}

/**
 * Favorites section rendered above the main sidebar menu.
 */
export function SidebarMenu({ visibleMenuKeys, collapsed, onNavigate }: SidebarMenuProps) {
  const { t } = useI18n();
  const { favorites, hydrated, removeFavorite, reorderFavorites } = useFavorites({
    visibleMenuKeys,
  });

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  const resolved = useMemo(
    () =>
      favorites
        .map((key) => resolveSidebarFavorite(key, visibleMenuKeys))
        .filter((item): item is NonNullable<typeof item> => item != null),
    [favorites, visibleMenuKeys]
  );

  const onDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    if (!over) return;
    reorderFavorites(String(active.id), String(over.id));
  };

  if (collapsed || !hydrated) return null;

  return (
    <nav className={sidebarStyles.favorites} aria-label={t('adminShell.sidebar.favorites.title')}>
      <div className={sidebarStyles.favoritesTitle}>
        <StarFilled aria-hidden style={{ color: '#faad14' }} />
        <span>{t('adminShell.sidebar.favorites.title')}</span>
      </div>
      {resolved.length === 0 ? (
        <p className={sidebarStyles.favoritesEmpty}>{t('adminShell.sidebar.favorites.empty')}</p>
      ) : (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
          <SortableContext items={resolved.map((item) => item.menuKey)} strategy={verticalListSortingStrategy}>
            <ul>
              {resolved.map((item) => (
                <SortableFavoriteItem
                  key={item.menuKey}
                  menuKey={item.menuKey}
                  href={item.href}
                  label={item.labelKey.includes('.') ? t(item.labelKey) : item.labelKey}
                  icon={resolveSidebarIconElement(item.icon)}
                  onRemove={() => removeFavorite(item.menuKey)}
                  onNavigate={onNavigate}
                />
              ))}
            </ul>
          </SortableContext>
        </DndContext>
      )}
    </nav>
  );
}
