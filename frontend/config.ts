import { Platform } from 'react-native';

// Platform-aware API URL configuration
const getApiBaseUrl = () => {
  // Önce environment variable kontrol et
  if (process.env.EXPO_PUBLIC_API_URL) {
    console.log('🌐 Using API URL from env:', process.env.EXPO_PUBLIC_API_URL);
    return process.env.EXPO_PUBLIC_API_URL;
  }

  // Tüm platformlar için backend port'unu kullan (5183)
  // Frontend 8081'de çalışıyor, backend 5183'te
  const backendApiUrl = 'http://localhost:5183/api';
  console.log('🔧 Using backend API URL:', backendApiUrl);
  return backendApiUrl;
};

export const API_BASE_URL = getApiBaseUrl();

console.log('🔧 Final API_BASE_URL:', API_BASE_URL);

// Diğer ortam değişkenleri
// export const ANOTHER_CONFIG = process.env.EXPO_PUBLIC_ANOTHER_CONFIG || 'default'; 