import { Platform } from 'react-native';

const isDev = __DEV__;

// Platform-aware API URL configuration
const getApiBaseUrl = () => {
  // 1. Priority: Check EXPO_PUBLIC_API_BASE_URL (Preferred)
  if (process.env.EXPO_PUBLIC_API_BASE_URL) {
    if (isDev) {
      console.log('🌐 Using API URL from EXPO_PUBLIC_API_BASE_URL:', process.env.EXPO_PUBLIC_API_BASE_URL);
    }
    return process.env.EXPO_PUBLIC_API_BASE_URL;
  }

  // 2. Check legacy env var
  if (process.env.EXPO_PUBLIC_API_URL) {
    if (isDev) {
      console.log('🌐 Using API URL from EXPO_PUBLIC_API_URL:', process.env.EXPO_PUBLIC_API_URL);
    }
    return process.env.EXPO_PUBLIC_API_URL;
  }

  if (!isDev) {
    throw new Error('EXPO_PUBLIC_API_BASE_URL must be configured for production builds.');
  }

  // 3. Platform-specific Fallback
  if (Platform.OS === 'web') {
    // Web: localhost works fine
    const backendApiUrl = 'http://localhost:5183/api';
    console.log('🔧 Using Web backend API URL:', backendApiUrl);
    return backendApiUrl;
  } else {
    // Native (iOS/Android): localhost does NOT work on physical devices or some emulators
    // Fallback to a development machine LAN IP.
    // TODO: Update this IP to your computer's local IP address (e.g., 192.168.1.X)
    // or start Expo with: EXPO_PUBLIC_API_BASE_URL=http://YOUR_IP:5183/api npx expo start
    const devMachineIp = '192.168.1.2'; // Example fallback
    const backendApiUrl = `http://${devMachineIp}:5183/api`;
    console.log('📱 Using Native Dev API URL (Fallback):', backendApiUrl);
    return backendApiUrl;
  }
};

export const API_BASE_URL = getApiBaseUrl();

if (isDev) {
  console.log('🔧 Final API_BASE_URL:', API_BASE_URL);
}

// Diğer ortam değişkenleri
// export const ANOTHER_CONFIG = process.env.EXPO_PUBLIC_ANOTHER_CONFIG || 'default'; 