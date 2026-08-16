'use client';

import { Descriptions, Divider, Modal, Space, Tag, Typography } from 'antd';
import Link from 'next/link';
import React from 'react';

import type { AdminShiftRow } from '@/features/shifts/api/shiftsOverview';
import {
  cashierInitial,
  differenceTextColor,
  shiftStatusTagColor,
  shortUserId,
} from '@/features/shifts/utils/shiftHistoryDisplay';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';

export type ShiftDetailModalProps = {
  open: boolean;
  shift: AdminShiftRow | null;
  onClose: () => void;
};

export const ShiftDetailModal: React.FC<ShiftDetailModalProps> = ({ open, shift, onClose }) => {
  const { t, formatLocale } = useI18n();
  const ts = (path: string) => t(`shifts:${path}`);

  const formatDt = (value?: string | null) =>
    value
      ? formatDateTime(value, formatLocale, { dateStyle: 'short', timeStyle: 'short' })
      : FORMAT_EMPTY_DISPLAY;

  const formatMoney = (value?: number | null) => formatCurrency(value ?? 0, formatLocale);

  const differenceExplanation = (() => {
    if (!shift) return null;
    if (shift.status === 'Discrepancy') {
      return ts('details.differenceDiscrepancy');
    }
    if ((shift.difference ?? 0) === 0) {
      return ts('details.differenceBalanced');
    }
    return ts('details.differenceOther');
  })();

  return (
    <Modal
      open={open}
      onCancel={onClose}
      onOk={onClose}
      okText={ts('details.close')}
      cancelButtonProps={{ style: { display: 'none' } }}
      title={ts('details.title')}
      width={640}
      destroyOnHidden
    >
      {!shift ? null : (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          <Descriptions size="small" column={1} bordered>
            <Descriptions.Item label={ts('columns.cashier')}>
              {cashierInitial(shift.cashierName)} {shift.cashierName} (#{shortUserId(shift.cashierId)})
            </Descriptions.Item>
            <Descriptions.Item label={ts('userId')}>{shift.cashierId}</Descriptions.Item>
            <Descriptions.Item label={ts('columns.register')}>
              {shift.registerNumber?.trim() || shift.cashRegisterId}
            </Descriptions.Item>
            <Descriptions.Item label={ts('columns.startedAt')}>
              {formatDt(shift.startedAt)}
            </Descriptions.Item>
            <Descriptions.Item label={ts('columns.endedAt')}>{formatDt(shift.endedAt)}</Descriptions.Item>
            <Descriptions.Item label={ts('columns.status')}>
              <Tag color={shiftStatusTagColor(shift.status)}>
                {ts(`status.${shift.status}`) || shift.status}
              </Tag>
            </Descriptions.Item>
          </Descriptions>

          <div>
            <Typography.Text strong>{ts('details.paymentBreakdown')}</Typography.Text>
            <Descriptions size="small" column={1} bordered style={{ marginTop: 8 }}>
              <Descriptions.Item label={ts('columns.sales')}>
                {formatMoney(shift.totalSales)}
              </Descriptions.Item>
              <Descriptions.Item label={ts('columns.cash')}>
                {formatMoney(shift.totalCash)}
              </Descriptions.Item>
              <Descriptions.Item label={ts('columns.card')}>
                {formatMoney(shift.totalCard)}
              </Descriptions.Item>
              <Descriptions.Item label={ts('columns.startBalance')}>
                {formatMoney(shift.startBalance)}
              </Descriptions.Item>
              <Descriptions.Item label={ts('columns.endBalance')}>
                {formatMoney(shift.endBalance)}
              </Descriptions.Item>
              <Descriptions.Item label={ts('columns.cashCount')}>
                {shift.cashCount == null ? FORMAT_EMPTY_DISPLAY : formatMoney(shift.cashCount)}
              </Descriptions.Item>
              <Descriptions.Item label={ts('columns.difference')}>
                <Typography.Text style={{ color: differenceTextColor(shift.difference) }}>
                  {formatMoney(shift.difference)}
                </Typography.Text>
              </Descriptions.Item>
            </Descriptions>
          </div>

          <div>
            <Typography.Text strong>{ts('details.differenceExplanation')}</Typography.Text>
            <Typography.Paragraph type="secondary" style={{ marginTop: 4, marginBottom: 0 }}>
              {differenceExplanation}
            </Typography.Paragraph>
            {shift.notes ? (
              <Typography.Paragraph style={{ marginTop: 8, marginBottom: 0 }}>
                <Typography.Text type="secondary">{ts('details.notes')}: </Typography.Text>
                {shift.notes}
              </Typography.Paragraph>
            ) : null}
          </div>

          {shift.dailyClosingId ? (
            <>
              <Divider style={{ margin: '8px 0' }} />
              <Link href="/tagesabschluss">{ts('details.openDailyClosing')}</Link>
            </>
          ) : null}
        </Space>
      )}
    </Modal>
  );
};
