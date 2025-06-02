import { apiClient } from './config';

export interface UserSettings {
    id: string;
    userId: string;
    language: string;
    theme: 'light' | 'dark' | 'system';
    notifications: boolean;
    printerSettings: {
        enabled: boolean;
        model: string;
        paperSize: string;
    };
    receiptSettings: {
        showLogo: boolean;
        showTaxDetails: boolean;
        footerText: string;
    };
}

export interface UpdateSettingsRequest {
    language?: string;
    theme?: 'light' | 'dark' | 'system';
    notifications?: boolean;
    printerSettings?: Partial<UserSettings['printerSettings']>;
    receiptSettings?: Partial<UserSettings['receiptSettings']>;
}

export const settingsService = {
    getUserSettings: async (): Promise<UserSettings> => {
        return apiClient.get<UserSettings>('/settings/user');
    },

    updateUserSettings: async (settings: UpdateSettingsRequest): Promise<UserSettings> => {
        return apiClient.put<UserSettings>('/settings/user', settings);
    },

    resetUserSettings: async (): Promise<UserSettings> => {
        return apiClient.post<UserSettings>('/settings/user/reset', {});
    }
}; 