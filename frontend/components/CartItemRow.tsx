import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
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
}

export const CartItemRow: React.FC<CartItemRowProps> = ({ item }) => {
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

            {/* Row 2: Qty × UnitPrice + Tax Info */}
            <View style={styles.detailsRow}>
                <Text style={styles.qtyPrice}>
                    {quantity} × {formatPrice(item.unitPrice)}
                </Text>
                <Text style={styles.taxInfo}>{taxLabel}</Text>
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
        borderBottomColor: '#E5E7EB',
        backgroundColor: '#FFFFFF',
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
    },
    qtyPrice: {
        fontSize: 12,
        color: '#6B7280',
    },
    taxInfo: {
        fontSize: 10,
        color: '#9CA3AF',
        fontStyle: 'italic',
    },
    notes: {
        fontSize: 11,
        color: '#6B7280',
        marginTop: 4,
        fontStyle: 'italic',
    },
});
