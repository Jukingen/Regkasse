/**
 * Below are the colors that are used in the app. The colors are defined in the light and dark mode.
 * There are many other ways to style your app. For example, [Nativewind](https://www.nativewind.dev/), [Tamagui](https://tamagui.dev/), [unistyles](https://reactnativeunistyles.vercel.app), etc.
 */

const tintColorLight = '#0a7ea4';
const tintColorDark = '#fff';

export const Colors = {
  light: {
    text: '#11181C',
    background: '#fff',
    tint: tintColorLight,
    icon: '#687076',
    tabIconDefault: '#687076',
    tabIconSelected: tintColorLight,
  },
  dark: {
    text: '#ECEDEE',
    background: '#151718',
    tint: tintColorDark,
    icon: '#9BA1A6',
    tabIconDefault: '#9BA1A6',
    tabIconSelected: tintColorDark,
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
