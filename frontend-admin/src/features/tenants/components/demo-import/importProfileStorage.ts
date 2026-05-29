import type { DemoImportProfile } from '@/features/tenants/components/demo-import/importProfiles';

const STORAGE_KEY = 'regkasse.demoImportProfiles.v1';
const MAX_PROFILES_PER_SCOPE = 30;

type ProfileStore = Record<string, DemoImportProfile[]>;

function scopeKey(tenantId?: string): string {
    return tenantId?.trim() || '_default';
}

function readStore(): ProfileStore {
    if (typeof window === 'undefined') return {};
    try {
        const raw = window.localStorage.getItem(STORAGE_KEY);
        if (!raw) return {};
        const parsed = JSON.parse(raw) as ProfileStore;
        return parsed && typeof parsed === 'object' ? parsed : {};
    } catch {
        return {};
    }
}

function writeStore(store: ProfileStore): void {
    if (typeof window === 'undefined') return;
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(store));
}

export function listSavedDemoImportProfiles(tenantId?: string): DemoImportProfile[] {
    const store = readStore();
    return store[scopeKey(tenantId)] ?? [];
}

export function saveDemoImportProfile(profile: DemoImportProfile, tenantId?: string): DemoImportProfile[] {
    const key = scopeKey(tenantId);
    const store = readStore();
    const existing = store[key] ?? [];

    const withoutDuplicate = existing.filter(
        (p) => p.id !== profile.id && p.name.toLowerCase() !== profile.name.toLowerCase(),
    );
    const next = [profile, ...withoutDuplicate].slice(0, MAX_PROFILES_PER_SCOPE);
    store[key] = next;
    writeStore(store);
    return next;
}

export function deleteDemoImportProfile(profileId: string, tenantId?: string): DemoImportProfile[] {
    const key = scopeKey(tenantId);
    const store = readStore();
    const existing = store[key] ?? [];
    const next = existing.filter((p) => p.id !== profileId);
    store[key] = next;
    writeStore(store);
    return next;
}
