'use client';

import {
  CopyOutlined,
  FolderOpenOutlined,
  MailOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { Button, Space, Typography } from 'antd';
import { useCallback, useEffect, useMemo, useState } from 'react';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import {
  type DownloadNotifyPrefs,
  readDownloadNotifyPrefs,
  writeDownloadNotifyPrefs,
} from '@/lib/download/downloadNotifyPrefs';
import { playDownloadNotifySound } from '@/lib/download/downloadNotifySound';
import { NOTIFY_DEFAULTS } from '@/lib/notificationService';

export const EXPORT_DOWNLOAD_NOTIFY_KEY = 'regkasse-export-download';

const SUPPORT_MAILTO =
  'mailto:support@regkasse.at?subject=Export%20fehlgeschlagen%20%2F%20Export%20failed';

export type ExportDownloadNotifyArgs = {
  fileName: string;
  /** Override preparing body (defaults to generating hint). */
  preparingDescription?: string;
  onRetry?: () => void;
  /** Optional: re-open save picker / reveal folder flow. */
  onOpenFolder?: () => void;
};

/**
 * Preparing / success / error notifications for export downloads.
 * Honors local prefs: notifyOnExports + optional sound.
 */
export function useExportDownloadNotifications() {
  const { t } = useI18n();
  const { notification } = useAntdApp();
  const notify = useNotify();
  const [prefs, setPrefs] = useState<DownloadNotifyPrefs>(() => readDownloadNotifyPrefs());

  useEffect(() => {
    setPrefs(readDownloadNotifyPrefs());
  }, []);

  const updatePrefs = useCallback((patch: Partial<DownloadNotifyPrefs>) => {
    const next = writeDownloadNotifyPrefs(patch);
    setPrefs(next);
    return next;
  }, []);

  const closePanel = useCallback(() => {
    notification.destroy(EXPORT_DOWNLOAD_NOTIFY_KEY);
  }, [notification]);

  const notifyPreparing = useCallback(
    (args: Pick<ExportDownloadNotifyArgs, 'fileName' | 'preparingDescription'>) => {
      if (!prefs.notifyOnExports) return;
      if (prefs.playSound) playDownloadNotifySound('start');

      const description = (
        <Space orientation="vertical" size={4}>
          <Typography.Text>
            {t('common.downloadNotify.preparingBody', { fileName: args.fileName })}
          </Typography.Text>
          <Typography.Text type="secondary">
            {args.preparingDescription ?? t('common.downloadNotify.preparingHint')}
          </Typography.Text>
        </Space>
      );

      notification.info({
        key: EXPORT_DOWNLOAD_NOTIFY_KEY,
        message: t('common.downloadNotify.preparingTitle'),
        description,
        duration: 0,
        placement: NOTIFY_DEFAULTS.placement,
      });
    },
    [notification, prefs.notifyOnExports, prefs.playSound, t]
  );

  const notifyCompleted = useCallback(
    (args: ExportDownloadNotifyArgs) => {
      if (!prefs.notifyOnExports) return;
      if (prefs.playSound) playDownloadNotifySound('success');

      const handleCopy = async () => {
        try {
          await navigator.clipboard.writeText(args.fileName);
          notify.success(t('common.downloadNotify.copySuccess'));
        } catch {
          notify.error(t('common.downloadNotify.copyFailed'));
        }
      };

      const handleOpenFolder = () => {
        if (args.onOpenFolder) {
          args.onOpenFolder();
          return;
        }
        notify.info(t('common.downloadNotify.openFolderHint'), {
          mode: 'notification',
          description: t('common.downloadNotify.openFolderHintBody'),
        });
      };

      const btn = (
        <Space wrap>
          <Button size="small" icon={<FolderOpenOutlined />} onClick={handleOpenFolder}>
            {t('common.downloadNotify.openFolder')}
          </Button>
          <Button size="small" icon={<CopyOutlined />} onClick={() => void handleCopy()}>
            {t('common.downloadNotify.copy')}
          </Button>
        </Space>
      );

      notification.success({
        key: EXPORT_DOWNLOAD_NOTIFY_KEY,
        message: t('common.downloadNotify.completedTitle'),
        description: t('common.downloadNotify.completedBody', { fileName: args.fileName }),
        btn,
        duration: 12,
        placement: NOTIFY_DEFAULTS.placement,
      });
    },
    [notification, notify, prefs.notifyOnExports, prefs.playSound, t]
  );

  const notifyFailed = useCallback(
    (args: ExportDownloadNotifyArgs & { errorDetail?: string }) => {
      if (!prefs.notifyOnExports) return;
      if (prefs.playSound) playDownloadNotifySound('error');

      const btn = (
        <Space wrap>
          {args.onRetry ? (
            <Button
              size="small"
              type="primary"
              icon={<ReloadOutlined />}
              onClick={() => {
                closePanel();
                args.onRetry?.();
              }}
            >
              {t('common.downloadNotify.retry')}
            </Button>
          ) : null}
          <Button
            size="small"
            icon={<MailOutlined />}
            href={SUPPORT_MAILTO}
            target="_blank"
            rel="noopener noreferrer"
          >
            {t('common.downloadNotify.contactSupport')}
          </Button>
        </Space>
      );

      notification.error({
        key: EXPORT_DOWNLOAD_NOTIFY_KEY,
        message: t('common.downloadNotify.failedTitle'),
        description: args.errorDetail?.trim()
          ? args.errorDetail
          : t('common.downloadNotify.failedBody'),
        btn,
        duration: 0,
        placement: NOTIFY_DEFAULTS.placement,
      });
    },
    [closePanel, notification, prefs.notifyOnExports, prefs.playSound, t]
  );

  return useMemo(
    () => ({
      prefs,
      updatePrefs,
      notifyPreparing,
      notifyCompleted,
      notifyFailed,
      closePanel,
      enabled: prefs.notifyOnExports,
    }),
    [prefs, updatePrefs, notifyPreparing, notifyCompleted, notifyFailed, closePanel]
  );
}
