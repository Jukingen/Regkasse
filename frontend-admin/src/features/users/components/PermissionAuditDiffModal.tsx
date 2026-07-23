'use client';

import {
  CopyOutlined,
  CommentOutlined,
  UndoOutlined,
} from '@ant-design/icons';
import { Alert, Button, Input, Modal, Space, Table, Tag, Typography } from 'antd';
import React, { useMemo, useState } from 'react';

import type { PermissionAuditEntry } from '@/features/audit/api/permissionAudit';
import {
  formatPermissionDiffClipboard,
  linesFromDedicatedEntry,
  summarizePermissionDiffLines,
  toPermissionDiffTableRows,
  type PermissionDiffTableRow,
} from '@/features/users/utils/permissionAuditDiffView';
import type { PermissionStateKind } from '@/features/users/utils/permissionAuditDiff';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

type Props = {
  open: boolean;
  onClose: () => void;
  entry: PermissionAuditEntry | null;
  /** Extra lines when a full role snapshot diff is available. */
  extraRows?: PermissionDiffTableRow[];
  canRevert?: boolean;
  onRevert?: (entry: PermissionAuditEntry) => void;
  onAddNote?: (entry: PermissionAuditEntry, note: string) => Promise<void>;
};

function stateLabel(
  state: PermissionStateKind | null,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  if (!state) return '—';
  const key = `users.permissionAudit.state.${state}`;
  const labeled = t(key);
  return labeled === key ? state : labeled;
}

function markerEmoji(marker: PermissionDiffTableRow['marker']): string {
  switch (marker) {
    case 'green':
      return '🟢';
    case 'red':
      return '🔴';
    case 'yellow':
      return '🟡';
    default:
      return '⚪';
  }
}

export function PermissionAuditDiffModal({
  open,
  onClose,
  entry,
  extraRows,
  canRevert,
  onRevert,
  onAddNote,
}: Props) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const [noteOpen, setNoteOpen] = useState(false);
  const [note, setNote] = useState('');
  const [savingNote, setSavingNote] = useState(false);

  const rows = useMemo(() => {
    if (extraRows?.length) return extraRows;
    if (!entry) return [];
    return toPermissionDiffTableRows(linesFromDedicatedEntry(entry));
  }, [entry, extraRows]);

  const summary = useMemo(() => summarizePermissionDiffLines(rows), [rows]);

  const when = entry?.timestamp ? formatDateTime(entry.timestamp) : '—';
  const actor = entry
    ? `${entry.actorName || t('users.permissionAudit.systemActor')}${
        entry.actorEmail ? ` (${entry.actorEmail})` : ''
      }`
    : '—';

  const handleCopy = async () => {
    const text = formatPermissionDiffClipboard(rows, {
      roleName: entry?.roleName,
      timestamp: when,
      actor,
    }, (s) => stateLabel(s, t));
    try {
      await navigator.clipboard.writeText(text);
      message.success(t('users.permissionAudit.diff.copySuccess'));
    } catch {
      message.error(t('users.permissionAudit.diff.copyError'));
    }
  };

  const handleSaveNote = async () => {
    if (!entry || !onAddNote || !note.trim()) return;
    setSavingNote(true);
    try {
      await onAddNote(entry, note.trim());
      message.success(t('users.permissionAudit.diff.noteSuccess'));
      setNoteOpen(false);
      setNote('');
    } catch {
      message.error(t('users.permissionAudit.diff.noteError'));
    } finally {
      setSavingNote(false);
    }
  };

  return (
    <>
      <Modal
        title={t('users.permissionAudit.diff.title', { when })}
        open={open}
        onCancel={onClose}
        width={720}
        destroyOnHidden
        footer={[
          <Button key="copy" icon={<CopyOutlined />} onClick={() => void handleCopy()}>
            {t('users.permissionAudit.diff.copy')}
          </Button>,
          <Button
            key="note"
            icon={<CommentOutlined />}
            disabled={!entry || !onAddNote}
            onClick={() => setNoteOpen(true)}
          >
            {t('users.permissionAudit.diff.addNote')}
          </Button>,
          <Button
            key="revert"
            type="primary"
            danger
            icon={<UndoOutlined />}
            disabled={!canRevert || !entry || !onRevert}
            onClick={() => {
              if (entry && onRevert) onRevert(entry);
            }}
          >
            {t('users.permissionAudit.diff.revertToState')}
          </Button>,
          <Button key="close" onClick={onClose}>
            {t('common.buttons.close')}
          </Button>,
        ]}
      >
        {entry ? (
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <div>
              <Typography.Text type="secondary">
                {t('users.permissionAudit.diff.role')}:{' '}
              </Typography.Text>
              <Typography.Text strong>{entry.roleName || '—'}</Typography.Text>
            </div>
            <div>
              <Typography.Text type="secondary">
                {t('users.permissionAudit.diff.actor')}:{' '}
              </Typography.Text>
              <Typography.Text strong>{actor}</Typography.Text>
            </div>

            <Alert
              type="info"
              showIcon
              title={t('users.permissionAudit.diff.summary', {
                changed: summary.changed,
                added: summary.added,
                removed: summary.removed,
              })}
            />

            {rows.length === 0 ? (
              <Typography.Text type="secondary">
                {t('users.permissionAudit.noDiffDetails')}
              </Typography.Text>
            ) : (
              <Table
                size="small"
                pagination={false}
                rowKey="key"
                dataSource={rows}
                columns={[
                  {
                    title: t('users.permissionAudit.diff.colPermission'),
                    dataIndex: 'permissionKey',
                    key: 'permissionKey',
                    render: (v: string) => <Typography.Text code>{v}</Typography.Text>,
                  },
                  {
                    title: t('users.permissionAudit.diff.colBefore'),
                    dataIndex: 'oldState',
                    key: 'oldState',
                    width: 140,
                    render: (v: PermissionStateKind | null) => stateLabel(v, t),
                  },
                  {
                    title: t('users.permissionAudit.diff.colAfter'),
                    dataIndex: 'newState',
                    key: 'newState',
                    width: 140,
                    render: (v: PermissionStateKind | null) => stateLabel(v, t),
                  },
                  {
                    title: t('users.permissionAudit.diff.colDiff'),
                    dataIndex: 'marker',
                    key: 'marker',
                    width: 72,
                    align: 'center',
                    render: (m: PermissionDiffTableRow['marker']) => (
                      <Tag style={{ margin: 0 }}>{markerEmoji(m)}</Tag>
                    ),
                  },
                ]}
              />
            )}
          </Space>
        ) : null}
      </Modal>

      <Modal
        title={t('users.permissionAudit.diff.addNote')}
        open={noteOpen}
        onCancel={() => setNoteOpen(false)}
        onOk={() => void handleSaveNote()}
        confirmLoading={savingNote}
        okText={t('users.permissionAudit.diff.saveNote')}
        destroyOnHidden
      >
        <Input.TextArea
          rows={3}
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder={t('users.permissionAudit.diff.notePlaceholder')}
        />
      </Modal>
    </>
  );
}
