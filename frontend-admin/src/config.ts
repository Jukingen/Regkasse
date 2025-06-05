// API Configuration
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

// Application Configuration
export const APP_CONFIG = {
  name: 'Registrierkasse Admin',
  version: '1.0.0',
  defaultLanguage: 'de',
  supportedLanguages: ['de', 'en', 'tr'],
  dateFormat: 'DD.MM.YYYY',
  timeFormat: 'HH:mm:ss',
  currency: 'EUR',
  taxRates: {
    standard: 20,
    reduced: 10,
    special: 13
  },
  pagination: {
    defaultPageSize: 10,
    pageSizeOptions: [10, 25, 50, 100]
  },
  sessionTimeout: 30 * 60 * 1000, // 30 minutes
  refreshTokenTimeout: 7 * 24 * 60 * 60 * 1000 // 7 days
};

// TSE Configuration
export const TSE_CONFIG = {
  model: 'Epson-TSE',
  timeout: 30000, // 30 seconds
  dailyClosureTime: '23:59:59',
  requiredFields: [
    'BelegDatum',
    'Uhrzeit',
    'TSE-Signatur',
    'Kassen-ID'
  ]
};

// Printer Configuration
export const PRINTER_CONFIG = {
  supportedModels: [
    'EPSON TM-T88VI',
    'Star TSP 700'
  ],
  requiredFont: 'OCRA-B',
  paperWidth: 80,
  paperHeight: 297
};

// Security Configuration
export const SECURITY_CONFIG = {
  minPasswordLength: 8,
  requireSpecialChars: true,
  requireNumbers: true,
  requireUppercase: true,
  requireLowercase: true,
  maxLoginAttempts: 5,
  lockoutDuration: 15 * 60 * 1000 // 15 minutes
};

// Export all configurations
export default {
  API_BASE_URL,
  APP_CONFIG,
  TSE_CONFIG,
  PRINTER_CONFIG,
  SECURITY_CONFIG
}; 