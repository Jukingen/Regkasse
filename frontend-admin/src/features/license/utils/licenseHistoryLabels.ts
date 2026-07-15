type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

const EVENT_I18N_KEYS: Record<string, string> = {
    activated: 'license.history.events.activated',
    extended: 'license.history.events.extended',
    renewed: 'license.history.events.renewed',
    issued: 'license.history.events.issued',
    revoked: 'license.history.events.revoked',
    trial: 'license.history.events.trial',
    updated: 'license.history.events.updated',
};

export function getLicenseHistoryEventLabel(eventType: string, t: TranslateFn): string {
    const normalized = eventType.trim().toLowerCase();
    const key = EVENT_I18N_KEYS[normalized];
    if (key) {
        return t(key);
    }
    return eventType;
}

export function getLicenseHistoryEventTagColor(eventType: string): string {
    switch (eventType.trim().toLowerCase()) {
        case 'activated':
        case 'issued':
            return 'green';
        case 'extended':
        case 'renewed':
            return 'blue';
        case 'trial':
            return 'gold';
        case 'revoked':
            return 'red';
        case 'updated':
            return 'purple';
        default:
            return 'default';
    }
}
