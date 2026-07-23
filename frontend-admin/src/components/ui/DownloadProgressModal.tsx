'use client';

import {
  CloseOutlined,
  CloudDownloadOutlined,
  DownloadOutlined,
  FolderOpenOutlined,
  PauseCircleOutlined,
  PlayCircleOutlined,
  ReloadOutlined,
  ShareAltOutlined,
} from '@ant-design/icons';
import { Alert, Button, Modal, Progress, Space, Typography } from 'antd';
import type { CSSProperties } from 'react';

import { useMobileDownloadClient } from '@/hooks/useMobileDownloadClient';
import { useI18n } from '@/i18n/I18nProvider';
import { formatBytes } from '@/i18n/formatting';
import {
  formatEtaLabel,
  formatSpeedLabel,
} from '@/lib/download/downloadProgressMath';
import type { ProgressiveDownloadSnapshot } from '@/lib/download/progressiveDownload';
import {
  formatMobileFileSize,
  formatMobileSpeed,
  shouldOfferBackgroundDownload,
  touchFriendlyButtonStyle,
  TOUCH_TARGET_MIN_PX,
} from '@/lib/download/mobileDownload';

export type DownloadProgressModalProps = {
  open: boolean;
  snapshot: ProgressiveDownloadSnapshot | null;
  /** When true, download continues while UI is a compact bottom bar. */
  backgroundMode?: boolean;
  onPause?: () => void;
  onResume?: () => void;
  onCancel?: () => void;
  onRetry?: () => void;
  onClose?: () => void;
  onDownloadInBackground?: () => void;
  onExpandFromBackground?: () => void;
  onShare?: () => void | Promise<void>;
  onSaveToFiles?: () => void | Promise<void>;
  showShare?: boolean;
  showSaveToFiles?: boolean;
};

/**
 * Large-file download progress: bar, bytes, ETA, speed, pause/resume, retry on network error.
 * Mobile: touch targets ≥44px, MB-first sizes, thicker bar, background + share/save actions.
 */
