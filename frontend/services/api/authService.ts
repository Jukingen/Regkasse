import AsyncStorage from '@react-native-async-storage/async-storage';

import { apiClient } from './config';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  refreshToken: string;
  user: {
    id: string;
    username: string;
    email: string;
    role: string;
  };
}

export interface User {
  id: string;
  username: string;
  email: string;
  role: string;
}

// Login işlemi
export const login = async (credentials: LoginRequest): Promise<LoginResponse> => {
  try {
    const response = await apiClient.post<LoginResponse>('/auth/login', credentials);
    
    // Token'ları kaydet
    await AsyncStorage.setItem('token', response.token);
    await AsyncStorage.setItem('refreshToken', response.refreshToken);
    await AsyncStorage.setItem('user', JSON.stringify(response.user));
    
    return response;
  } catch (error) {
    console.error('Login error:', error);
    throw new Error('Login başarısız');
  }
};

// Logout işlemi
export const logout = async (): Promise<void> => {
  try {
    await AsyncStorage.removeItem('token');
    await AsyncStorage.removeItem('refreshToken');
    await AsyncStorage.removeItem('user');
  } catch (error) {
    console.error('Logout error:', error);
  }
};

// Mevcut kullanıcıyı getir
export const getCurrentUser = async (): Promise<User | null> => {
  try {
    const userStr = await AsyncStorage.getItem('user');
    return userStr ? JSON.parse(userStr) : null;
  } catch (error) {
    console.error('Get current user error:', error);
    return null;
  }
};

// Token'ı yenile
export const refreshToken = async (): Promise<string | null> => {
  try {
    const refreshTokenStr = await AsyncStorage.getItem('refreshToken');
    if (!refreshTokenStr) return null;

    const response = await apiClient.post<{ token: string }>('/auth/refresh', {
      refreshToken: refreshTokenStr
    });

    await AsyncStorage.setItem('token', response.token);
    return response.token;
  } catch (error) {
    console.error('Refresh token error:', error);
    await logout();
    return null;
  }
};

// Demo kullanıcı ile otomatik login
export const loginWithDemoUser = async (): Promise<LoginResponse> => {
  return await login({
    email: 'cashier@demo.com',  // ✅ Email field'ı kullan
    password: 'Cashier123!'
  });
}; 