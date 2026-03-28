/**
 * Load raw `de` locale JSON for parity tests. Node fs only — no app bootstrap, no axios/env.
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HELPERS_DIR = dirname(fileURLToPath(import.meta.url));
/** `src/` directory (helpers → __tests__ → shared → src). */
const SRC_DIR = join(HELPERS_DIR, '..', '..', '..');

export type DeLocaleNamespace = 'common' | 'invoices';

export function loadDeLocaleRoot(namespace: DeLocaleNamespace): Record<string, unknown> {
    const filePath = join(SRC_DIR, 'i18n', 'locales', 'de', `${namespace}.json`);
    const raw = readFileSync(filePath, 'utf-8');
    const parsed = JSON.parse(raw) as unknown;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
        throw new Error(`Expected object root in ${filePath}`);
    }
    return parsed as Record<string, unknown>;
}

/** Dot path like `messages.noTechnicalDetail` from locale file root (no `common.` prefix). */
export function getLocaleStringAtPath(root: Record<string, unknown>, dotPath: string): unknown {
    const parts = dotPath.split('.');
    let cur: unknown = root;
    for (const p of parts) {
        if (cur == null || typeof cur !== 'object' || Array.isArray(cur)) return undefined;
        cur = (cur as Record<string, unknown>)[p];
    }
    return cur;
}
