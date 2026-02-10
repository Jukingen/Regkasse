// Soft Minimal Design System
// Instagram story style: soft beige background, rounded corners, subtle shadows, brown tones

import { TextStyle, ViewStyle } from 'react-native';

// -------------------------
// COLOR PALETTE
// -------------------------
export const SoftColors = {
    // Backgrounds
    bgPrimary: '#FAF8F5',       // Warm cream
    bgSecondary: '#F5F0EB',     // Soft beige
    bgCard: '#FFFFFF',          // Pure white
    bgAccent: '#FFF9F5',        // Very light peach

    // Text
    textPrimary: '#3D3229',     // Dark brown
    textSecondary: '#7A6F64',   // Medium brown
    textMuted: '#A89F96',       // Light brown
    textInverse: '#FFFFFF',     // White

    // Accents
    accent: '#C8A87C',          // Warm gold/caramel
    accentLight: '#E8D5B5',     // Light caramel
    accentDark: '#8B7355',      // Dark mocha

    // Semantic
    success: '#7DB87D',         // Soft green
    successBg: '#F0F7F0',
    error: '#D4847C',           // Muted coral
    errorBg: '#FDF5F4',
    warning: '#E5B97D',         // Soft amber
    warningBg: '#FFF8EE',
    info: '#8BA4C4',            // Muted blue
    infoBg: '#F5F8FB',

    // Border & Divider
    border: '#E8E2DB',          // Warm gray
    borderLight: '#F2EDE7',
    divider: 'rgba(61, 50, 41, 0.06)',

    // Overlay
    overlay: 'rgba(61, 50, 41, 0.4)',
};

// -------------------------
// SPACING
// -------------------------
export const SoftSpacing = {
    xs: 4,
    sm: 8,
    md: 12,
    lg: 16,
    xl: 24,
    xxl: 32,
};

// -------------------------
// BORDER RADIUS
// -------------------------
export const SoftRadius = {
    sm: 8,
    md: 12,
    lg: 16,
    xl: 20,
    xxl: 28,
    full: 9999,
};

// -------------------------
// SHADOWS (soft & subtle)
// -------------------------
export const SoftShadows = {
    sm: {
        shadowColor: '#3D3229',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.04,
        shadowRadius: 8,
        elevation: 2,
    } as ViewStyle,
    md: {
        shadowColor: '#3D3229',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.06,
        shadowRadius: 12,
        elevation: 4,
    } as ViewStyle,
    lg: {
        shadowColor: '#3D3229',
        shadowOffset: { width: 0, height: 8 },
        shadowOpacity: 0.08,
        shadowRadius: 24,
        elevation: 8,
    } as ViewStyle,
};

// -------------------------
// TYPOGRAPHY
// -------------------------
export const SoftTypography = {
    // Headers
    h1: { fontSize: 28, fontWeight: '700' as const, letterSpacing: -0.5 } as TextStyle,
    h2: { fontSize: 22, fontWeight: '600' as const, letterSpacing: -0.3 } as TextStyle,
    h3: { fontSize: 18, fontWeight: '600' as const, letterSpacing: 0 } as TextStyle,

    // Body
    body: { fontSize: 15, fontWeight: '400' as const, lineHeight: 22 } as TextStyle,
    bodySmall: { fontSize: 13, fontWeight: '400' as const, lineHeight: 18 } as TextStyle,

    // Labels
    label: { fontSize: 13, fontWeight: '500' as const, letterSpacing: 0.5 } as TextStyle,
    caption: { fontSize: 11, fontWeight: '400' as const, letterSpacing: 0.2 } as TextStyle,

    // Price
    price: { fontSize: 16, fontWeight: '700' as const } as TextStyle,
    priceSmall: { fontSize: 14, fontWeight: '600' as const } as TextStyle,
};
