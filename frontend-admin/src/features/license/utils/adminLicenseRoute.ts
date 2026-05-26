export const ADMIN_LICENSE_PAGE_PATH = '/admin/license';
export const ADMIN_LICENSE_PAGE_INTENT_EXTEND = 'extend';

const MACHINE_HASH_HEX_REGEX = /^[0-9a-f]{64}$/i;

type SearchParamsReader = {
    get(name: string): string | null;
};

export type AdminLicensePageIntent = typeof ADMIN_LICENSE_PAGE_INTENT_EXTEND;

export type AdminLicensePagePrefill = {
    intent?: AdminLicensePageIntent;
    machineHashHex?: string;
    requireFingerprint: boolean;
};

export function buildAdminLicensePageHref(args?: {
    machineHash?: string | null;
    intent?: AdminLicensePageIntent;
}): string {
    const params = new globalThis.URLSearchParams();
    const intent = args?.intent ?? ADMIN_LICENSE_PAGE_INTENT_EXTEND;
    params.set('intent', intent);

    const machineHash = args?.machineHash?.trim();
    if (machineHash) {
        params.set('machineHash', machineHash);
    }

    const search = params.toString();
    return search ? `${ADMIN_LICENSE_PAGE_PATH}?${search}` : ADMIN_LICENSE_PAGE_PATH;
}

export function readAdminLicensePagePrefill(
    searchParams: SearchParamsReader,
): AdminLicensePagePrefill {
    const rawIntent = searchParams.get('intent')?.trim().toLowerCase();
    const intent = rawIntent === ADMIN_LICENSE_PAGE_INTENT_EXTEND ? ADMIN_LICENSE_PAGE_INTENT_EXTEND : undefined;

    const rawMachineHash = searchParams.get('machineHash')?.trim().toLowerCase();
    const machineHashHex =
        rawMachineHash && MACHINE_HASH_HEX_REGEX.test(rawMachineHash) ? rawMachineHash : undefined;

    return {
        intent,
        machineHashHex,
        requireFingerprint: Boolean(machineHashHex),
    };
}
