import AsyncStorage from '@react-native-async-storage/async-storage';

import { apiClient } from './config';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresIn?: number;
  refreshToken?: string; // Opsiyonel, backend'den gelirse kullan
  user: {
    id: string;
    username?: string;
    email: string;
    role: string;
    firstName?: string;
    lastName?: string;
    roles?: string[];
  };
}

export interface User {
  id: string;
  username?: string;
  email: string;
  role: string;
  firstName?: string;
  lastName?: string;
  roles?: string[];
}

// Login işlemi
export const login = async (credentials: LoginRequest): Promise<LoginResponse> => {
  try {
    const response = await apiClient.post<LoginResponse>('/auth/login', credentials);
    
    // Token'ı kaydet
    await AsyncStorage.setItem('token', response.token);
    await AsyncStorage.setItem('user', JSON.stringify(response.user));
    
    // Eğer refreshToken varsa onu da kaydet
    if (response.refreshToken) {
      await AsyncStorage.setItem('refreshToken', response.refreshToken);
    }
    
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
    await AsyncStorage.removeItem('user');
    // refreshToken varsa onu da temizle
    await AsyncStorage.removeItem('refreshToken');
  } catch (error) {
    console.error('Logout error:', error);
  }
};

// Mevcut kullanıcıyı getir
export const getCurrentUser = async (): Promise<User | null> => {
  try {
    const userStr = await AsyncStorage.getItem('user');
    if (!userStr) {
      console.log('No user data found in storage');
      return null;
    }
    
    const user = JSON.parse(userStr);
    console.log('Retrieved user from storage:', user);
    return user;
  } catch (error) {
    console.error('Get current user error:', error);
    return null;
  }
};

// Token'ı yenile
export const refreshToken = async (): Promise<string | null> => {
  try {
    const refreshTokenStr = await AsyncStorage.getItem('refreshToken');
    if (!refreshTokenStr) {
      console.log('No refresh token available');
      return null;
    }

    const response = await apiClient.post<{ token: string }>('/auth/refresh', {
      refreshToken: refreshTokenStr
    });

    if (response && response.token) {
      await AsyncStorage.setItem('token', response.token);
      return response.token;
    } else {
      console.error('Invalid refresh response:', response);
      return null;
    }
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