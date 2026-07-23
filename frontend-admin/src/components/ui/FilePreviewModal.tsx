'use client';

import {
  CloseOutlined,
  CopyOutlined,
  DownloadOutlined,
  EyeOutlined,
  SearchOutlined,
} from '@ant-design/icons';
import { Alert, Button, Input, Modal, Space, Table, Typography } from 'antd';
import { useEffect, useMemo, useState } from 'react';

import { useNotify } from '@/hooks/useNotify';
import { JsonCodePreview } from '@/lib/preview/JsonCodePreview';
import { TextCodePreview } from '@/lib/preview/TextCodePreview';
import {
  detectPreviewKind,
  parseCsvPreview,
  preparePlainText,
  prettyPrintJson,
  type PreviewableFileKind,
} from '@/lib/preview/filePreview';
import { useI18n } from '@/i18n/I18nProvider';

export type FilePreviewSource = {
  /** UTF-8 text for json/csv/txt */
  text?: string | null;
  /** Blob for pdf (or any binary); text kinds may also pass blob */
  blob?: Blob | null;
  /** Object URL for PDF when already created by caller */
  objectUrl?: string | null;
};

export type FilePreviewModalProps = {
  open: boolean;
  fileName: string;
  fileType?: string | null;
  mimeType?: string | null;
  source: FilePreviewSource;
  onClose: () => void;
  onDownload?: () => void | Promise<void>;
  downloadLoading?: boolean;
};

async function blobToText(blob: Blob): Promise<string> {
  return blob.text();
}

/**
 * Content preview for JSON / CSV / TXT / PDF with search, copy, and download.
 */
