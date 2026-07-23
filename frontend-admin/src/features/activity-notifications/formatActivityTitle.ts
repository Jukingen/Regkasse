import type { ActivityDto } from '@/api/manual/activityEvents';
import { isPermissionActivityType } from '@/features/activity-notifications/activityTypes';

type Translate = (key: string, params?: Record<string, string | number>) => string;

function metaString(metadata: Record<string, unknown> | null | undefined, key: string): string | null {
  const value = metadata?.[key];
  if (typeof value === 'string' && value.trim()) return value.trim();
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return null;
}

/**
 * Prefer localized permission titles (actor + role/permission) when metadata is present;
 * otherwise fall back to the server title.
 */
export function formatActivityTitle(activity: ActivityDto, t: Translate): string {
  if (!isPermissionActivityType(activity.type)) {
    return activity.title;
  }

  const actor =
    metaString(activity.metadata, 'ActorName') ||
    metaString(activity.metadata, 'ActorEmail') ||
    activity.actorName ||
    t('activityNotifications.permissionTitles.defaultActor');
  const role = metaString(activity.metadata, 'RoleName') || activity.entityId || '';
  const permission = metaString(activity.metadata, 'PermissionKey') || '';

  return t(`activityNotifications.permissionTitles.${activity.type}`, {
    actor,
    role,
    permission,
  });
}

export function formatActivityWhatChanged(activity: ActivityDto): string | null {
  if (!isPermissionActivityType(activity.type)) {
    return activity.description?.trim() || null;
  }
  const fromMeta = metaString(activity.metadata, 'WhatChanged');
  if (fromMeta) return fromMeta;
  if (activity.description?.trim()) return activity.description.trim();
  return null;
}
