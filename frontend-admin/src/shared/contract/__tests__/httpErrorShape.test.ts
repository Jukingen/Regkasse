import { describe, expect, it } from 'vitest';
import {
    getAxiosResponseDataString,
    getAxiosResponseStatus,
    isAntdFormValidateError,
} from '@/shared/contract/httpErrorShape';

describe('getAxiosResponseStatus', () => {
    it('returns undefined for non-objects', () => {
        expect(getAxiosResponseStatus('x')).toBeUndefined();
    });

    it('reads numeric status', () => {
        expect(getAxiosResponseStatus({ response: { status: 409 } })).toBe(409);
    });
});

describe('getAxiosResponseDataString', () => {
    it('reads string data', () => {
        expect(getAxiosResponseDataString({ response: { data: 'bad' } })).toBe('bad');
    });

    it('reads message from object data', () => {
        expect(getAxiosResponseDataString({ response: { data: { message: 'nope' } } })).toBe('nope');
    });
});

describe('isAntdFormValidateError', () => {
    it('detects errorFields', () => {
        expect(isAntdFormValidateError({ errorFields: [] })).toBe(true);
    });
});
