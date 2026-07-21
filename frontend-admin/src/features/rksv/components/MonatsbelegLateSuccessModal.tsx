'use client';

import { Alert, Button, Descriptions, Modal, Tag } from 'antd';
import { useRouter } from 'next/navigation';

import type { MonatsbelegLateSuccessResult } from '@/features/rksv/types/createMonatsbelegResponseExtended';
import { formatDateTime } from '@/i18n/formatting';
import { formatViennaYearMonth } from '@/shared/utils/viennaCalendar';

export type MonatsbelegLateSuccessModalProps = {
  open: boolean;
  result: MonatsbelegLateSuccessResult | null;
  onClose: () => void;
};

export function MonatsbelegLateSuccessModal({
  open,
  result,
  onClose,
}: MonatsbelegLateSuccessModalProps) {
  const router = useRouter();

  return (
    <Modal
      title="Monatsbeleg erfolgreich nachgeholt"
      open={open}
      onCancel={onClose}
      footer={[
        <Button key="close" onClick={onClose}>
          Schließen
        </Button>,
        <Button
          key="audit"
          type="link"
          onClick={() => {
            onClose();
            router.push('/audit-logs');
          }}
        >
          Audit-Log anzeigen
        </Button>,
      ]}
      width={520}
      destroyOnHidden
    >
      {result ? (
        <>
          <Descriptions column={1} bordered size="small">
            <Descriptions.Item label="Abgedeckter Zeitraum">
              {formatViennaYearMonth(result.year, result.month)}
            </Descriptions.Item>
            <Descriptions.Item label="Verspätung">
              {result.daysLate} {result.daysLate === 1 ? 'Tag' : 'Tage'}
            </Descriptions.Item>
            <Descriptions.Item label="Erstellungsdatum">
              {formatDateTime(result.createdAt, '')}
            </Descriptions.Item>
            <Descriptions.Item label="Status">
              {result.isLateCreated ? (
                <Tag color="orange">Verspätet erstellt</Tag>
              ) : (
                <Tag color="green">Erstellt</Tag>
              )}
            </Descriptions.Item>
            <Descriptions.Item label="Beleg-Nummer">{result.receiptNumber}</Descriptions.Item>
          </Descriptions>

          <Alert
            title="Audit-Log Eintrag"
            description="Dieser Vorgang wurde im Audit-Log protokolliert."
            type="info"
            showIcon
            style={{ marginTop: 16 }}
          />
        </>
      ) : null}
    </Modal>
  );
}
