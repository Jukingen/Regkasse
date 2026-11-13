'use client';

/**
 * React hook for standardized toasts — uses `useAntdApp()` (theme-aware) + i18n.
 * Prefer this over raw `message.success(...)` in components.
 */
import type { Key as ReactKey } from 'react';
import { useCallback, useMemo } from 'react';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { shouldSuppressCanceledRequestToast } from '@/lib/httpCancellation';
import {
  NOTIFY_DEFAULTS,
  type NotifyOptions,
  notifyError,
  notifyInfo,
  notifySuccess,
  notifyWarning,
} from '@/lib/notificationService';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';
import {
  type UserFacingApiErrorOptions,
  getUserFacingApiErrorMessage,
} from '@/shared/errors/userFacingApiError';

type TOptions = Record<string, string | number> | undefined;
type Kind = 'success' | 'error' | 'warning' | 'info';

/** i18n keys are dotted paths without spaces; raw UI copy usually has spaces. */
function resolveContent(
  t: (key: string, options?: TOptions) => string,
  keyOrMessage: string,
  values?: TOptions
): string {
  if (keyOrMessage.includes('.') && !keyOrMessage.includes(' ')) {
    return t(keyOrMessage, values);
  }
  return keyOrMessage;
}

export function useNotify() {
  const { message, notification } = useAntdApp();
  const { t } = useI18n();

  const show = useCallback(
    (kind: Kind, content: string, options?: NotifyOptions) => {
      if (options?.mode === 'notification' || options?.description != null || options?.btn != null) {
        notification[kind]({
          message: content,
          description: options?.description,
          btn: options?.btn,
          duration: options?.duration ?? NOTIFY_DEFAULTS.notificationDuration,
          placement: options?.placement ?? NOTIFY_DEFAULTS.placement,
          key: options?.key,
        });
        return;
      }
      message[kind]({
        content,
        duration: options?.duration ?? NOTIFY_DEFAULTS.messageDuration,
        key: options?.key,
      });
    },
    [message, notification]
  );

  const success = useCallback(
    (keyOrMessage: string, options?: NotifyOptions & { values?: TOptions }) => {
      show('success', resolveContent(t, keyOrMessage, options?.values), options);
    },
    [show, t]
  );

  const error = useCallback(
    (keyOrMessage: string, options?: NotifyOptions & { values?: TOptions }) => {
      show('error', resolveContent(t, keyOrMessage, options?.values), options);
    },
    [show, t]
  );

  const warning = useCallback(
    (keyOrMessage: string, options?: NotifyOptions & { values?: TOptions }) => {
      show('warning', resolveContent(t, keyOrMessage, options?.values), options);
    },
    [show, t]
  );

  const info = useCallback(
    (keyOrMessage: string, options?: NotifyOptions & { values?: TOptions }) => {
      show('info', resolveContent(t, keyOrMessage, options?.values), options);
    },
    [show, t]
  );

  /** Strict i18n key helper — translates once (does not re-resolve). */
  const successKey = useCallback(
    (key: string, values?: TOptions, options?: NotifyOptions) => {
      show('success', t(key, values), options);
    },
    [show, t]
  );

  const errorKey = useCallback(
    (key: string, values?: TOptions, options?: NotifyOptions) => {
      show('error', t(key, values), options);
    },
    [show, t]
  );

  /** Localized API failure toast (message). */
  const apiError = useCallback(
    (err: unknown, options: UserFacingApiErrorOptions) => {
      if (shouldSuppressCanceledRequestToast(err)) return;
      openApiErrorMessage(message.open, t, err, options);
    },
    [message.open, t]
  );

  /** Localized API failure as notification panel. */
  const apiErrorNotification = useCallback(
    (err: unknown, options: UserFacingApiErrorOptions & { titleKey?: string }) => {
      if (shouldSuppressCanceledRequestToast(err)) return;
      const description = getUserFacingApiErrorMessage(t, err, options);
      notification.error({
        message: t(options.titleKey ?? 'common.errorLoadTitle'),
        description,
        duration: NOTIFY_DEFAULTS.notificationDuration,
        placement: NOTIFY_DEFAULTS.placement,
      });
    },
    [notification, t]
  );

  /** Indeterminate loading toast (pair with `destroy` or a keyed success/error). */
  const loading = useCallback(
    (keyOrMessage: string, options?: NotifyOptions & { values?: TOptions }) => {
      const content = resolveContent(t, keyOrMessage, options?.values);
      return message.loading({
        content,
        duration: options?.duration ?? 0,
        key: options?.key,
      });
    },
    [message, t]
  );

  const destroy = useCallback(
    (key?: ReactKey) => {
      if (key != null) message.destroy(key);
      else message.destroy();
    },
    [message]
  );

  return useMemo(
    () => ({
      success,
      error,
      warning,
      info,
      successKey,
      errorKey,
      apiError,
      apiErrorNotification,
      loading,
      destroy,
      /** Escape hatch when a caller already holds a translated string and is outside typical flow. */
      notifySuccess,
      notifyError,
      notifyWarning,
      notifyInfo,
      defaults: NOTIFY_DEFAULTS,
    }),
    [
      success,
      error,
      warning,
      info,
      successKey,
      errorKey,
      apiError,
      apiErrorNotification,
      loading,
      destroy,
    ]
  );
}
