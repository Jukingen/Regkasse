/**
 * Below are the colors that are used in the app. The colors are defined in the light and dark mode.
 * There are many other ways to style your app. For example, [Nativewind](https://www.nativewind.dev/), [Tamagui](https://tamagui.dev/), [unistyles](https://reactnativeunistyles.vercel.app), etc.
 */

const tintColorLight = '#0a7ea4';
const tintColorDark = '#fff';

export const Colors = {
  light: {
    // Ana renkler
    primary: '#2196F3',
    secondary: '#FF9800',
    success: '#4CAF50',
    error: '#F44336',
    warning: '#FF9800',
    info: '#2196F3',
    
    // Arka plan renkleri
    background: '#F5F5F5',
    surface: '#FFFFFF',
    card: '#FFFFFF',
    
    // Metin renkleri
    text: '#212121',
    textSecondary: '#757575',
    textTertiary: '#9E9E9E',
    
    // Kenarlık renkleri
    border: '#E0E0E0',
    borderLight: '#F0F0F0',
    
    // Durum renkleri
    online: '#4CAF50',
    offline: '#FF9800',
    
    // Sepet ve ödeme renkleri
    cartBackground: '#F9F9F9',
    paymentButton: '#4CAF50',
    paymentButtonDisabled: '#CCCCCC',
    
    // Kategori renkleri
    categoryFood: '#FF5722',
    categoryDrink: '#2196F3',
    categoryDessert: '#E91E63',
    categoryOther: '#9C27B0',
  },
  dark: {
    // Ana renkler
    primary: '#64B5F6',
    secondary: '#FFB74D',
    success: '#81C784',
    error: '#E57373',
    warning: '#FFB74D',
    info: '#64B5F6',
    
    // Arka plan renkleri
    background: '#121212',
    surface: '#1E1E1E',
    card: '#2D2D2D',
    
    // Metin renkleri
    text: '#FFFFFF',
    textSecondary: '#B0B0B0',
    textTertiary: '#808080',
    
    // Kenarlık renkleri
    border: '#404040',
    borderLight: '#303030',
    
    // Durum renkleri
    online: '#81C784',
    offline: '#FFB74D',
    
    // Sepet ve ödeme renkleri
    cartBackground: '#2A2A2A',
    paymentButton: '#81C784',
    paymentButtonDisabled: '#666666',
    
    // Kategori renkleri
    categoryFood: '#FF8A65',
    categoryDrink: '#64B5F6',
    categoryDessert: '#F48FB1',
    categoryOther: '#BA68C8',
  },
};

// Spacing sistemi
export const Spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
};

// Border radius sistemi
export const BorderRadius = {
  sm: 4,
  md: 8,
  lg: 12,
  xl: 16,
  xxl: 24,
};

// Typography sistemi
export const Typography = {
  h1: {
    fontSize: 32,
    fontWeight: 'bold' as const,
  },
  h2: {
    fontSize: 24,
    fontWeight: '600' as const,
  },
  h3: {
    fontSize: 20,
    fontWeight: '600' as const,
  },
  body: {
    fontSize: 16,
    fontWeight: 'normal' as const,
  },
  bodySmall: {
    fontSize: 14,
    fontWeight: 'normal' as const,
  },
  caption: {
    fontSize: 12,
    fontWeight: 'normal' as const,
  },
  button: {
    fontSize: 16,
    fontWeight: '600' as const,
  },
};

export const lightTheme = {
  primary: '#007AFF',
  secondary: '#5856D6',
  background: '#F5F5F5',
  card: '#FFFFFF',
  text: '#000000',
  border: '#DDDDDD',
  error: '#FF3B30',
  success: '#34C759',
  warning: '#FF9500',
  info: '#5856D6',
  disabled: '#999999',
  placeholder: '#8E8E93',
  // Özel renkler
  headerBackground: '#FFFFFF',
  tabBarBackground: '#FFFFFF',
  inputBackground: '#F0F0F0',
  buttonText: '#FFFFFF',
  priceText: '#007AFF',
  cartBackground: '#FFFFFF',
  searchBackground: '#F0F0F0',
};

export const darkTheme = {
  primary: '#0A84FF',
  secondary: '#5E5CE6',
  background: '#000000',
  card: '#1C1C1E',
  text: '#FFFFFF',
  border: '#38383A',
  error: '#FF453A',
  success: '#32D74B',
  warning: '#FF9F0A',
  info: '#5E5CE6',
  disabled: '#8E8E93',
  placeholder: '#8E8E93',
  // Özel renkler
  headerBackground: '#1C1C1E',
  tabBarBackground: '#1C1C1E',
  inputBackground: '#2C2C2E',
  buttonText: '#FFFFFF',
  priceText: '#0A84FF',
  cartBackground: '#1C1C1E',
  searchBackground: '#2C2C2E',
};

export type Theme = typeof lightTheme;
