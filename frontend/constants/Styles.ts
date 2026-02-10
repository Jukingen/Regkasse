import { StyleSheet, Dimensions } from 'react-native';

import { Theme } from './Colors';

const { width, height } = Dimensions.get('window');

export const createStyles = (theme: Theme) => StyleSheet.create({
  // Layout
  container: {
    flex: 1,
    backgroundColor: theme.background,
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  spaceBetween: {
    justifyContent: 'space-between',
  },

  // Typography
  h1: {
    fontSize: 18,
    fontWeight: 'bold',
    color: theme.text,
  },
  h2: {
    fontSize: 16,
    fontWeight: 'bold',
    color: theme.text,
  },
  h3: {
    fontSize: 14,
    fontWeight: '600',
    color: theme.text,
  },
  body: {
    fontSize: 13,
    color: theme.text,
  },
  caption: {
    fontSize: 12,
    color: theme.placeholder,
  },

  // Input
  input: {
    height: 32,
    backgroundColor: theme.inputBackground,
    borderRadius: 4,
    paddingHorizontal: 8,
    color: theme.text,
    fontSize: 13,
    borderWidth: 1,
    borderColor: theme.border,
  },
  inputError: {
    borderColor: theme.error,
  },

  // Button
  button: {
    height: 32,
    borderRadius: 4,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: theme.primary,
  },
  buttonText: {
    color: theme.buttonText,
    fontSize: 13,
    fontWeight: '600',
  },
  buttonDisabled: {
    opacity: 0.7,
  },

  // Card
  card: {
    backgroundColor: theme.card,
    borderRadius: 8,
    padding: 8,
    marginVertical: 4,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 2,
    elevation: 2,
  },

  // List
  listItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 8,
    borderBottomWidth: 1,
    borderBottomColor: theme.border,
  },

  // Spacing
  spacing: {
    xs: 2,
    sm: 4,
    md: 8,
    lg: 12,
    xl: 16,
  },

  // Dimensions
  screenWidth: width,
  screenHeight: height,
});

// Özel stil yardımcıları
export const spacing = {
  xs: 2,
  sm: 4,
  md: 8,
  lg: 12,
  xl: 16,
};

export const borderRadius = {
  sm: 4,
  md: 8,
  lg: 12,
  xl: 16,
  round: 9999,
};

export const shadows = {
  sm: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.1,
    shadowRadius: 2,
    elevation: 2,
  },
  md: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.15,
    shadowRadius: 4,
    elevation: 4,
  },
  lg: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.2,
    shadowRadius: 8,
    elevation: 8,
  },
}; 