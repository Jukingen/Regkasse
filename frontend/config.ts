import { Platform } from 'react-native';

// Platform-aware API URL configuration
const getApiBaseUrl = () => {
  // Önce environment variable kontrol et
  if (process.env.EXPO_PUBLIC_API_URL) {
    console.log('🌐 Using API URL from env:', process.env.EXPO_PUBLIC_API_URL);
    return process.env.EXPO_PUBLIC_API_URL;
  }

  // Platform-based fallback
  if (Platform.OS === 'ios') {
    // iOS Simulator için local machine IP kullan
    const iosApiUrl = 'http://192.168.1.2:5183/api'; // User'ın gerçek IP adresi
    console.log('🍎 iOS platform detected, using:', iosApiUrl);
    return iosApiUrl;
  } else if (Platform.OS === 'android') {
    // Android Emulator için 10.0.2.2 kullan
    const androidApiUrl = 'http://10.0.2.2:5183/api';
    console.log('🤖 Android platform detected, using:', androidApiUrl);
    return androidApiUrl;
  } else {
    // Web için localhost:5183 (backend API port'u)
    const webApiUrl = 'http://localhost:5183/api';
    console.log('🌐 Web platform detected, using:', webApiUrl);
    return webApiUrl;
  }
};

export const API_BASE_URL = getApiBaseUrl();

console.log('🔧 Final API_BASE_URL:', API_BASE_URL);

// Diğer ortam değişkenleri
// export const ANOTHER_CONFIG = process.env.EXPO_PUBLIC_ANOTHER_CONFIG || 'default'; 