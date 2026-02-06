// Soft minimal button component with variants
import React from 'react';
import { StyleSheet, Pressable, Text, ActivityIndicator, ViewStyle, TextStyle } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../../constants/SoftTheme';

interface ButtonProps {
    title: string;
    variant?: 'primary' | 'secondary' | 'ghost';
    size?: 'sm' | 'md' | 'lg';
    loading?: boolean;
    disabled?: boolean;
    onPress?: () => void;
    style?: ViewStyle;
}

export function SoftButton({
    title,
    variant = 'primary',
    size = 'md',
    loading,
    disabled,
    onPress,
    style
}: ButtonProps) {
    const sizeStyles = {
        sm: styles.sizeSm,
        md: styles.sizeMd,
        lg: styles.sizeLg,
    };

    const textSizeStyles = {
        sm: styles.textSm,
        md: styles.textMd,
        lg: styles.textLg,
    };

    const variantStyles: Record<string, ViewStyle> = {
        primary: styles.primary,
        secondary: styles.secondary,
        ghost: styles.ghost,
    };

    const textVariantStyles: Record<string, TextStyle> = {
        primary: styles.primaryText,
        secondary: styles.secondaryText,
        ghost: styles.ghostText,
    };

    return (
        <Pressable
            style={({ pressed }) => [
                styles.base,
                sizeStyles[size],
                variantStyles[variant],
                pressed && styles.pressed,
                disabled && styles.disabled,
                style,
            ]}
            onPress={onPress}
            disabled={disabled || loading}
        >
            {loading ? (
                <ActivityIndicator
                    color={variant === 'primary' ? SoftColors.textInverse : SoftColors.accent}
                    size="small"
                />
            ) : (
                <Text style={[styles.text, textSizeStyles[size], textVariantStyles[variant]]}>
                    {title}
                </Text>
            )}
        </Pressable>
    );
}

const styles = StyleSheet.create({
    base: {
        borderRadius: SoftRadius.md,
        justifyContent: 'center',
        alignItems: 'center',
    },
    sizeSm: {
        height: 36,
        paddingHorizontal: SoftSpacing.lg,
    },
    sizeMd: {
        height: 48,
        paddingHorizontal: SoftSpacing.xl,
    },
    sizeLg: {
        height: 56,
        paddingHorizontal: SoftSpacing.xxl,
    },
    primary: {
        backgroundColor: SoftColors.accent,
        ...SoftShadows.sm,
    },
    secondary: {
        backgroundColor: SoftColors.bgSecondary,
        borderWidth: 1,
        borderColor: SoftColors.border,
    },
    ghost: {
        backgroundColor: 'transparent',
    },
    pressed: {
        opacity: 0.85,
        transform: [{ scale: 0.98 }],
    },
    disabled: {
        opacity: 0.5,
    },
    text: {
        ...SoftTypography.label,
    },
    textSm: {
        fontSize: 12,
    },
    textMd: {
        fontSize: 14,
    },
    textLg: {
        fontSize: 16,
    },
    primaryText: {
        color: SoftColors.textInverse,
    },
    secondaryText: {
        color: SoftColors.textPrimary,
    },
    ghostText: {
        color: SoftColors.accent,
    },
});
