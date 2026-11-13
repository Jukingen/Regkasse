/**
 * Central toast / notification façade for frontend-admin.
 *
 * - React: prefer {@link useNotify} (`@/hooks/useNotify`) — uses `useAntdApp` + i18n.
 * - Plain modules: `notifySuccess` / `notifyError` / … (registered via AntdAppBridgeRegistrar).
 * Never `import { message, notification } from 'antd'`.
 */
import type { MessageInstance } from 'antd/es/message/interface';
import type { NotificationInstance, NotificationPlacement } from 'antd/es/notification/interface';
import type { ReactNode } from 'react';

import { shouldSuppressCanceledRequestToast } from '@/lib/httpCancellation';

export type NotifyPlacement = NotificationPlacement;

export type NotifyMode = 'message' | 'notification';

export type NotifyOptions = {
  /** Seconds; defaults from {@link NOTIFY_DEFAULTS}. */
  duration?: number;
  /** Notification panel placement (ignored for `mode: 'message'`). */
  placement?: NotifyPlacement;
  /** Extra body for notification panel. */
  description?: ReactNode;
  /** Action buttons / footer for notification panels. */
  btn?: ReactNode;
  /** Ant Design message/notification key (dedupe / update). */
  key?: string;
  /**
   * `message` = top toast (default for success/info/warning).
   * `notification` = corner panel (default for long error copy when description set).
   */
  mode?: NotifyMode;
};

export const NOTIFY_DEFAULTS = {
  messageDuration: 3,
  notificationDuration: 8,
  placement: 'topRight' as NotifyPlacement,
} as const;

type RegisteredApis = {
  message: MessageInstance;
  notification: NotificationInstance;
};

let apis: RegisteredApis | null = null;
const pending: Array<{
  kind: 'success' | 'error' | 'warning' | 'info';
  content: string;
  options?: NotifyOptions;
}> = [];

export function registerNotificationApis(next: RegisteredApis): void {
  apis = next;
  for (const item of pending) {
    emit(item.kind, item.content, item.options);
  }
  pending.length = 0;
}

export function unregisterNotificationApis(): void {
  apis = null;
}

function queueOrEmit(
  kind: 'success' | 'error' | 'warning' | 'info',
  content: string,
  options?: NotifyOptions
): void {
  if (!apis) {
    pending.push({ kind, content, options });
    return;
  }
  emit(kind, content, options);
}

function resolveMode(options?: NotifyOptions): NotifyMode {
  if (options?.mode) return options.mode;
  if (options?.description != null) return 'notification';
  return 'message';
}

function emit(
  kind: 'success' | 'error' | 'warning' | 'info',
  content: string,
  options?: NotifyOptions
): void {
  if (!apis) return;

  const mode = resolveMode(options);
  if (mode === 'notification') {
    const duration = options?.duration ?? NOTIFY_DEFAULTS.notificationDuration;
    const placement = options?.placement ?? NOTIFY_DEFAULTS.placement;
    apis.notification[kind]({
      message: content,
      description: options?.description,
      btn: options?.btn,
      duration,
      placement,
      key: options?.key,
    });
    return;
  }

  const duration = options?.duration ?? NOTIFY_DEFAULTS.messageDuration;
  apis.message[kind]({
    content,
    duration,
    key: options?.key,
  });
}

/** Success toast (already-localized string). */
export function notifySuccess(message: string, options?: NotifyOptions): void {
  queueOrEmit('success', message, options);
}

/** Error toast/panel (already-localized string). */
export function notifyError(message: string, options?: NotifyOptions): void {
  queueOrEmit('error', message, options);
}

/** Warning toast (already-localized string). */
export function notifyWarning(message: string, options?: NotifyOptions): void {
  queueOrEmit('warning', message, options);
}

/** Info toast (already-localized string). */
export function notifyInfo(message: string, options?: NotifyOptions): void {
  queueOrEmit('info', message, options);
}

/**
 * Plain-module error helper: ignores cancelled requests.
 * Prefer passing a string already produced via `t(...)` or `translateApiError`.
 */
export function notifyErrorSafe(message: string, error?: unknown, options?: NotifyOptions): void {
  if (error != null && shouldSuppressCanceledRequestToast(error)) {
    return;
  }
  notifyError(message, options);
}

/** @deprecated Prefer {@link notifyError}; kept for axios / query bridge callers. */
export function showAntdError(content: string): void {
  notifyError(content);
}

export const NotificationService = {
  success: notifySuccess,
  error: notifyError,
  warning: notifyWarning,
  info: notifyInfo,
  errorSafe: notifyErrorSafe,
  defaults: NOTIFY_DEFAULTS,
} as const;