export function DownloadProgressModal({
  open,
  snapshot,
  backgroundMode = false,
  onPause,
  onResume,
  onCancel,
  onRetry,
  onClose,
  onDownloadInBackground,
  onExpandFromBackground,
  onShare,
  onSaveToFiles,
  showShare = false,
  showSaveToFiles = false,
}: DownloadProgressModalProps) {
  const { t, formatLocale } = useI18n();
  const isMobile = useMobileDownloadClient();
  const phase = snapshot?.phase ?? 'idle';
  const percent = snapshot?.percent ?? 0;
  const supportsPause = Boolean(snapshot?.supportsPause);
  const busy = phase === 'starting' || phase === 'downloading' || phase === 'paused';
  const terminal = phase === 'done' || phase === 'error' || phase === 'cancelled';

  const formatSize = (n: number) =>
    isMobile ? formatMobileFileSize(n, formatLocale) : formatBytes(n, formatLocale);

  const progressStatus =
    phase === 'error' || phase === 'cancelled'
      ? 'exception'
      : phase === 'done'
        ? 'success'
        : phase === 'paused'
          ? 'normal'
          : ('active' as const);

  const titleLabel = snapshot?.label?.trim() || snapshot?.fileName || '';
  const title =
    phase === 'done'
      ? t('common.downloadProgress.titleDone', { label: titleLabel })
      : phase === 'error'
        ? t('common.downloadProgress.titleError', { label: titleLabel })
        : phase === 'paused'
          ? t('common.downloadProgress.titlePaused', { label: titleLabel })
          : t('common.downloadProgress.titleDownloading', { label: titleLabel });

  const loaded = snapshot?.loadedBytes ?? 0;
  const total = snapshot?.totalBytes;
  const sizeLine =
    total != null && total > 0
      ? t('common.downloadProgress.sizeProgress', {
          loaded: formatSize(loaded),
          total: formatSize(total),
        })
      : t('common.downloadProgress.sizeLoaded', {
          loaded: formatSize(loaded),
        });

  const speed =
    snapshot && (phase === 'downloading' || phase === 'paused')
      ? isMobile
        ? formatMobileSpeed(snapshot.bytesPerSecond, formatLocale)
        : formatSpeedLabel(snapshot.bytesPerSecond, (n) => formatBytes(n, formatLocale))
      : null;

  const eta =
    snapshot && phase === 'downloading'
      ? formatEtaLabel(snapshot.etaSeconds, t)
      : phase === 'paused'
        ? t('common.downloadProgress.pausedHint')
        : null;

  const offerBackground =
    Boolean(onDownloadInBackground) &&
    busy &&
    !backgroundMode &&
    (shouldOfferBackgroundDownload(total) || (isMobile && total == null));

  const btnStyle = isMobile ? touchFriendlyButtonStyle({ width: isMobile ? '100%' : undefined }) : undefined;
  const actionBtnSize = isMobile ? ('large' as const) : undefined;

  const metaBlock = (
    <div
      style={{
        display: 'flex',
        flexDirection: isMobile ? 'column' : 'row',
        flexWrap: 'wrap',
        gap: isMobile ? 4 : 12,
        width: '100%',
        justifyContent: isMobile ? 'flex-start' : 'space-between',
      }}
    >
      <Typography.Text style={{ fontSize: isMobile ? 14 : undefined }}>{sizeLine}</Typography.Text>
      {speed ? (
        <Typography.Text type="secondary" style={{ fontSize: isMobile ? 14 : undefined }}>
          {t('common.downloadProgress.speed', { speed })}
        </Typography.Text>
      ) : null}
      {eta ? (
        <Typography.Text type="secondary" style={{ fontSize: isMobile ? 14 : undefined }}>
          {eta}
        </Typography.Text>
      ) : null}
    </div>
  );

  const progressEl = (
    <Progress
      percent={percent}
      status={progressStatus}
      strokeWidth={isMobile ? 14 : 8}
      showInfo={!isMobile || percent > 0}
      style={{ marginBottom: isMobile ? 4 : undefined }}
    />
  );

  const actionButtons = (
    <Space
      orientation={isMobile ? 'vertical' : 'horizontal'}
      wrap={!isMobile}
      style={{ width: '100%', justifyContent: isMobile ? 'stretch' : 'flex-end' }}
      size={isMobile ? 'middle' : 'small'}
    >
      {phase === 'error' && onRetry ? (
        <Button
          type="primary"
          size={actionBtnSize}
          style={btnStyle}
          icon={<ReloadOutlined />}
          onClick={onRetry}
          block={isMobile}
        >
          {t('common.downloadProgress.retry')}
        </Button>
      ) : null}

      {busy && supportsPause && phase === 'downloading' && onPause ? (
        <Button
          size={actionBtnSize}
          style={btnStyle}
          icon={<PauseCircleOutlined />}
          onClick={onPause}
          block={isMobile}
        >
          {t('common.downloadProgress.pause')}
        </Button>
      ) : null}

      {busy && supportsPause && phase === 'paused' && onResume ? (
        <Button
          type="primary"
          size={actionBtnSize}
          style={btnStyle}
          icon={<PlayCircleOutlined />}
          onClick={onResume}
          block={isMobile}
        >
          {t('common.downloadProgress.resume')}
        </Button>
      ) : null}

      {offerBackground ? (
        <Button
          size={actionBtnSize}
          style={btnStyle}
          icon={<CloudDownloadOutlined />}
          onClick={onDownloadInBackground}
          block={isMobile}
        >
          {t('common.downloadProgress.downloadInBackground')}
        </Button>
      ) : null}

      {busy && onCancel ? (
        <Button
          danger
          size={actionBtnSize}
          style={btnStyle}
          icon={<CloseOutlined />}
          onClick={onCancel}
          block={isMobile}
        >
          {t('common.downloadProgress.cancel')}
        </Button>
      ) : null}

      {phase === 'done' && showSaveToFiles && onSaveToFiles ? (
        <Button
          type="primary"
          size={actionBtnSize}
          style={btnStyle}
          icon={<FolderOpenOutlined />}
          onClick={() => void onSaveToFiles()}
          block={isMobile}
        >
          {t('common.downloadProgress.saveToFiles')}
        </Button>
      ) : null}

      {phase === 'done' && showShare && onShare ? (
        <Button
          size={actionBtnSize}
          style={btnStyle}
          icon={<ShareAltOutlined />}
          onClick={() => void onShare()}
          block={isMobile}
        >
          {t('common.downloadProgress.share')}
        </Button>
      ) : null}

      {terminal && onClose ? (
        <Button
          size={actionBtnSize}
          style={btnStyle}
          icon={<CloseOutlined />}
          onClick={onClose}
          block={isMobile}
        >
          {t('common.downloadProgress.close')}
        </Button>
      ) : null}
    </Space>
  );

  if (backgroundMode && open && busy) {
    const barStyle: CSSProperties = {
      position: 'fixed',
      left: 0,
      right: 0,
      bottom: 0,
      zIndex: 1100,
      padding: '12px 16px calc(12px + env(safe-area-inset-bottom, 0px))',
      background: 'var(--ant-color-bg-elevated, #fff)',
      boxShadow: '0 -4px 16px rgba(0,0,0,0.12)',
      borderTop: '1px solid var(--ant-color-split, #f0f0f0)',
    };

    return (
      <div role="status" aria-live="polite" style={barStyle}>
        <Space orientation="vertical" size={8} style={{ width: '100%' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, minHeight: TOUCH_TARGET_MIN_PX }}>
            <DownloadOutlined />
            <Typography.Text ellipsis style={{ flex: 1, fontWeight: 500 }}>
              {t('common.downloadProgress.backgroundTitle', { label: titleLabel })}
            </Typography.Text>
            {onExpandFromBackground ? (
              <Button
                type="link"
                size="large"
                style={touchFriendlyButtonStyle({ paddingInline: 12 })}
                onClick={onExpandFromBackground}
              >
                {t('common.downloadProgress.showProgress')}
              </Button>
            ) : null}
          </div>
          {progressEl}
          {metaBlock}
        </Space>
      </div>
    );
  }

  return (
    <Modal
      open={open && !backgroundMode}
      title={
        <Space>
          <DownloadOutlined />
          <span>{title}</span>
        </Space>
      }
      footer={null}
      closable={terminal}
      maskClosable={false}
      keyboard={terminal}
      onCancel={terminal ? onClose : undefined}
      destroyOnHidden
      width={isMobile ? '100%' : 520}
      centered={!isMobile}
      styles={
        isMobile
          ? {
              content: { margin: 8, maxWidth: 'calc(100vw - 16px)' },
              body: { paddingTop: 12 },
            }
          : undefined
      }
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        {snapshot?.fileName ? (
          <Typography.Text type="secondary" ellipsis style={{ maxWidth: '100%' }}>
            {snapshot.fileName}
          </Typography.Text>
        ) : null}

        {progressEl}
        {metaBlock}

        {phase === 'error' ? (
          <Alert
            type="error"
            showIcon
            title={
              snapshot?.errorKind === 'network'
                ? t('common.downloadProgress.networkError')
                : t('common.downloadProgress.genericError')
            }
            description={
              snapshot?.errorKind === 'network'
                ? t('common.downloadProgress.networkErrorHint')
                : snapshot?.errorMessage
            }
          />
        ) : null}

        {actionButtons}
      </Space>
    </Modal>
  );
}
