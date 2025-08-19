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

// 🔐 BACKEND TOKEN VALIDATION - F5 refresh'te backend'den kullanıcı durumunu kontrol eder
export const validateToken = async (): Promise<User | null> => {
  // 🚀 F5 REFRESH FIX: Debouncing için static flag
  if ((validateToken as any).isValidating) {
    console.log('🚫 validateToken zaten çalışıyor, atlanıyor...');
    return null;
  }
  
  // 🚀 F5 REFRESH FIX: Debouncing için timestamp kontrol
  const currentTime = Date.now();
  const lastCallTime = (validateToken as any).lastCallTime || 0;
  const DEBOUNCE_MS = 1000; // 1 saniye debounce
  
  if (currentTime - lastCallTime < DEBOUNCE_MS) {
    console.log(`🚫 validateToken debouncing: ${currentTime - lastCallTime}ms < ${DEBOUNCE_MS}ms`);
    return null;
  }
  
  // Flag'leri set et
  (validateToken as any).isValidating = true;
  (validateToken as any).lastCallTime = currentTime;
  
  try {
    console.log('🔐 Backend token validation başlatılıyor...');
    
    // Token'ı AsyncStorage'dan al
    const token = await AsyncStorage.getItem('token');
    if (!token) {
      console.log('❌ Token bulunamadı, validation atlanıyor');
      return null;
    }

    console.log('🔑 Token bulundu, backend\'e gönderiliyor...');

    // Backend'den kullanıcı bilgisini al
    const response = await apiClient.get<User>('/auth/me');
    
    if (response && response.id) {
      console.log('✅ Backend token validation başarılı:', response.email);
      
      // AsyncStorage'daki user bilgisini güncelle
      await AsyncStorage.setItem('user', JSON.stringify(response));
      
      return response;
    } else {
      console.log('❌ Backend token validation başarısız: invalid response');
      return null;
    }
  } catch (error: any) {
    console.error('❌ Backend token validation hatası:', error);
    
    // Token geçersizse storage'ı temizle
    if (error.status === 401) {
      console.log('🚨 Token geçersiz (401), storage temizleniyor...');
      await AsyncStorage.multiRemove(['token', 'refreshToken', 'user']);
    } else if (error.status === 500) {
      console.log('🚨 Backend hatası (500), storage temizleniyor...');
      await AsyncStorage.multiRemove(['token', 'refreshToken', 'user']);
    }
    
    return null;
  } finally {
    // Flag'i temizle
    (validateToken as any).isValidating = false;
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