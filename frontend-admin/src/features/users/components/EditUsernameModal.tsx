'use client';

import { Alert, App, Form, Input, Modal, Typography } from 'antd';
import { useEffect } from 'react';

import {
  type UpdateAdminUsernameResponse,
  useUpdateAdminUsernameMutation,
} from '@/features/users/api/users';
import {
  LOGIN_USERNAME_MAX_LENGTH,
  NOTES_MAX_LENGTH,
  createLoginUserNameRules,
} from '@/features/users/constants/validation';
import { useI18n } from '@/i18n';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

type FormValues = {
  newUsername: string;
  reason?: string;
};

export type EditUsernameModalProps = {
  open: boolean;
  userId: string;
  currentUsername: string;
  /** Optional context shown under the current username field. */
  userEmail?: string | null;
  onClose: () => void;
  onSuccess: (result: UpdateAdminUsernameResponse) => void;
};

export function EditUsernameModal(props: EditUsernameModalProps) {
  if (!props.open) {
    return null;
  }
  return <EditUsernameModalContent {...props} />;
}

function EditUsernameModalContent({
  open,
  userId,
  currentUsername,
  userEmail,
  onClose,
  onSuccess,
}: EditUsernameModalProps) {
  const { message } = App.useApp();

  const { t } = useI18n();
  const [form] = Form.useForm<FormValues>();
  const updateUsername = useUpdateAdminUsernameMutation();

  const usernameRules = createLoginUserNameRules({
    required: t('users.username.validation.required'),
    min: t('users.username.validation.min'),
    max: t('users.username.validation.max'),
    pattern: t('users.username.validation.pattern'),
  });

  useEffect(() => {
    if (!open) return;
    form.setFieldsValue({
      newUsername: currentUsername,
      reason: undefined,
    });
  }, [open, currentUsername, form]);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      const trimmed = values.newUsername.trim();
      if (trimmed === currentUsername.trim()) {
        onClose();
        return;
      }

      const result = await updateUsername.mutateAsync({
        userId,
        newUsername: trimmed,
        reason: values.reason?.trim() || undefined,
      });
      message.success(t('users.username.updateSuccess'));
      onSuccess(result);
      form.resetFields();
      onClose();
    } catch (error) {
      const normalized = normalizeApiError(error);
      if (normalized.httpStatus === 409) {
        message.error(t('users.username.conflict'));
        return;
      }
      message.error(
        getUserFacingApiErrorMessage(t, error, {
          fallbackKey: 'users.username.updateFailed',
          logContext: 'EditUsernameModal.update',
        })
      );
    }
  };

  return (
    <Modal
      title={t('users.username.editTitle')}
      open={open}
      onCancel={onClose}
      onOk={() => void handleSubmit()}
      okText={t('common.buttons.save')}
      cancelText={t('common.buttons.cancel')}
      confirmLoading={updateUsername.isPending}
      forceRender
    >
      <Alert
        type="info"
        showIcon
        title={t('users.username.infoTitle')}
        description={t('users.username.infoDescription')}
        style={{ marginBottom: 16 }}
      />

      <Form form={form} layout="vertical" preserve={false}>
        <Form.Item label={t('users.username.currentLabel')}>
          <Input value={currentUsername.trim() || '—'} disabled autoComplete="off" />
          {userEmail?.trim() ? (
            <Typography.Text
              type="secondary"
              style={{ fontSize: 12, display: 'block', marginTop: 4 }}
            >
              {userEmail.trim()}
            </Typography.Text>
          ) : null}
        </Form.Item>

        <Form.Item name="newUsername" label={t('users.username.newLabel')} rules={usernameRules}>
          <Input
            placeholder={t('users.username.placeholder')}
            maxLength={LOGIN_USERNAME_MAX_LENGTH}
            autoComplete="off"
          />
        </Form.Item>

        <Form.Item
          name="reason"
          label={t('users.username.reasonLabel')}
          help={t('users.username.reasonHelp')}
        >
          <Input.TextArea
            rows={3}
            maxLength={NOTES_MAX_LENGTH}
            showCount
            placeholder={t('users.username.reasonPlaceholder')}
          />
        </Form.Item>
      </Form>

      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {t('common.auth.loginIdentifierCaseHint')}
      </Typography.Text>
    </Modal>
  );
}
