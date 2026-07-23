'use client';

import { useCallback, useMemo, useRef, useState } from 'react';

import type { DownloadProgressModalProps } from '@/components/ui/DownloadProgressModal';
import { useExportDownloadNotifications } from '@/hooks/useExportDownloadNotifications';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { isNetworkDownloadError } from '@/lib/download/downloadProgressMath';
import { triggerBlobDownload } from '@/lib/download/exportDownload';
import {
  canUseWebShare,
  canUseWebShareFiles,
  hapticDownloadComplete,
  isMobileDownloadClient,
  saveBlobToFiles,
  shareDownloadBlob,
} from '@/lib/download/mobileDownload';
import {
  ProgressiveDownloadCancelledError,
  ProgressiveDownloadSession,
  type ProgressiveDownloadSnapshot,
  fetchBlobProgressive,
} from '@/lib/download/progressiveDownload';

export type ProgressiveDownloadRunOptions = {
  url: string;
  fileName: string;
  label?: string;
  expectedTotalBytes?: number | null;
  /** When set, called with the completed blob instead of auto-triggering a browser download. */
  onBlob?: (blob: Blob, fileName: string) => void | Promise<void>;
  /** Optional transform / validation after fetch (e.g. backup JSON error envelopes). */
  validateBlob?: (blob: Blob, headers: Record<string, unknown>) => Promise<Blob>;
  /** Skip export preparing/success/error notifications. */
  suppressExportNotifications?: boolean;
};

export type ProgressiveDownloadCustomOptions = {
  fileName: string;
  label?: string;
  expectedTotalBytes?: number | null;
  execute: (ctx: {
    session: ProgressiveDownloadSession;
    onProgress: (snapshot: ProgressiveDownloadSnapshot) => void;
  }) => Promise<void>;
  suppressExportNotifications?: boolean;
};

/**
 * Opens DownloadProgressModal and runs a Range-capable progressive download.
 */
