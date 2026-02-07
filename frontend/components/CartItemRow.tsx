import React from 'react';
import { View, Text, StyleSheet, Pressable, Platform, ActivityIndicator } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftShadows } from '../constants/SoftTheme';
import { formatPrice, formatPercent } from '../utils/formatPrice';

export interface CartItem {
    id?: string;
    itemId?: string;
    productId: string;
    productName: string;
    quantity?: number;
    qty?: number;
    unitPrice: number;
    totalPrice: number;
    taxType?: string;
    taxRate?: number;
    notes?: string;
    productImage?: string;
}

interface CartItemRowProps {
    item: CartItem;
    onIncrease?: () => Promise<void>;
    onDecrease?: () => Promise<void>;
}

export const CartItemRow: React.FC<CartItemRowProps> = ({ item, onIncrease, onDecrease }) => {
    const [updating, setUpdating] = React.useState(false);
    const quantity = item.quantity || item.qty || 0;
    const taxRate = item.taxRate || 0.20;
    const taxLabel = `inkl. ${formatPercent(taxRate)} MwSt.`;

    return (
        <View style={styles.container}>
            {/* Row 1: Product Name + Total Price */}
            <View style={styles.mainRow}>
                <Text style={styles.productName} numberOfLines={2}>
                    {item.productName || 'Unbekanntes Produkt'}
                </Text>
                <Text style={styles.totalPrice}>
                    {formatPrice(item.totalPrice)}
                </Text>
            </View>

            {/* Row 2: Qty Control & Price */}
            <View style={styles.detailsRow}>
                {/* Quantity Control (Antigravity Style) */}
                <View style={styles.qtyContainer}>
                    <Pressable
                        onPress={async () => {
                            if (updating || !onDecrease) return;
                            setUpdating(true);
                            try { await onDecrease(); } finally { setUpdating(false); }
                        }}
                        disabled={!onDecrease || updating}
                        style={({ pressed }) => [
                            styles.qtyBtn,
                            (!onDecrease || updating) && styles.qtyBtnDisabled,
                            pressed && styles.qtyBtnPressed
                        ]}
                        hitSlop={8}
                        accessibilityLabel="Decrease quantity"
                        accessibilityRole="button"
                    >
                        <Text style={[styles.qtyBtnText, updating && styles.qtyBtnTextDisabled]}>-</Text>
                    </Pressable>

                    <View style={styles.qtyValueContainer}>
                        {updating ? (
                            <ActivityIndicator size="small" color={SoftColors.accent} />
                        ) : (
                            <Text style={styles.qtyValue}>{quantity}</Text>
                        )}
                    </View>

                    <Pressable
                        onPress={async () => {
                            if (updating || !onIncrease) return;
                            setUpdating(true);
                            try { await onIncrease(); } finally { setUpdating(false); }
                        }}
                        disabled={!onIncrease || updating}
                        style={({ pressed }) => [
                            styles.qtyBtn,
                            (!onIncrease || updating) && styles.qtyBtnDisabled,
                            pressed && styles.qtyBtnPressed
                        ]}
                        hitSlop={8}
                        accessibilityLabel="Increase quantity"
                        accessibilityRole="button"
                    >
                        <Text style={[styles.qtyBtnText, (!onIncrease || updating) && styles.qtyBtnTextDisabled]}>+</Text>
                    </Pressable>
                </View>

                {/* Price Calculation */}
                <View style={styles.priceContainer}>
                    <Text style={styles.unitPrice}>
                        {formatPrice(item.unitPrice)} / Stk.
                    </Text>
                    <Text style={styles.taxInfo}>{taxLabel}</Text>
                </View>
            </View>

            {/* Row 3: Notes (if exists) */}
            {item.notes && (
                <Text style={styles.notes} numberOfLines={1}>
                    📝 {item.notes}
                </Text>
            )}
        </View>
    );
};

const styles = StyleSheet.create({
    container: {
        paddingVertical: 12,
        paddingHorizontal: 16,
        borderBottomWidth: 1,
        borderBottomColor: SoftColors.borderLight,
        backgroundColor: SoftColors.bgCard,
    },
    mainRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        marginBottom: 4,
    },
    productName: {
        flex: 1,
        fontSize: 14,
        fontWeight: '600',
        color: '#1F2937',
        marginRight: 12,
    },
    totalPrice: {
        fontSize: 14,
        fontWeight: '700',
        color: '#059669', // Green accent
    },
    detailsRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginTop: SoftSpacing.xs,
    },
    qtyContainer: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: SoftColors.bgSecondary,
        borderRadius: SoftRadius.full,
        padding: 2,
    },
    qtyBtn: {
        width: 28,
        height: 28,
        borderRadius: 14,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: SoftColors.bgCard,
        ...SoftShadows.sm,
    },
    qtyBtnPressed: {
        backgroundColor: SoftColors.borderLight,
        transform: [{ scale: 0.95 }],
    },
    qtyBtnDisabled: {
        opacity: 0.5,
        backgroundColor: 'transparent',
        elevation: 0,
        shadowOpacity: 0,
    },
    qtyBtnText: {
        fontSize: 16,
        fontWeight: '600',
        color: SoftColors.textPrimary,
        lineHeight: Platform.OS === 'web' ? 16 : undefined,
    },
    qtyBtnTextDisabled: {
        color: SoftColors.textMuted,
    },
    qtyValueContainer: {
        minWidth: 32,
        alignItems: 'center',
        justifyContent: 'center',
    },
    qtyValue: {
        fontSize: 14,
        fontWeight: '700',
        color: SoftColors.textPrimary,
        textAlign: 'center',
    },
    priceContainer: {
        alignItems: 'flex-end',
    },
    unitPrice: {
        fontSize: 12,
        color: SoftColors.textSecondary,
    },
    taxInfo: {
        fontSize: 10,
        color: SoftColors.textMuted,
        fontStyle: 'italic',
    },
    notes: {
        fontSize: 11,
        color: SoftColors.textSecondary,
        marginTop: 6,
        fontStyle: 'italic',
        backgroundColor: SoftColors.bgSecondary,
        paddingHorizontal: 8,
        paddingVertical: 2,
        borderRadius: 4,
        alignSelf: 'flex-start',
    },
});
