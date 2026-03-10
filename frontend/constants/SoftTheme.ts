// Soft Minimal Design System – POS mini design system
// Single source for spacing (8px base), typography, semantic colors, radius, shadow, state.

import { TextStyle, ViewStyle } from 'react-native';

// -------------------------
// MOTION (duration for animations – ease-out feel, no excessive animation)
// -------------------------
export const SoftMotion = {
    /** Micro feedback: 150–220ms range for snappy feel */
    micro: 180,
    /** Quick feedback: press, toggle */
    fast: 200,
    /** In/out: toast, modal */
    normal: 300,
} as const;

// -------------------------
// ELEVATION (align with SoftShadows)
// -------------------------
export const SoftElevation = {
    flat: 0,
    raised: 1,
    card: 3,
    overlay: 6,
} as const;

// -------------------------
// STATE (consistent pressed/disabled/focus behaviour)
// -------------------------
export const SoftState = {
    /** Pressable default – no change */
    default: {} as ViewStyle,
    /** Pressed: subtle feedback */
    pressed: { opacity: 0.92 } as ViewStyle,
    /** Pressed with scale (buttons, chips) */
    pressedScale: { opacity: 0.92, transform: [{ scale: 0.99 }] } as ViewStyle,
    /** Disabled */
    disabled: { opacity: 0.5 } as ViewStyle,
    /** Selected – use component semantic (e.g. accent bg) */
    selected: {} as ViewStyle,
    /** Focus-visible: keyboard/screen reader focus ring (WCAG 2.4.7) */
    focusVisible: { borderWidth: 2, borderColor: '#0A7EA4' } as ViewStyle,
};

// -------------------------
// COLOR PALETTE
// -------------------------
export const SoftColors = {
    // Backgrounds
    bgPrimary: '#FAF8F5',       // Warm cream
    bgSecondary: '#F5F0EB',     // Soft beige
    bgCard: '#FFFFFF',          // Pure white
    bgAccent: '#FFF9F5',        // Very light peach

    // Text (WCAG AA–friendly contrast on bgCard/bgPrimary)
    textPrimary: '#3D3229',     // Dark brown
    textSecondary: '#5C534A',   // Medium brown – improved contrast
    textMuted: '#8A8178',       // Light brown – readable on cards
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

    // Focus (visible focus ring for keyboard/screen reader)
    focusRing: '#0A7EA4',

    // Border & Divider
    border: '#E8E2DB',          // Warm gray
    borderLight: '#F2EDE7',
    divider: 'rgba(61, 50, 41, 0.06)',

    // Overlay
    overlay: 'rgba(61, 50, 41, 0.4)',
};

// -------------------------
// SPACING (8px base – use multiples for consistency)
// -------------------------
export const SoftSpacing = {
    xs: 4,
    sm: 8,
    md: 16,
    lg: 24,
    xl: 32,
    xxl: 40,
};
/** 8px-based scale for layout (1=8px, 2=16px, …). Prefer for new UI. */
export const Space8 = {
    1: 8,
    2: 16,
    3: 24,
    4: 32,
    5: 40,
    6: 48,
} as const;

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
// SHADOWS (refined, low clutter)
// -------------------------
export const SoftShadows = {
    sm: {
        shadowColor: '#3D3229',
        shadowOffset: { width: 0, height: 1 },
        shadowOpacity: 0.03,
        shadowRadius: 4,
        elevation: 1,
    } as ViewStyle,
    md: {
        shadowColor: '#3D3229',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.05,
        shadowRadius: 8,
        elevation: 3,
    } as ViewStyle,
    lg: {
        shadowColor: '#3D3229',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.06,
        shadowRadius: 16,
        elevation: 6,
    } as ViewStyle,
};

// -------------------------
// TYPOGRAPHY (clear hierarchy: page > section > body > label > caption)
// -------------------------
export const SoftTypography = {
    // Page / screen title
    h1: { fontSize: 24, fontWeight: '700' as const, letterSpacing: -0.5, lineHeight: 30 } as TextStyle,
    // Section title (e.g. "Tisch wählen", "Kategorien")
    h2: { fontSize: 18, fontWeight: '600' as const, letterSpacing: -0.2, lineHeight: 24 } as TextStyle,
    // Card/list title
    h3: { fontSize: 16, fontWeight: '600' as const, letterSpacing: 0 } as TextStyle,

    body: { fontSize: 15, fontWeight: '400' as const, lineHeight: 22 } as TextStyle,
    bodySmall: { fontSize: 13, fontWeight: '400' as const, lineHeight: 18 } as TextStyle,

    label: { fontSize: 13, fontWeight: '500' as const, letterSpacing: 0.3 } as TextStyle,
    /** Min 12px – do not use smaller for secondary text (readability) */
    caption: { fontSize: 12, fontWeight: '400' as const, letterSpacing: 0.2 } as TextStyle,

    price: { fontSize: 16, fontWeight: '700' as const } as TextStyle,
    priceSmall: { fontSize: 14, fontWeight: '600' as const } as TextStyle,
    /** Critical data: total, main price – stronger emphasis */
    priceTotal: { fontSize: 20, fontWeight: '800' as const, letterSpacing: -0.3 } as TextStyle,
};

/** Minimum font size for any secondary/caption text on POS. */
export const MIN_FONT_SIZE_SECONDARY = 12;

// -------------------------
// PRICE PRESENTATION (which typography for which context)
// -------------------------
export const PricePresentation = {
    /** Inline in list/card */
    inline: 'priceSmall' as const,
    /** Row total, line total */
    row: 'price' as const,
    /** Grand total, summary total */
    total: 'priceTotal' as const,
};