export function useProgressiveDownload() {
  const { t } = useI18n();
  const notify = useNotify();
  const exportNotify = useExportDownloadNotifications();
  const [snapshot, setSnapshot] = useState<ProgressiveDownloadSnapshot | null>(null);
  const [backgroundMode, setBackgroundMode] = useState(false);
  const [completedBlob, setCompletedBlob] = useState<Blob | null>(null);
  const sessionRef = useRef<ProgressiveDownloadSession | null>(null);
  const lastUrlRunRef = useRef<ProgressiveDownloadRunOptions | null>(null);
  const lastCustomRunRef = useRef<ProgressiveDownloadCustomOptions | null>(null);
  const completedBlobRef = useRef<Blob | null>(null);
  const completedFileNameRef = useRef<string>('');
  const runningRef = useRef(false);
  const runRef = useRef<(options: ProgressiveDownloadRunOptions) => Promise<void>>(async () => undefined);
  const runCustomRef = useRef<(options: ProgressiveDownloadCustomOptions) => Promise<void>>(
    async () => undefined
  );

  const close = useCallback(() => {
    const phase = snapshot?.phase;
    if (phase === 'starting' || phase === 'downloading' || phase === 'paused') return;
    sessionRef.current = null;
    lastUrlRunRef.current = null;
    lastCustomRunRef.current = null;
    completedBlobRef.current = null;
    completedFileNameRef.current = '';
    setCompletedBlob(null);
    setBackgroundMode(false);
    setSnapshot(null);
  }, [snapshot?.phase]);

  const pause = useCallback(() => {
    sessionRef.current?.pause();
    setSnapshot((prev) => (prev ? { ...prev, phase: 'paused' } : prev));
  }, []);

  const resume = useCallback(() => {
    sessionRef.current?.resume();
    setSnapshot((prev) => (prev ? { ...prev, phase: 'downloading' } : prev));
  }, []);

  const cancel = useCallback(() => {
    sessionRef.current?.cancel();
  }, []);

  const downloadInBackground = useCallback(() => {
    setBackgroundMode(true);
  }, []);

  const expandFromBackground = useCallback(() => {
    setBackgroundMode(false);
  }, []);

  const markDone = useCallback((fileName: string, label: string | undefined, size: number, blob?: Blob) => {
    if (blob) {
      completedBlobRef.current = blob;
      completedFileNameRef.current = fileName;
      setCompletedBlob(blob);
    }
    setBackgroundMode(false);
    hapticDownloadComplete();
    setSnapshot({
      phase: 'done',
      fileName,
      label,
      loadedBytes: size,
      totalBytes: size,
      percent: 100,
      bytesPerSecond: 0,
      etaSeconds: 0,
      supportsPause: false,
    });
  }, []);

  const handleShare = useCallback(async () => {
    const blob = completedBlobRef.current;
    const name = completedFileNameRef.current;
    if (!blob || !name) return;
    try {
      const result = await shareDownloadBlob(blob, name, {
        title: name,
        text: t('common.downloadProgress.shareText', { fileName: name }),
      });
      if (result === 'unsupported') {
        notify.info(t('common.downloadProgress.shareUnsupported'));
      }
    } catch {
      notify.error(t('common.downloadProgress.shareFailed'));
    }
  }, [notify, t]);

  const handleSaveToFiles = useCallback(async () => {
    const blob = completedBlobRef.current;
    const name = completedFileNameRef.current;
    if (!blob || !name) return;
    try {
      const result = await saveBlobToFiles(blob, name, triggerBlobDownload);
      if (result === 'cancelled') return;
      if (result === 'shared' || result === 'downloaded' || result === 'saved') {
        notify.success(t('common.downloadProgress.saveToFilesSuccess'));
      }
    } catch {
      notify.error(t('common.downloadProgress.saveToFilesFailed'));
    }
  }, [notify, t]);

  const executeUrl = useCallback(
    async (options: ProgressiveDownloadRunOptions) => {
      if (runningRef.current) return;
      runningRef.current = true;
      lastUrlRunRef.current = options;
      lastCustomRunRef.current = null;
      completedBlobRef.current = null;
      completedFileNameRef.current = '';
      setCompletedBlob(null);
      setBackgroundMode(false);
      const session = new ProgressiveDownloadSession();
      sessionRef.current = session;
      const suppress = options.suppressExportNotifications === true;

      try {
        if (!suppress) exportNotify.notifyPreparing({ fileName: options.fileName });
        const result = await fetchBlobProgressive({
          url: options.url,
          fileName: options.fileName,
          label: options.label,
          expectedTotalBytes: options.expectedTotalBytes,
          session,
          onProgress: setSnapshot,
        });

        let blob = result.blob;
        if (options.validateBlob) {
          blob = await options.validateBlob(blob, result.headers);
        }

        if (options.onBlob) {
          await options.onBlob(blob, result.fileName);
        } else {
          triggerBlobDownload(blob, result.fileName);
        }

        markDone(result.fileName, options.label, blob.size, blob);
        if (!suppress) {
          exportNotify.notifyCompleted({
            fileName: result.fileName,
            onRetry: () => {
              void runRef.current(options);
            },
          });
        }
      } catch (err) {
        setBackgroundMode(false);
        if (err instanceof ProgressiveDownloadCancelledError) {
          setSnapshot((prev) =>
            prev
              ? { ...prev, phase: 'cancelled', errorKind: 'cancelled' }
              : {
                  phase: 'cancelled',
                  fileName: options.fileName,
                  label: options.label,
                  loadedBytes: 0,
                  totalBytes: options.expectedTotalBytes ?? null,
                  percent: 0,
                  bytesPerSecond: 0,
                  etaSeconds: null,
                  supportsPause: false,
                  errorKind: 'cancelled',
                }
          );
          if (!suppress) exportNotify.closePanel();
          return;
        }
        if (!suppress) {
          exportNotify.notifyFailed({
            fileName: options.fileName,
            errorDetail: isNetworkDownloadError(err)
              ? undefined
              : err instanceof Error
                ? err.message
                : undefined,
            onRetry: () => {
              void runRef.current(options);
            },
          });
        }
        throw err;
      } finally {
        runningRef.current = false;
      }
    },
    [exportNotify, markDone]
  );

  const executeCustom = useCallback(
    async (options: ProgressiveDownloadCustomOptions) => {
      if (runningRef.current) return;
      runningRef.current = true;
      lastCustomRunRef.current = options;
      lastUrlRunRef.current = null;
      completedBlobRef.current = null;
      completedFileNameRef.current = '';
      setCompletedBlob(null);
      setBackgroundMode(false);
      const session = new ProgressiveDownloadSession();
      sessionRef.current = session;
      const suppress = options.suppressExportNotifications === true;

      setSnapshot({
        phase: 'starting',
        fileName: options.fileName,
        label: options.label,
        loadedBytes: 0,
        totalBytes: options.expectedTotalBytes ?? null,
        percent: 0,
        bytesPerSecond: 0,
        etaSeconds: null,
        supportsPause: false,
      });

      try {
        if (!suppress) exportNotify.notifyPreparing({ fileName: options.fileName });
        await options.execute({ session, onProgress: setSnapshot });
        hapticDownloadComplete();
        setBackgroundMode(false);
        setSnapshot((prev) =>
          prev
            ? {
                ...prev,
                phase: 'done',
                percent: 100,
                etaSeconds: 0,
                bytesPerSecond: 0,
              }
            : {
                phase: 'done',
                fileName: options.fileName,
                label: options.label,
                loadedBytes: options.expectedTotalBytes ?? 0,
                totalBytes: options.expectedTotalBytes ?? null,
                percent: 100,
                bytesPerSecond: 0,
                etaSeconds: 0,
                supportsPause: false,
              }
        );
        if (!suppress) {
          exportNotify.notifyCompleted({
            fileName: options.fileName,
            onRetry: () => {
              void runCustomRef.current(options);
            },
          });
        }
      } catch (err) {
        setBackgroundMode(false);
        if (err instanceof ProgressiveDownloadCancelledError) {
          setSnapshot((prev) =>
            prev
              ? { ...prev, phase: 'cancelled', errorKind: 'cancelled' }
              : {
                  phase: 'cancelled',
                  fileName: options.fileName,
                  label: options.label,
                  loadedBytes: 0,
                  totalBytes: options.expectedTotalBytes ?? null,
                  percent: 0,
                  bytesPerSecond: 0,
                  etaSeconds: null,
                  supportsPause: false,
                  errorKind: 'cancelled',
                }
          );
          if (!suppress) exportNotify.closePanel();
          return;
        }
        if (
          err &&
          typeof err === 'object' &&
          'code' in err &&
          (err as { code?: string }).code === 'cancelled'
        ) {
          setSnapshot((prev) =>
            prev ? { ...prev, phase: 'cancelled', errorKind: 'cancelled' } : prev
          );
          if (!suppress) exportNotify.closePanel();
          return;
        }
        setSnapshot((prev) => {
          if (prev?.phase === 'error') return prev;
          return prev
            ? {
                ...prev,
                phase: 'error',
                errorKind: 'unknown',
                errorMessage: err instanceof Error ? err.message : String(err),
              }
            : prev;
        });
        if (!suppress) {
          exportNotify.notifyFailed({
            fileName: options.fileName,
            errorDetail: isNetworkDownloadError(err)
              ? undefined
              : err instanceof Error
                ? err.message
                : undefined,
            onRetry: () => {
              void runCustomRef.current(options);
            },
          });
        }
        throw err;
      } finally {
        runningRef.current = false;
      }
    },
    [exportNotify]
  );

  const run = useCallback(
    async (options: ProgressiveDownloadRunOptions) => {
      try {
        await executeUrl(options);
      } catch {
        // Error UI via snapshot + export notify.
      }
    },
    [executeUrl]
  );

  const runCustom = useCallback(
    async (options: ProgressiveDownloadCustomOptions) => {
      try {
        await executeCustom(options);
      } catch {
        // Error UI via snapshot + export notify.
      }
    },
    [executeCustom]
  );

  runRef.current = run;
  runCustomRef.current = runCustom;

  const retry = useCallback(() => {
    if (lastUrlRunRef.current) {
      void run(lastUrlRunRef.current);
      return;
    }
    if (lastCustomRunRef.current) {
      void runCustom(lastCustomRunRef.current);
    }
  }, [run, runCustom]);

  const mobileActions = isMobileDownloadClient();
  const canShare = mobileActions && Boolean(completedBlob) && canUseWebShare();
  const canSaveFiles =
    mobileActions && Boolean(completedBlob) && (canUseWebShareFiles() || canUseWebShare());

  const modalProps: DownloadProgressModalProps = useMemo(
    () => ({
      open: snapshot != null,
      snapshot,
      backgroundMode,
      onPause: pause,
      onResume: resume,
      onCancel: cancel,
      onRetry: retry,
      onClose: close,
      onDownloadInBackground: downloadInBackground,
      onExpandFromBackground: expandFromBackground,
      onShare: canShare ? handleShare : undefined,
      onSaveToFiles: canSaveFiles ? handleSaveToFiles : undefined,
      showShare: canShare,
      showSaveToFiles: canSaveFiles,
    }),
    [
      snapshot,
      backgroundMode,
      pause,
      resume,
      cancel,
      retry,
      close,
      downloadInBackground,
      expandFromBackground,
      handleShare,
      handleSaveToFiles,
      canShare,
      canSaveFiles,
    ]
  );

  return {
    snapshot,
    open: snapshot != null,
    backgroundMode,
    run,
    runCustom,
    pause,
    resume,
    cancel,
    retry,
    close,
    downloadInBackground,
    expandFromBackground,
    modalProps,
    isBusy:
      snapshot?.phase === 'starting' ||
      snapshot?.phase === 'downloading' ||
      snapshot?.phase === 'paused',
  };
}
