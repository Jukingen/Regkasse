import type { Rule } from 'antd/es/form';
import {
    LOGIN_USERNAME_MAX_LENGTH,
    LOGIN_USERNAME_MIN_LENGTH,
    LOGIN_USERNAME_PATTERN,
    PASSWORD_MAX_LENGTH,
} from '@/features/users/constants/validation';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

type AuthTranslate = (key: string, options?: Record<string, string | number>) => string;

/** Login form rules aligned with backend login identifier contract. */
export function buildLoginFormRules(t: AuthTranslate): { loginIdentifier: Rule[]; password: Rule[] } {
    return {
        loginIdentifier: [
            { required: true, message: t('common.auth.validation.loginIdentifierRequired') },
            { min: LOGIN_USERNAME_MIN_LENGTH, message: t('common.auth.validation.loginIdentifierMin') },
            { max: LOGIN_USERNAME_MAX_LENGTH, message: t('common.auth.validation.loginIdentifierMax') },
            {
                validator: async (_, value) => {
                    const trimmed = String(value ?? '').trim();
                    if (!trimmed) return;
                    if (EMAIL_PATTERN.test(trimmed)) return;
                    if (!LOGIN_USERNAME_PATTERN.test(trimmed)) {
                        throw new Error(t('common.auth.validation.loginIdentifierPattern'));
                    }
                },
            },
        ],
        password: [
            { required: true, message: t('common.auth.validation.passwordRequired') },
            { max: PASSWORD_MAX_LENGTH, message: t('common.auth.validation.passwordMax') },
        ],
    };
}
