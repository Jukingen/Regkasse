import type { ActivityDto } from '@/api/manual/activityEvents';
import {
  isCatalogActivityType,
  isPermissionActivityType,
} from '@/features/activity-notifications/activityTypes';
import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';
import { technicalConsole } from '@/shared/dev/technicalConsole';

type Translate = (key: string, params?: Record<string, string | number>) => string;

const UNKNOWN_EVENT_TITLE_KEY = 'activity.events.unknown.title';

function metaString(metadata: Record<string, unknown> | null | undefined, key: string): string | null {
  const value = metadata?.[key];
  if (typeof value === 'string' && value.trim()) return value.trim();
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return null;
}

function isMissingTranslation(key: string, translated: string): boolean {
  return !translated || translated === key || translated === USER_FACING_MISSING_TRANSLATION_LABEL;
}

function translateCatalogTitle(type: string, t: Translate): string {
  const key = `activity.events.${type}.title`;
  const translated = t(key);
  if (!isMissingTranslation(key, translated)) return translated;
  technicalConsole.warn('Activity feed translation key is missing', { key });
  return t(UNKNOWN_EVENT_TITLE_KEY);
}

/**
 * Permission events use actor/role titles. Catalog events use activity.events titles.
 * A missing catalog key logs a warning and falls back to the generic unknown-event title.
 */
export function formatActivityTitle(activity: ActivityDto, t: Translate): string {
  if (isPermissionActivityType(activity.type)) {
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

  if (isCatalogActivityType(activity.type)) return translateCatalogTitle(activity.type, t);
  return activity.title;
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

export function formatActivityDescription(activity: ActivityDto, t: Translate): string | null {
  if (!isCatalogActivityType(activity.type)) return formatActivityWhatChanged(activity);
  const key = `activity.events.${activity.type}.description`;
  const translated = t(key);
  if (isMissingTranslation(key, translated)) return formatActivityWhatChanged(activity);
  return translated;
}
