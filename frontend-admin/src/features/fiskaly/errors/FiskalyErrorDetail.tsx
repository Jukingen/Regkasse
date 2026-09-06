'use client';

import { Alert, Button, Descriptions, Drawer, Skeleton, Space, Tag, Typography } from 'antd';

import type { FiskalyErrorDetail, FiskalyErrorReviewStatus } from '@/features/fiskaly/api/fiskalyErrors';
import {
  knownErrorSolutionKey,
  knownErrorSolutionTitleKey,
} from '@/features/fiskaly/errors/knownErrorSolutions';

function prettyJson(raw: string | null | undefined): string {
  if (!raw) return '—';
  try {
    return JSON.stringify(JSON.parse(raw) as unknown, null, 2);
  } catch {
    return raw;
  }
}

function reviewColor(status: string): 'default' | 'success' | 'warning' | 'error' {
  if (status === 'resolved') return 'success';
  if (status === 'known_issue') return 'warning';
  return 'error';
}

export type FiskalyErrorDetailProps = {
  open: boolean;
  loading: boolean;
  error: boolean;
  isSuperAdmin: boolean;
  canResolve: boolean;
  resolvePending: boolean;
  detail: FiskalyErrorDetail | undefined;
  title: string;
  loadFailed: string;
  labels: {
    date: string;
    code: string;
    message: string;
    type: string;
    tenant: string;
    user: string;
    status: string;
    receipt: string;
    register: string;
    request: string;
    response: string;
    stack: string;
    solution: string;
    markResolved: string;
    markKnownIssue: string;
    markOpen: string;
  };
  kindLabel: (value: string) => string;
  reviewLabel: (status: string) => string;
  formatDate: (value: string) => string;
  t: (key: string) => string;
  onClose: () => void;
  onResolve: (id: string, status: FiskalyErrorReviewStatus) => void;
};

export function FiskalyErrorDetailView({
  open,
  loading,
  error,
  isSuperAdmin,
  canResolve,
  resolvePending,
  detail,
  title,
  loadFailed,
  labels,
  kindLabel,
  reviewLabel,
  formatDate,
  t,
  onClose,
  onResolve,
}: FiskalyErrorDetailProps) {
  const solutionKey = knownErrorSolutionKey(detail?.errorCode);
  const solutionTitleKey = knownErrorSolutionTitleKey(detail?.errorCode);
  const solutionText = solutionKey
    ? t(solutionKey)
    : (detail?.knownSolution?.hint ?? null);

  return (
    <Drawer
      title={title}
      open={open}
      onClose={onClose}
      size="large"
      extra={
        canResolve && detail ? (
          <Space>
            <Button
              size="small"
              disabled={detail.reviewStatus === 'resolved' || resolvePending}
              onClick={() => onResolve(detail.id, 'resolved')}
            >
              {labels.markResolved}
            </Button>
            <Button
              size="small"
              disabled={detail.reviewStatus === 'known_issue' || resolvePending}
              onClick={() => onResolve(detail.id, 'known_issue')}
            >
              {labels.markKnownIssue}
            </Button>
          </Space>
        ) : null
      }
    >
      {loading && !detail ? <Skeleton active paragraph={{ rows: 8 }} /> : null}
      {error && !detail ? <Alert type="error" showIcon title={loadFailed} /> : null}
      {detail ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          {solutionText ? (
            <Alert
              type="info"
              showIcon
              title={solutionTitleKey ? t(solutionTitleKey) : (detail.knownSolution?.title ?? labels.solution)}
              description={solutionText}
            />
          ) : null}
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={labels.date}>{formatDate(detail.createdAtUtc)}</Descriptions.Item>
            <Descriptions.Item label={labels.code}>{detail.errorCode || '—'}</Descriptions.Item>
            <Descriptions.Item label={labels.message}>{detail.errorMessage || '—'}</Descriptions.Item>
            <Descriptions.Item label={labels.type}>{kindLabel(detail.operationType)}</Descriptions.Item>
            <Descriptions.Item label={labels.status}>
              <Tag color={reviewColor(detail.reviewStatus)}>{reviewLabel(detail.reviewStatus)}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label={labels.user}>
              {detail.userDisplayName || detail.userId}
            </Descriptions.Item>
            {isSuperAdmin ? (
              <Descriptions.Item label={labels.tenant}>
                {detail.tenantName || detail.tenantId}
              </Descriptions.Item>
            ) : null}
            <Descriptions.Item label={labels.receipt}>{detail.receiptNumber || '—'}</Descriptions.Item>
            <Descriptions.Item label={labels.register}>
              {detail.cashRegisterName || detail.cashRegisterId}
            </Descriptions.Item>
          </Descriptions>
          <div>
            <Typography.Text strong>{labels.request}</Typography.Text>
            <pre style={{ whiteSpace: 'pre-wrap', marginTop: 8 }}>{prettyJson(detail.requestPayloadJson)}</pre>
          </div>
          <div>
            <Typography.Text strong>{labels.response}</Typography.Text>
            <pre style={{ whiteSpace: 'pre-wrap', marginTop: 8 }}>{prettyJson(detail.responsePayloadJson)}</pre>
          </div>
          {detail.stackTrace ? (
            <div>
              <Typography.Text strong>{labels.stack}</Typography.Text>
              <pre style={{ whiteSpace: 'pre-wrap', marginTop: 8 }}>{detail.stackTrace}</pre>
            </div>
          ) : null}
        </Space>
      ) : null}
    </Drawer>
  );
}
