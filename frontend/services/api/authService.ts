import { apiClient } from './config';

export interface LoginRequest {
    username: string;
    password: string;
}

export interface LoginResponse {
    token: string;
    refreshToken: string;
    expiresIn: number;
}

export interface User {
    id: string;
    username: string;
    email: string;
    role: 'admin' | 'cashier' | 'manager';
}

export const authService = {
    login: async (credentials: LoginRequest): Promise<LoginResponse> => {
        return await apiClient.post<LoginResponse>('/auth/login', credentials);
    },

    logout: async (): Promise<void> => {
        await apiClient.post('/auth/logout', {});
    },

    refreshToken: async (): Promise<LoginResponse> => {
        return await apiClient.post<LoginResponse>('/auth/refresh-token', {});
    },

    getCurrentUser: async (): Promise<User> => {
        return await apiClient.get<User>('/auth/me');
    }
}; 