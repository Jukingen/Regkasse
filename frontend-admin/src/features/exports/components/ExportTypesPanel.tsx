'use client';

import {
  DownloadOutlined,
  HolderOutlined,
  StarFilled,
  StarOutlined,
} from '@ant-design/icons';
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
import { Button, Card, Typography } from 'antd';
import Link from 'next/link';
import type { CSSProperties } from 'react';

import { useExportFavorites } from '@/features/exports/useExportFavorites';
import type { ExportTypeDef, ExportTypeId } from '@/features/exports/exportTypeCatalog';
import { useI18n } from '@/i18n/I18nProvider';

function SortableFavoriteRow({
  item,
  label,
  onUnstar,
}: {
  item: ExportTypeDef;
  label: string;
  onUnstar: () => void;
}) {
  const { t } = useI18n();
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: item.id,
  });

  const style: CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.85 : 1,
    display: 'flex',
    alignItems: 'center',
    gap: 8,
    padding: '8px 0',
    borderBottom: '1px solid var(--ant-color-split, #f0f0f0)',
  };

  return (
    <div ref={setNodeRef} style={style}>
      <Button
        type="text"
        size="small"
        icon={<HolderOutlined />}
        aria-label={t('common.exportFavorites.reorderHandle')}
        style={{ cursor: 'grab' }}
        {...attributes}
        {...listeners}
      />
      <Button
        type="text"
        size="small"
        icon={<StarFilled style={{ color: '#faad14' }} />}
        aria-label={t('common.exportFavorites.unstar')}
        onClick={onUnstar}
      />
      <Typography.Text style={{ flex: 1 }}>{label}</Typography.Text>
      <Link href={item.href}>
        <Button type="primary" size="small" icon={<DownloadOutlined />}>
          {t('common.exportFavorites.open')}
        </Button>
      </Link>
    </div>
  );
}

function CatalogRow({
  item,
  label,
  starred,
  onToggle,
}: {
  item: ExportTypeDef;
  label: string;
  starred: boolean;
  onToggle: () => void;
}) {
  const { t } = useI18n();
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '8px 0',
        borderBottom: '1px solid var(--ant-color-split, #f0f0f0)',
      }}
    >
      <Button
        type="text"
        size="small"
        icon={
          starred ? (
            <StarFilled style={{ color: '#faad14' }} />
          ) : (
            <StarOutlined />
          )
        }
        aria-label={starred ? t('common.exportFavorites.unstar') : t('common.exportFavorites.star')}
        onClick={onToggle}
      />
      <Typography.Text style={{ flex: 1, paddingLeft: starred ? 0 : 0 }}>
        {starred ? `⭐ ${label}` : label}
      </Typography.Text>
      <Link href={item.href}>
        <Button size="small" icon={<DownloadOutlined />}>
          {t('common.exportFavorites.open')}
        </Button>
      </Link>
    </div>
  );
}

/**
 * Export types list with star favorites, download shortcuts, and drag-reorder of favorites.
 */
export function ExportTypesPanel() {
  const { t } = useI18n();
  const { hydrated, visibleCatalog, favorites, isFavorite, toggleFavorite, reorderFavorites } =
    useExportFavorites();

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  const onDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    if (!over) return;
    reorderFavorites(String(active.id), String(over.id));
  };

  if (!hydrated) return null;
  if (visibleCatalog.length === 0) return null;

  const nonFavoriteCatalog = visibleCatalog.filter((e) => !isFavorite(e.id));

  return (
    <Card title={t('common.exportFavorites.typesTitle')}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t('common.exportFavorites.typesHint')}
      </Typography.Paragraph>

      {favorites.length > 0 ? (
        <>
          <Typography.Text strong style={{ display: 'block', marginBottom: 4 }}>
            {t('common.exportFavorites.favoritesSection')}
          </Typography.Text>
          <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
            <SortableContext
              items={favorites.map((f) => f.id)}
              strategy={verticalListSortingStrategy}
            >
              {favorites.map((item) => (
                <SortableFavoriteRow
                  key={item.id}
                  item={item}
                  label={t(item.labelKey)}
                  onUnstar={() => toggleFavorite(item.id as ExportTypeId)}
                />
              ))}
            </SortableContext>
          </DndContext>
        </>
      ) : null}

      {nonFavoriteCatalog.length > 0 ? (
        <>
          <Typography.Text
            strong
            style={{ display: 'block', marginTop: favorites.length ? 16 : 0, marginBottom: 4 }}
          >
            {t('common.exportFavorites.allSection')}
          </Typography.Text>
          {nonFavoriteCatalog.map((item) => (
            <CatalogRow
              key={item.id}
              item={item}
              label={t(item.labelKey)}
              starred={false}
              onToggle={() => toggleFavorite(item.id)}
            />
          ))}
        </>
      ) : null}
    </Card>
  );
}
