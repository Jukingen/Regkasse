import { apiClient } from './config';

export interface LoginRequest {
    email: string;
    password: string;
    rememberMe?: boolean;
}

export interface LoginResponse {
    token: string;
    refreshToken: string;
    user: {
        id: string;
        email: string;
        firstName: string;
        lastName: string;
        role: string;
        employeeNumber: string;
        sessionId: string;
    };
    message: string;
}

export interface User {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    role: string;
    employeeNumber: string;
}

export interface RefreshTokenRequest {
    token: string;
}

export const authService = {
    login: async (credentials: LoginRequest): Promise<LoginResponse> => {
        return await apiClient.post<LoginResponse>('/auth/login', credentials);
    },

    logout: async (): Promise<void> => {
        await apiClient.post('/auth/logout', {});
    },

    refreshToken: async (refreshToken: string): Promise<LoginResponse> => {
        return await apiClient.post<LoginResponse>('/auth/refresh', { token: refreshToken });
    },

    getCurrentUser: async (): Promise<User> => {
        return await apiClient.get<User>('/auth/me');
    }
}; 