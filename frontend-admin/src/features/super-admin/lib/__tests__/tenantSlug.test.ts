import { describe, expect, it } from 'vitest';

import {
    buildDefaultAdminEmail,
    getTenantSlugPreviewSegment,
    normalizeTenantSlugInput,
    sanitizeTenantSlugKeystroke,
    suggestTenantSlugFromName,
    validateTenantSlug,
} from '../tenantSlug';

describe('sanitizeTenantSlugKeystroke', () => {
    it('allows only lowercase letters digits and hyphens', () => {
        expect(sanitizeTenantSlugKeystroke('Cafe_Example!')).toBe('cafe-example');
    });
});

describe('normalizeTenantSlugInput', () => {
    it('extracts slug from full regkasse.at host', () => {
        expect(normalizeTenantSlugInput('test.regkasse.at')).toBe('test');
    });

    it('extracts slug from https URL', () => {
        expect(normalizeTenantSlugInput('https://cafe-example.regkasse.at/login')).toBe('cafe-example');
    });

    it('converts underscores and spaces to hyphens', () => {
        expect(normalizeTenantSlugInput('Cafe Wien')).toBe('cafe-wien');
    });
});

describe('validateTenantSlug', () => {
    it('accepts valid slugs', () => {
        expect(validateTenantSlug('cafe')).toBeNull();
        expect(validateTenantSlug('cafe-example')).toBeNull();
        expect(validateTenantSlug('mein-cafe-123')).toBeNull();
    });

    it('rejects dots and spaces in final slug', () => {
        expect(validateTenantSlug('cafe.shop')).toBe('invalid');
        expect(validateTenantSlug('cafe shop')).toBe('invalid');
    });

    it('rejects reserved slugs', () => {
        expect(validateTenantSlug('admin')).toBe('reserved');
    });

    it('rejects invalid leading hyphen', () => {
        expect(validateTenantSlug('-invalid')).toBe('invalid');
    });

    it('allows in-progress trailing hyphen', () => {
        expect(validateTenantSlug('cafe-', { inProgress: true })).toBeNull();
    });
});

describe('suggestTenantSlugFromName', () => {
    it('transliterates umlauts and hyphenates', () => {
        expect(suggestTenantSlugFromName('Café Müller')).toBe('cafe-mueller');
    });
});

describe('getTenantSlugPreviewSegment', () => {
    it('shows sanitized segment while typing', () => {
        expect(getTenantSlugPreviewSegment('cafe-')).toBe('cafe-');
    });

    it('uses normalized segment when valid', () => {
        expect(getTenantSlugPreviewSegment('test.regkasse.at')).toBe('test');
    });
});

describe('buildDefaultAdminEmail', () => {
    it('builds admin email from slug and domain', () => {
        expect(buildDefaultAdminEmail('cafe-example', 'regkasse.at')).toBe('admin@cafe-example.regkasse.at');
    });
});
