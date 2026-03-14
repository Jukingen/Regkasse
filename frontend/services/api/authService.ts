import { storage } from '../../utils/storage';

import { apiClient } from './config';
import { normalizeLoginError } from '../../features/auth/authErrors';

export interface LoginRequest {
  email: string;
  password: string;
  /** Future-proof: backend policy (e.g. strict mode) may use this; POS sends 'pos'. */
  clientApp?: 'pos' | 'admin';
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
    permissions?: string[];
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
  /** Backend permission claims (resource.action). Aligned with backend RolePermissionMatrix. */
  permissions?: string[];
}

// Login işlemi
export const login = async (credentials: LoginRequest): Promise<LoginResponse> => {
  try {
    const response = await apiClient.post<LoginResponse>('/auth/login', credentials);

    // Token storage'ı AuthContext'te yapılıyor, burada yapmıyoruz
    // await storage.setItem('token', response.token);
    // await storage.setItem('user', JSON.stringify(response.user));

    // Eğer refreshToken varsa onu da kaydet
    if (response.refreshToken) {
      await storage.setItem('refreshToken', response.refreshToken);
    }

    return response;
  } catch (error: unknown) {
    console.error('Login error:', error);
    // Propagate original error (response/data/message preserved via normalizeLoginError); no generic overwrite.
    throw normalizeLoginError(error);
  }
};

export const logout = async (): Promise<void> => {
  try {
    await apiClient.post('/auth/logout');
  } catch (error) {
    console.warn('Backend logout call failed (non-critical):', error);
  } finally {
    await storage.removeItem('token');
    await storage.removeItem('user');
    await storage.removeItem('refreshToken');
  }
};

// Mevcut kullanıcıyı getir
export const getCurrentUser = async (): Promise<User | null> => {
  try {
    const userStr = await storage.getItem('user');
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
    const token = await storage.getItem('token');
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
      await storage.setItem('user', JSON.stringify(response));

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
      await storage.multiRemove(['token', 'refreshToken', 'user']);
    } else if (error.status === 500) {
      console.log('🚨 Backend hatası (500), storage temizleniyor...');
      await storage.multiRemove(['token', 'refreshToken', 'user']);
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
    const refreshTokenStr = await storage.getItem('refreshToken');
    if (!refreshTokenStr) {
      console.log('No refresh token available');
      return null;
    }

    const response = await apiClient.post<{ token: string }>('/auth/refresh', {
      refreshToken: refreshTokenStr
    });

    if (response && response.token) {
      await storage.setItem('token', response.token);
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
    email: 'cashier@demo.com',
    password: 'Cashier123!',
    clientApp: 'pos'
  });
}; 