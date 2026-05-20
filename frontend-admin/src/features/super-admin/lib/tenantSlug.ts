/**
 * Tenant slug normalization and validation (UI: lowercase, digits, hyphens only).
 */
/** Final slug: segments of a-z0-9 separated by single hyphens (no leading/trailing hyphen). */
export const TENANT_SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/** @deprecated Use TENANT_SLUG_PATTERN */
export const TENANT_SLUG_REGEX = TENANT_SLUG_PATTERN;

/** While typing (allows trailing hyphen before next segment). */
export const TENANT_SLUG_IN_PROGRESS_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]*)?$/;

export const TENANT_SLUG_MAX_LENGTH = 63;

const RESERVED_SLUGS = new Set(['admin', 'www', 'api', 'mail']);

export type TenantSlugValidationCode = 'required' | 'invalid' | 'reserved' | 'taken' | 'checking';

/**
 * Restricts live input to lowercase letters, digits, and hyphens.
 */
export function sanitizeTenantSlugKeystroke(raw: string): string {
    return raw.toLowerCase().replace(/_/g, '-').replace(/[^a-z0-9-]/g, '');
}

/**
 * Strips URLs / regkasse host suffixes when user pastes a full domain.
 */
export function normalizeTenantSlugInput(raw: string): string {
    let value = raw.trim().toLowerCase();
    if (!value) {
        return '';
    }

    value = value.replace(/^https?:\/\//, '');
    value = value.split(/[/?#]/)[0] ?? value;
    value = value.split(':')[0] ?? value;

    if (value.includes('@')) {
        value = value.split('@').pop() ?? value;
    }

    const labels = value.split('.').filter(Boolean);
    const regkasseIndex = labels.findIndex((l) => l === 'regkasse');
    if (regkasseIndex > 0) {
        value = labels.slice(0, regkasseIndex).join('-');
    } else if (labels.length > 1 && (labels.includes('at') || labels.includes('local'))) {
        value = labels[0] ?? value;
    }

    return sanitizeTenantSlugKeystroke(value.replace(/_/g, '-').replace(/\s+/g, '-'))
        .replace(/-+/g, '-')
        .replace(/^-+|-+$/g, '');
}

/** Preview label for live URL display (falls back to sanitized keystroke). */
export function getTenantSlugPreviewSegment(raw: string | undefined): string {
    const sanitized = sanitizeTenantSlugKeystroke(raw ?? '');
    if (!sanitized) {
        return '';
    }
    if (sanitized.endsWith('-')) {
        return sanitized;
    }
    const normalized = normalizeTenantSlugInput(raw ?? '');
    if (normalized && TENANT_SLUG_PATTERN.test(normalized)) {
        return normalized;
    }
    return sanitized;
}

export function validateTenantSlug(raw: string, options?: { inProgress?: boolean }): TenantSlugValidationCode | null {
    const trimmed = raw?.trim() ?? '';
    const sanitized = sanitizeTenantSlugKeystroke(raw);
    const slug = options?.inProgress ? sanitized : normalizeTenantSlugInput(raw);

    if (!slug) {
        return 'required';
    }

    if (!options?.inProgress) {
        if (/\s/.test(trimmed)) {
            return 'invalid';
        }
        const isDomainPaste = /regkasse\.(at|local)/i.test(trimmed);
        if (/[.]/.test(trimmed) && !isDomainPaste) {
            return 'invalid';
        }
    }

    if (!options?.inProgress && (sanitized.startsWith('-') || sanitized.endsWith('-'))) {
        return 'invalid';
    }

    if (RESERVED_SLUGS.has(slug)) {
        return 'reserved';
    }

    if (slug.length > TENANT_SLUG_MAX_LENGTH) {
        return 'invalid';
    }

    const pattern = options?.inProgress ? TENANT_SLUG_IN_PROGRESS_PATTERN : TENANT_SLUG_PATTERN;
    if (!pattern.test(slug)) {
        return 'invalid';
    }
    return null;
}

/** Suggest a slug from company name (German umlauts → ASCII, hyphens only). */
export function suggestTenantSlugFromName(name: string): string {
    const ascii = name
        .trim()
        .toLowerCase()
        .replace(/ä/g, 'ae')
        .replace(/ö/g, 'oe')
        .replace(/ü/g, 'ue')
        .replace(/ß/g, 'ss')
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^-+|-+$/g, '');

    return normalizeTenantSlugInput(ascii);
}

export function buildDefaultAdminEmail(slug: string, baseDomain: string): string {
    const normalized = normalizeTenantSlugInput(slug);
    return normalized ? `admin@${normalized}.${baseDomain}` : '';
}
