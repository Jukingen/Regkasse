'use client';

import { useCallback, useMemo, useRef, useState } from 'react';

import type { DownloadPreviewModalProps } from '@/components/ui/DownloadPreviewModal';
import { useExportDownloadNotifications } from '@/hooks/useExportDownloadNotifications';
import { useMobileDownloadClient } from '@/hooks/useMobileDownloadClient';
import { useI18n } from '@/i18n/I18nProvider';
import { formatBytes, formatDateTime } from '@/i18n/formatting';
import { formatMobileFileSize } from '@/lib/download/mobileDownload';

export type DownloadPreviewRequest = {
  fileName: string;
  fileType: string;
  contentSummary: string;
  tenantName?: string;
  registerName?: string;
  hint?: string;
  /** Raw byte size (known or estimated). */
  sizeBytes?: number;
  isSizeEstimate?: boolean;
  /** Override preformatted size; defaults from sizeBytes via formatBytes. */
  fileSize?: string;
  createdAt?: Date | string;
  /** Override preformatted created label. */
  createdAtLabel?: string;
  execute: () => void | Promise<void>;
  onSaveToFolder?: () => void | Promise<void>;
  /** Skip preparing/success/error export notifications for this request. */
  suppressExportNotifications?: boolean;
  enableSendEmail?: boolean;
  defaultEmailTo?: string;
  emailSubject?: string;
  emailSourceKind?: string | null;
  emailSourceId?: string | null;
  resolveEmailContent?: () => Promise<Blob>;
};

type PreviewState = DownloadPreviewRequest & { open: true };

/**
 * Opens a shared download-preview dialog before running an export download.
 * Emits preparing / completed / failed notifications when prefs allow.
 */
export function useDownloadPreview() {
  const { formatLocale } = useI18n();
  const isMobile = useMobileDownloadClient();
  const exportNotify = useExportDownloadNotifications();
  const [preview, setPreview] = useState<PreviewState | null>(null);
  const [confirmLoading, setConfirmLoading] = useState(false);
  const previewRef = useRef<PreviewState | null>(null);
  previewRef.current = preview;

  const close = useCallback(() => {
    if (confirmLoading) return;
    setPreview(null);
  }, [confirmLoading]);

  const requestPreview = useCallback((request: DownloadPreviewRequest) => {
    setPreview({ ...request, open: true });
  }, []);

  const runConfirm = useCallback(async () => {
    const current = previewRef.current;
    if (!current) return;
    setConfirmLoading(true);
    const fileName = current.fileName;
    const suppress = current.suppressExportNotifications === true;
    if (!suppress) {
      exportNotify.notifyPreparing({ fileName });
    }
    try {
      await current.execute();
      if (!suppress) {
        exportNotify.notifyCompleted({
          fileName,
          onRetry: () => void runConfirm(),
          onOpenFolder: current.onSaveToFolder
            ? () => {
                void current.onSaveToFolder?.();
              }
            : undefined,
        });
      }
      setPreview(null);
    } catch {
      if (!suppress) {
        exportNotify.notifyFailed({
          fileName,
          onRetry: () => void runConfirm(),
        });
      }
    } finally {
      setConfirmLoading(false);
    }
  }, [exportNotify]);

  const runSaveToFolder = useCallback(async () => {
    const current = previewRef.current;
    if (!current?.onSaveToFolder) return;
    setConfirmLoading(true);
    const fileName = current.fileName;
    const suppress = current.suppressExportNotifications === true;
    if (!suppress) {
      exportNotify.notifyPreparing({ fileName });
    }
    try {
      await current.onSaveToFolder();
      if (!suppress) {
        exportNotify.notifyCompleted({
          fileName,
          onOpenFolder: () => {
            void current.onSaveToFolder?.();
          },
          onRetry: () => void runSaveToFolder(),
        });
      }
      setPreview(null);
    } catch {
      if (!suppress) {
        exportNotify.notifyFailed({
          fileName,
          onRetry: () => void runSaveToFolder(),
        });
      }
    } finally {
      setConfirmLoading(false);
    }
  }, [exportNotify]);

  const modalProps: DownloadPreviewModalProps = useMemo(() => {
    const createdAt =
      preview?.createdAtLabel ??
      formatDateTime(preview?.createdAt ?? new Date(), formatLocale, { second: '2-digit' });

    const rawSize = preview?.sizeBytes ?? 0;
    const formattedSize =
      preview?.fileSize ??
      (isMobile ? formatMobileFileSize(rawSize, formatLocale) : formatBytes(rawSize, formatLocale));

    return {
      open: preview != null,
      fileName: preview?.fileName ?? '',
      fileSize: formattedSize,
      fileType: preview?.fileType ?? '',
      createdAt,
      contentSummary: preview?.contentSummary ?? '',
      tenantName: preview?.tenantName,
      registerName: preview?.registerName,
      hint: preview?.hint,
      sizeBytes: preview?.sizeBytes,
      isSizeEstimate: preview?.isSizeEstimate ?? false,
      confirmLoading,
      onConfirm: () => void runConfirm(),
      onCancel: close,
      onSaveToFolder: preview?.onSaveToFolder ? () => void runSaveToFolder() : undefined,
      enableSendEmail: preview?.enableSendEmail === true,
      defaultEmailTo: preview?.defaultEmailTo,
      emailSubject: preview?.emailSubject,
      emailSourceKind: preview?.emailSourceKind,
      emailSourceId: preview?.emailSourceId,
      resolveEmailContent: preview?.resolveEmailContent,
    };
  }, [close, confirmLoading, formatLocale, isMobile, preview, runConfirm, runSaveToFolder]);

  return {
    requestPreview,
    close,
    confirmLoading,
    isOpen: preview != null,
    modalProps,
    exportNotify,
  };
}
