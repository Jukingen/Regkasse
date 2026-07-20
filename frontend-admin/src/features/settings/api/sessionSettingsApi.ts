import { customInstance } from '@/lib/axios';

export type SessionSettings = {
    timeoutMinutes: number;
    warningMinutes: number;
    enabled: boolean;
};

export type UpdateSessionSettingsPayload = SessionSettings;

type SessionSettingsApiDto = {
    timeoutMinutes?: number;
    warningMinutes?: number;
    enabled?: boolean;
    TimeoutMinutes?: number;
    WarningMinutes?: number;
    Enabled?: boolean;
};

function mapFromApi(dto: SessionSettingsApiDto): SessionSettings {
    return {
        timeoutMinutes: dto.timeoutMinutes ?? dto.TimeoutMinutes ?? 30,
        warningMinutes: dto.warningMinutes ?? dto.WarningMinutes ?? 5,
        enabled: dto.enabled ?? dto.Enabled ?? true,
    };
}

export async function fetchSessionSettings(): Promise<SessionSettings> {
    const res = await customInstance<SessionSettingsApiDto>({
        url: '/api/settings/session',
        method: 'GET',
    });
    return mapFromApi(res);
}

export async function updateSessionSettings(payload: UpdateSessionSettingsPayload): Promise<SessionSettings> {
    const res = await customInstance<SessionSettingsApiDto>({
        url: '/api/settings/session',
        method: 'PUT',
        data: payload,
    });
    return mapFromApi(res);
}
