/**
 * Client-side validation for super-admin tenant create form.
 */

const COMPANY_NAME_MIN = 2;
const COMPANY_NAME_MAX = 200;
const ADDRESS_MAX = 500;

export type CompanyNameValidationCode = 'required' | 'tooShort' | 'tooLong';
export type ContactEmailValidationCode = 'required' | 'invalid';
export type PhoneValidationCode = 'invalid';
export type AddressValidationCode = 'tooLong';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
/** Digits, spaces, +, -, parentheses — typical AT/EU phone input. */
const PHONE_PATTERN = /^\+?[\d\s\-()/]{6,30}$/;

export function validateCompanyName(value: string | undefined): CompanyNameValidationCode | null {
    const trimmed = value?.trim() ?? '';
    if (!trimmed) {
        return 'required';
    }
    if (trimmed.length < COMPANY_NAME_MIN) {
        return 'tooShort';
    }
    if (trimmed.length > COMPANY_NAME_MAX) {
        return 'tooLong';
    }
    return null;
}

export function validateContactEmail(value: string | undefined): ContactEmailValidationCode | null {
    const trimmed = value?.trim() ?? '';
    if (!trimmed) {
        return 'required';
    }
    if (!EMAIL_PATTERN.test(trimmed)) {
        return 'invalid';
    }
    return null;
}

export function validatePhone(value: string | undefined): PhoneValidationCode | null {
    const trimmed = value?.trim() ?? '';
    if (!trimmed) {
        return null;
    }
    if (!PHONE_PATTERN.test(trimmed)) {
        return 'invalid';
    }
    return null;
}

export function validateAddress(value: string | undefined): AddressValidationCode | null {
    const trimmed = value?.trim() ?? '';
    if (!trimmed) {
        return null;
    }
    if (trimmed.length > ADDRESS_MAX) {
        return 'tooLong';
    }
    return null;
}

/** Optional field: success when user entered a valid non-empty value. */
export function isOptionalFieldValid(value: string | undefined, validator: (v: string | undefined) => string | null): boolean {
    const trimmed = value?.trim() ?? '';
    if (!trimmed) {
        return false;
    }
    return validator(value) === null;
}
