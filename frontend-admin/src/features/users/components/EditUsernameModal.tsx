'use client';

import { useEffect } from 'react';
import { Alert, Form, Input, Modal, message } from 'antd';

import {
    useUpdateAdminUsernameMutation,
    type UpdateAdminUsernameResponse,
} from '@/features/users/api/users';
import {
    createLoginUserNameRules,
    LOGIN_USERNAME_MAX_LENGTH,
    NOTES_MAX_LENGTH,
} from '@/features/users/constants/validation';
import { useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';

type FormValues = {
    newUsername: string;
    reason?: string;
};

export type EditUsernameModalProps = {
    open: boolean;
    userId: string;
    currentUsername: string;
    onClose: () => void;
    onSuccess: (result: UpdateAdminUsernameResponse) => void;
};

export function EditUsernameModal({
    open,
    userId,
    currentUsername,
    onClose,
    onSuccess,
}: EditUsernameModalProps) {
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
                }),
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
            destroyOnHidden
        >
            <Form form={form} layout="vertical" preserve={false}>
                <Form.Item
                    name="newUsername"
                    label={t('users.username.newLabel')}
                    rules={usernameRules}
                >
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

                <Alert type="warning" showIcon message={t('users.username.auditHint')} />
            </Form>
        </Modal>
    );
}