export function FilePreviewModal({
  open,
  fileName,
  fileType,
  mimeType,
  source,
  onClose,
  onDownload,
  downloadLoading = false,
}: FilePreviewModalProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const kind = useMemo(
    () => detectPreviewKind(fileName, fileType, mimeType ?? source.blob?.type),
    [fileName, fileType, mimeType, source.blob?.type]
  );

  const [searchQuery, setSearchQuery] = useState('');
  const [resolvedText, setResolvedText] = useState<string | null>(null);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState(false);

  useEffect(() => {
    if (!open) {
      setSearchQuery('');
      setResolvedText(null);
      setPdfUrl(null);
      setLoadError(false);
      setLoading(false);
      return;
    }

    let revoked: string | null = null;
    let cancelled = false;

    async function load() {
      setLoading(true);
      setLoadError(false);
      try {
        if (kind === 'pdf') {
          if (source.objectUrl) {
            if (!cancelled) setPdfUrl(source.objectUrl);
            return;
          }
          if (source.blob) {
            const url = URL.createObjectURL(source.blob);
            revoked = url;
            if (!cancelled) setPdfUrl(url);
            return;
          }
          if (!cancelled) setLoadError(true);
          return;
        }

        if (source.text != null) {
          if (!cancelled) setResolvedText(source.text);
          return;
        }
        if (source.blob) {
          const text = await blobToText(source.blob);
          if (!cancelled) setResolvedText(text);
          return;
        }
        if (!cancelled) setLoadError(true);
      } catch {
        if (!cancelled) setLoadError(true);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void load();

    return () => {
      cancelled = true;
      if (revoked) URL.revokeObjectURL(revoked);
    };
  }, [open, kind, source.text, source.blob, source.objectUrl]);

  const jsonPrepared = useMemo(() => {
    if (kind !== 'json' || resolvedText == null) return null;
    return prettyPrintJson(resolvedText);
  }, [kind, resolvedText]);

  const textPrepared = useMemo(() => {
    if ((kind !== 'txt' && kind !== 'unsupported') || resolvedText == null) return null;
    return preparePlainText(resolvedText);
  }, [kind, resolvedText]);

  const csvPrepared = useMemo(() => {
    if (kind !== 'csv' || resolvedText == null) return null;
    return parseCsvPreview(resolvedText);
  }, [kind, resolvedText]);

  const lineCount = useMemo(() => {
    if (jsonPrepared) return jsonPrepared.lineCount;
    if (textPrepared) return textPrepared.lineCount;
    if (csvPrepared) return csvPrepared.totalRowCount + (csvPrepared.headers.length ? 1 : 0);
    return 0;
  }, [jsonPrepared, textPrepared, csvPrepared]);

  const clipboardText = useMemo(() => {
    if (jsonPrepared) return jsonPrepared.text;
    if (textPrepared) return textPrepared.text;
    if (kind === 'csv' && resolvedText) return resolvedText;
    return null;
  }, [jsonPrepared, textPrepared, kind, resolvedText]);

  const truncated =
    Boolean(jsonPrepared?.truncated) ||
    Boolean(textPrepared?.truncated) ||
    Boolean(csvPrepared?.truncated);

  const handleCopy = async () => {
    if (!clipboardText) {
      notify.errorKey('common.filePreview.copyUnavailable');
      return;
    }
    try {
      await navigator.clipboard.writeText(clipboardText);
      notify.successKey('common.filePreview.copySuccess');
    } catch {
      notify.errorKey('common.filePreview.copyFailed');
    }
  };

  const showSearch = kind === 'json' || kind === 'txt' || kind === 'csv' || kind === 'unsupported';

  return (
    <Modal
      open={open}
      onCancel={onClose}
      width={920}
      destroyOnHidden
      title={
        <Space>
          <EyeOutlined />
          <span>
            {t('common.filePreview.title', { fileName })}
          </span>
        </Space>
      }
      footer={
        <div
          style={{
            display: 'flex',
            flexWrap: 'wrap',
            gap: 12,
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <Typography.Text type="secondary">
            {kind === 'pdf'
              ? t('common.filePreview.pdfHint')
              : t('common.filePreview.lineCount', { count: lineCount })}
          </Typography.Text>
          <Space wrap>
            {clipboardText ? (
              <Button icon={<CopyOutlined />} onClick={() => void handleCopy()}>
                {t('common.filePreview.copy')}
              </Button>
            ) : null}
            {onDownload ? (
              <Button
                type="primary"
                icon={<DownloadOutlined />}
                loading={downloadLoading}
                onClick={() => void onDownload()}
              >
                {t('common.filePreview.download')}
              </Button>
            ) : null}
            <Button icon={<CloseOutlined />} onClick={onClose}>
              {t('common.filePreview.close')}
            </Button>
          </Space>
        </div>
      }
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        {showSearch ? (
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t('common.filePreview.searchPlaceholder')}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        ) : null}

        {truncated ? (
          <Alert type="info" showIcon title={t('common.filePreview.truncated')} />
        ) : null}

        {loading ? (
          <Typography.Text type="secondary">{t('common.filePreview.loading')}</Typography.Text>
        ) : null}

        {loadError || kind === 'unsupported' ? (
          <Alert
            type="warning"
            showIcon
            title={
              kind === 'unsupported'
                ? t('common.filePreview.unsupported')
                : t('common.filePreview.loadFailed')
            }
          />
        ) : null}

        {!loading && !loadError && kind === 'json' && jsonPrepared ? (
          <JsonCodePreview text={jsonPrepared.text} searchQuery={searchQuery} maxHeight="60vh" />
        ) : null}

        {!loading && !loadError && kind === 'txt' && textPrepared ? (
          <TextCodePreview text={textPrepared.text} searchQuery={searchQuery} maxHeight="60vh" />
        ) : null}

        {!loading && !loadError && kind === 'csv' && csvPrepared ? (
          <CsvPreviewTable data={csvPrepared} searchQuery={searchQuery} />
        ) : null}

        {!loading && !loadError && kind === 'pdf' && pdfUrl ? (
          <iframe
            src={pdfUrl}
            title={t('common.filePreview.pdfFrameTitle', { fileName })}
            style={{ width: '100%', height: '60vh', border: '1px solid #f0f0f0', borderRadius: 6 }}
          />
        ) : null}

        {!loading && !loadError && kind === 'unsupported' && textPrepared ? (
          <TextCodePreview text={textPrepared.text} searchQuery={searchQuery} maxHeight="60vh" />
        ) : null}
      </Space>
    </Modal>
  );
}

function CsvPreviewTable({
  data,
  searchQuery,
}: {
  data: ReturnType<typeof parseCsvPreview>;
  searchQuery: string;
}) {
  const q = searchQuery.trim().toLowerCase();
  const filteredRows = useMemo(() => {
    if (!q) return data.rows;
    return data.rows.filter((row) => row.some((cell) => cell.toLowerCase().includes(q)));
  }, [data.rows, q]);

  const columns = data.headers.map((h, idx) => ({
    title: h,
    dataIndex: String(idx),
    key: `c-${idx}`,
    ellipsis: true,
    render: (value: string) => value ?? '',
  }));

  const tableData = filteredRows.map((row, rowIdx) => {
    const record: Record<string, string | number> = { key: rowIdx };
    data.headers.forEach((_, colIdx) => {
      record[String(colIdx)] = row[colIdx] ?? '';
    });
    return record;
  });

  return (
    <Table
      size="small"
      pagination={{ pageSize: 50, showSizeChanger: false }}
      scroll={{ x: true, y: 420 }}
      columns={columns}
      dataSource={tableData}
    />
  );
}

export function isPreviewableFileKind(kind: PreviewableFileKind): boolean {
  return kind !== 'unsupported';
}
