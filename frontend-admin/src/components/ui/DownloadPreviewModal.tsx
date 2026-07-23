'use client';

import {
  CloseOutlined,
  CopyOutlined,
  DownloadOutlined,
  EyeOutlined,
  FolderOpenOutlined,
  MailOutlined,
  ShareAltOutlined,
} from '@ant-design/icons';
import { Alert, Button, Card, Modal, Space, Typography } from 'antd';
import { useEffect, useState, type ReactNode } from 'react';

import {
  FilePreviewModal,
  type FilePreviewSource,
} from '@/components/ui/FilePreviewModal';
import { useKeyboardShortcutLabels } from '@/components/KeyboardShortcutsProvider';
import { SendExportEmailModal } from '@/features/export-email/components/SendExportEmailModal';
import { useKeyboardShortcutListener } from '@/hooks/useKeyboardShortcutListener';
import { useMobileDownloadClient } from '@/hooks/useMobileDownloadClient';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { isLargeExport } from '@/lib/download/downloadPreview';
import { canSaveBlobToFolder, triggerBlobDownload } from '@/lib/download/exportDownload';
import {
  canUseWebShare,
  canUseWebShareFiles,
  formatMobileFileSize,
  saveBlobToFiles,
  shareDownloadBlob,
  touchFriendlyButtonStyle,
} from '@/lib/download/mobileDownload';
import { canPreviewFile } from '@/lib/preview/filePreview';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

export type DownloadPreviewModalProps = {
  open: boolean;
  fileName: string;
  /** Preformatted size label, e.g. "2.4 MB" or "~1.2 MB". */
  fileSize: string;
  fileType: string;
  /** Preformatted created-at label. */
  createdAt: string;
  contentSummary: string;
  tenantName?: string;
  registerName?: string;
  /** Optional note under the detail card (e.g. fiscal disclaimer). */
  hint?: string;
  /** Raw bytes — enables >100 MB warning when provided. */
  sizeBytes?: number;
  /** When true, size is an estimate before generation. */
  isSizeEstimate?: boolean;
  /** Optional content for in-modal file preview (JSON/CSV/TXT/PDF). */
  contentPreview?: FilePreviewSource | null;
  /** Lazy-load preview content when not already available (e.g. history redownload). */
  resolveContentPreview?: () => Promise<FilePreviewSource>;
  confirmLoading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  /** Desktop File System Access save picker. Omitted when unsupported. */
  onSaveToFolder?: () => void | Promise<void>;
  /** Prefer default recipient for email delivery. */
  defaultEmailTo?: string;
  /** Optional subject override for email delivery. */
  emailSubject?: string;
  /** When set with sourceId, server can load the artifact without a client blob. */
  emailSourceKind?: string | null;
  emailSourceId?: string | null;
  /** Resolve export bytes for email send (after generation / from preview blob). */
  resolveEmailContent?: () => Promise<Blob>;
  /** Show "send as email" when resolveEmailContent or source refs are available. */
  enableSendEmail?: boolean;
};

type MetaRowProps = {
  label: string;
  value: ReactNode;
  isLast?: boolean;
};

function MetaRow({ label, value, isLast }: MetaRowProps) {
  return (
    <div
      style={{
        display: 'flex',
        gap: 8,
        padding: '4px 0',
        borderBottom: isLast ? undefined : '1px solid var(--ant-color-split, #f0f0f0)',
      }}
    >
      <Typography.Text type="secondary" style={{ minWidth: 96, flexShrink: 0 }}>
        {label}
      </Typography.Text>
      <Typography.Text style={{ flex: 1, wordBreak: 'break-word' }}>{value}</Typography.Text>
    </div>
  );
}

/**
 * Reusable pre-download preview: file name, format, size, created time,
 * optional tenant/register/content, copy name, download, cancel.
 * Mobile: ≥44px touch targets, MB-first size, Save to Files + native share.
 */
