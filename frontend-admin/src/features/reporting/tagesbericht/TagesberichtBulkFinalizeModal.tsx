'use client';

import { Modal, Typography } from 'antd';

type Props = {
  open: boolean;
  confirmTitle: string;
  confirmBody: string;
  okLabel: string;
  cancelLabel: string;
  loading?: boolean;
  onOk: () => void;
  onCancel: () => void;
};

export function TagesberichtBulkFinalizeModal({
  open,
  confirmTitle,
  confirmBody,
  okLabel,
  cancelLabel,
  loading,
  onOk,
  onCancel,
}: Props) {
  return (
    <Modal
      open={open}
      title={confirmTitle}
      okText={okLabel}
      cancelText={cancelLabel}
      confirmLoading={loading}
      onOk={onOk}
      onCancel={onCancel}
      destroyOnHidden
    >
      <Typography.Paragraph data-testid="tagesbericht-bulk-finalize-modal">
        {confirmBody}
      </Typography.Paragraph>
    </Modal>
  );
}
