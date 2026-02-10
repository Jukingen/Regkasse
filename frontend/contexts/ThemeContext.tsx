import { storage } from '../utils/storage';
import React, { createContext, useContext, useState, useEffect } from 'react';
import { useColorScheme } from 'react-native';

import { lightTheme, darkTheme, Theme } from '../constants/Colors';

type ThemeMode = 'light' | 'dark' | 'system';

interface ThemeContextType {
  theme: Theme;
  themeMode: ThemeMode;
  setThemeMode: (mode: ThemeMode) => void;
  isDark: boolean;
}

const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

export const ThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const systemColorScheme = useColorScheme();
  const [themeMode, setThemeMode] = useState<ThemeMode>('system');

  useEffect(() => {
    // Kaydedilmiş tema tercihini yükle
    const loadThemePreference = async () => {
      try {
        const savedThemeMode = await storage.getItem('themeMode');
        if (savedThemeMode) {
          setThemeMode(savedThemeMode as ThemeMode);
        }
      } catch (error) {
        console.error('Tema tercihi yüklenirken hata:', error);
      }
    };

    loadThemePreference();
  }, []);

  const handleThemeModeChange = async (mode: ThemeMode) => {
    try {
      await storage.setItem('themeMode', mode);
      setThemeMode(mode);
    } catch (error) {
      console.error('Tema tercihi kaydedilirken hata:', error);
    }
  };

  const isDark = themeMode === 'system'
    ? systemColorScheme === 'dark'
    : themeMode === 'dark';

  const theme = isDark ? darkTheme : lightTheme;

  return (
    <ThemeContext.Provider
      value={{
        theme,
        themeMode,
        setThemeMode: handleThemeModeChange,
        isDark,
      }}
    >
      {children}
    </ThemeContext.Provider>
  );
};

export const useTheme = () => {
  const context = useContext(ThemeContext);
  if (context === undefined) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
}; 