export function DownloadPreviewModal({
  open,
  fileName,
  fileSize,
  fileType,
  createdAt,
  contentSummary,
  tenantName,
  registerName,
  hint,
  sizeBytes,
  isSizeEstimate = false,
  contentPreview = null,
  resolveContentPreview,
  confirmLoading = false,
  onConfirm,
  onCancel,
  onSaveToFolder,
  defaultEmailTo = '',
  emailSubject,
  emailSourceKind,
  emailSourceId,
  resolveEmailContent,
  enableSendEmail = false,
}: DownloadPreviewModalProps) {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const isMobile = useMobileDownloadClient();
  const { getShortcutLabel } = useKeyboardShortcutLabels();
  const [busy, setBusy] = useState(false);
  const [contentPreviewOpen, setContentPreviewOpen] = useState(false);
  const [resolvedPreview, setResolvedPreview] = useState<FilePreviewSource | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [emailOpen, setEmailOpen] = useState(false);
  const showFolder = Boolean(onSaveToFolder) && canSaveBlobToFolder();
  const large = isLargeExport(sizeBytes);
  const loading = busy || confirmLoading;
  const activePreview = resolvedPreview ?? contentPreview;
  const canOpenContentPreview =
    canPreviewFile(fileName, fileType, activePreview?.blob?.type) &&
    (Boolean(activePreview?.text || activePreview?.blob || activePreview?.objectUrl) ||
      Boolean(resolveContentPreview));

  const previewBlob = activePreview?.blob ?? null;
  const showSaveToFiles =
    isMobile && Boolean(previewBlob) && (canUseWebShareFiles() || canUseWebShare());
  const showShare = isMobile && Boolean(previewBlob) && canUseWebShare();
  const showEmail =
    enableSendEmail &&
    (Boolean(resolveEmailContent) ||
      Boolean(previewBlob) ||
      Boolean(emailSourceKind && emailSourceId));

  const downloadShortcut = getShortcutLabel('downloadExport');
  const previewShortcut = getShortcutLabel('openDownloadPreview');
  const downloadTitle = t('keyboardShortcuts.downloadExportWithShortcut', {
    shortcut: downloadShortcut,
  });
  const previewTitle = t('keyboardShortcuts.openDownloadPreviewWithShortcut', {
    shortcut: previewShortcut,
  });

  const openContentPreview = async () => {
    if (activePreview?.text || activePreview?.blob || activePreview?.objectUrl) {
      setContentPreviewOpen(true);
      return;
    }
    if (!resolveContentPreview) return;
    setPreviewLoading(true);
    try {
      const source = await resolveContentPreview();
      setResolvedPreview(source);
      setContentPreviewOpen(true);
    } catch {
      notify.error(t('common.filePreview.loadFailed'));
    } finally {
      setPreviewLoading(false);
    }
  };

  const run = async (action: () => void | Promise<void>) => {
    setBusy(true);
    try {
      await action();
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    if (!open) {
      setContentPreviewOpen(false);
      setResolvedPreview(null);
      setPreviewLoading(false);
      setEmailOpen(false);
    }
  }, [open, fileName]);

  useKeyboardShortcutListener(
    KEYBOARD_SHORTCUT_EVENTS.downloadExport,
    () => {
      if (!open || loading) return;
      void run(onConfirm);
    },
    open && !loading
  );

  useKeyboardShortcutListener(
    KEYBOARD_SHORTCUT_EVENTS.openDownloadPreview,
    () => {
      if (!open || loading || !canOpenContentPreview) return;
      void openContentPreview();
    },
    open && !loading && canOpenContentPreview
  );

  const sizeDisplayBase =
    isMobile && sizeBytes != null && Number.isFinite(sizeBytes)
      ? formatMobileFileSize(sizeBytes, formatLocale)
      : fileSize;

  const sizeDisplay = isSizeEstimate
    ? t('common.exportDownload.sizeEstimate', { size: sizeDisplayBase })
    : sizeDisplayBase;

  const handleCopyFileName = async () => {
    try {
      await navigator.clipboard.writeText(fileName);
      notify.success(t('common.exportDownload.copyFileNameSuccess'));
    } catch {
      notify.error(t('common.exportDownload.copyFileNameFailed'));
    }
  };

  const handleShare = async () => {
    if (!previewBlob) return;
    try {
      const result = await shareDownloadBlob(previewBlob, fileName, {
        title: fileName,
        text: t('common.downloadProgress.shareText', { fileName }),
      });
      if (result === 'unsupported') {
        notify.info(t('common.downloadProgress.shareUnsupported'));
      }
    } catch {
      notify.error(t('common.downloadProgress.shareFailed'));
    }
  };

  const handleSaveToFiles = async () => {
    if (!previewBlob) return;
    try {
      const result = await saveBlobToFiles(previewBlob, fileName, triggerBlobDownload);
      if (result === 'cancelled') return;
      notify.success(t('common.downloadProgress.saveToFilesSuccess'));
    } catch {
      notify.error(t('common.downloadProgress.saveToFilesFailed'));
    }
  };

  const resolveEmailBlob = async (): Promise<Blob> => {
    if (resolveEmailContent) return resolveEmailContent();
    if (previewBlob) return previewBlob;
    throw new Error('No export content available for email.');
  };

  const btnSize = isMobile ? ('large' as const) : undefined;
  const btnStyle = isMobile ? touchFriendlyButtonStyle() : undefined;

  return (
    <>
      <Modal
        title={t('common.exportDownload.title')}
        open={open}
        onCancel={loading ? undefined : onCancel}
        destroyOnHidden
        maskClosable={!loading}
        keyboard={!loading}
        width={isMobile ? '100%' : 560}
        centered={!isMobile}
        styles={
          isMobile
            ? {
                content: { margin: 8, maxWidth: 'calc(100vw - 16px)' },
              }
            : undefined
        }
        footer={
          <Space
            orientation={isMobile ? 'vertical' : 'horizontal'}
            wrap={!isMobile}
            style={{ width: '100%', justifyContent: isMobile ? 'stretch' : 'flex-end' }}
            size={isMobile ? 'middle' : 'small'}
          >
            <Button
              size={btnSize}
              style={btnStyle}
              icon={<CopyOutlined />}
              onClick={() => void handleCopyFileName()}
              disabled={loading}
              block={isMobile}
            >
              {t('common.exportDownload.copyFileName')}
            </Button>
            {canOpenContentPreview ? (
              <Button
                size={btnSize}
                style={btnStyle}
                icon={<EyeOutlined />}
                onClick={() => void openContentPreview()}
                loading={previewLoading}
                disabled={loading}
                title={previewTitle}
                block={isMobile}
              >
                {t('common.exportDownload.preview')}
              </Button>
            ) : null}
            {showFolder ? (
              <Button
                size={btnSize}
                style={btnStyle}
                icon={<FolderOpenOutlined />}
                loading={loading}
                onClick={() => void run(() => onSaveToFolder?.())}
                block={isMobile}
              >
                {t('common.exportDownload.openInFolder')}
              </Button>
            ) : null}
            {showSaveToFiles ? (
              <Button
                size={btnSize}
                style={btnStyle}
                icon={<FolderOpenOutlined />}
                disabled={loading}
                onClick={() => void run(handleSaveToFiles)}
                block={isMobile}
              >
                {t('common.exportDownload.saveToFiles')}
              </Button>
            ) : null}
            {showShare ? (
              <Button
                size={btnSize}
                style={btnStyle}
                icon={<ShareAltOutlined />}
                disabled={loading}
                onClick={() => void run(handleShare)}
                block={isMobile}
              >
                {t('common.exportDownload.share')}
              </Button>
            ) : null}
            {showEmail ? (
              <Button
                size={btnSize}
                style={btnStyle}
                icon={<MailOutlined />}
                disabled={loading}
                onClick={() => setEmailOpen(true)}
                block={isMobile}
              >
                {t('common.exportDownload.sendAsEmail')}
              </Button>
            ) : null}
            <Button
              type="primary"
              size={btnSize}
              style={btnStyle}
              icon={<DownloadOutlined />}
              loading={loading}
              onClick={() => void run(onConfirm)}
              title={downloadTitle}
              block={isMobile}
            >
              {t('common.exportDownload.download')}
            </Button>
            <Button
              size={btnSize}
              style={btnStyle}
              icon={<CloseOutlined />}
              onClick={onCancel}
              disabled={loading}
              block={isMobile}
            >
              {t('common.exportDownload.cancel')}
            </Button>
          </Space>
        }
      >
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          {large ? (
            <Alert
              type="warning"
              showIcon
              title={t('common.exportDownload.largeFileWarning')}
              description={t('common.exportDownload.mayTakeAMoment')}
            />
          ) : null}

          <Card size="small" styles={{ body: { padding: 16 } }}>
            <Typography.Paragraph
              strong
              style={{ marginBottom: 12, wordBreak: 'break-all' }}
              copyable={{ text: fileName }}
            >
              {fileName}
            </Typography.Paragraph>

            <MetaRow label={t('common.exportDownload.format')} value={fileType} />
            <MetaRow label={t('common.exportDownload.size')} value={sizeDisplay} />
            <MetaRow label={t('common.exportDownload.created')} value={createdAt} />
            {tenantName ? (
              <MetaRow label={t('common.exportDownload.tenant')} value={tenantName} />
            ) : null}
            {registerName ? (
              <MetaRow label={t('common.exportDownload.register')} value={registerName} />
            ) : null}
            <MetaRow
              label={t('common.exportDownload.content')}
              value={contentSummary}
              isLast={!hint}
            />

            {hint ? (
              <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
                {hint}
              </Typography.Paragraph>
            ) : null}
          </Card>
        </Space>
      </Modal>

      {canOpenContentPreview && activePreview ? (
        <FilePreviewModal
          open={contentPreviewOpen}
          fileName={fileName}
          fileType={fileType}
          source={activePreview}
          onClose={() => setContentPreviewOpen(false)}
          onDownload={() => void run(onConfirm)}
          downloadLoading={loading}
        />
      ) : null}

      {showEmail ? (
        <SendExportEmailModal
          open={emailOpen}
          onClose={() => setEmailOpen(false)}
          fileName={fileName}
          sizeBytes={sizeBytes}
          defaultTo={defaultEmailTo}
          defaultSubject={emailSubject}
          sourceKind={emailSourceKind}
          sourceId={emailSourceId}
          resolveContent={
            resolveEmailContent || previewBlob ? () => resolveEmailBlob() : undefined
          }
        />
      ) : null}
    </>
  );
}
