import React from 'react';
import { View, Text, StyleSheet, ScrollView } from 'react-native';
import { ReceiptDTO } from '../types/ReceiptDTO';

interface ReceiptTemplateProps {
    receipt: ReceiptDTO;
}

export const ReceiptTemplate: React.FC<ReceiptTemplateProps> = ({ receipt }) => {
    const {
        company,
        items,
        taxRates,
        payments,
        signature,
        grandTotal,
        date,
        receiptNumber,
        cashierName,
        kassenID,
    } = receipt;

    const formatDate = (dateStr: string) => {
        try {
            return new Date(dateStr).toLocaleString('de-AT');
        } catch {
            return dateStr;
        }
    };

    const formatCurrency = (amount: number) => {
        return amount.toFixed(2).replace('.', ',');
    };

    return (
        <ScrollView style={styles.container} contentContainerStyle={styles.contentContainer}>
            {/* Header */}
            <View style={styles.header}>
                <Text style={styles.companyName}>{company?.name || 'Firma'}</Text>
                <Text style={styles.companyAddress}>{company?.address || ''}</Text>
                <Text style={styles.companyUid}>UID: {company?.taxNumber || ''}</Text>

                <View style={styles.metaData}>
                    <Text style={styles.metaText}>Beleg-Nr: {receiptNumber}</Text>
                    <Text style={styles.metaText}>Datum: {formatDate(date)}</Text>
                    <Text style={styles.metaText}>Kassen-ID: {kassenID}</Text>
                    <Text style={styles.metaText}>Kassierer: {cashierName}</Text>
                </View>
            </View>

            {/* Items */}
            <View style={styles.itemsContainer}>
                <View style={styles.itemHeader}>
                    <Text style={[styles.itemText, styles.flex2, styles.bold]}>Artikel</Text>
                    <Text style={[styles.itemText, styles.flexRight, styles.bold]}>Menge</Text>
                    <Text style={[styles.itemText, styles.flexRight, styles.bold]}>Einzel</Text>
                    <Text style={[styles.itemText, styles.flexRight, styles.bold]}>Gesamt</Text>
                </View>

                {items.map((item, index) => (
                    <View key={index} style={styles.itemRow}>
                        <Text style={[styles.itemText, styles.flex2]}>{item.name}</Text>
                        <Text style={[styles.itemText, styles.flexRight]}>{item.quantity}</Text>
                        <Text style={[styles.itemText, styles.flexRight]}>{formatCurrency(item.unitPrice)}</Text>
                        <Text style={[styles.itemText, styles.flexRight]}>{formatCurrency(item.totalPrice)} {item.taxRate >= 20 ? 'A' : (item.taxRate >= 10 ? 'B' : 'C')}</Text>
                    </View>
                ))}
            </View>

            {/* Totals */}
            <View style={styles.divider} />

            <View style={styles.totalRow}>
                <Text style={styles.totalLabel}>SUMME EUR</Text>
                <Text style={styles.totalValue}>{formatCurrency(grandTotal)}</Text>
            </View>

            {/* VAT Breakdown */}
            <View style={styles.vatContainer}>
                <Text style={styles.vatHeader}>Steueraufschlüsselung</Text>
                <View style={styles.vatRow}>
                    <Text style={[styles.vatText, styles.bold]}>Satz</Text>
                    <Text style={[styles.vatText, styles.bold]}>Netto</Text>
                    <Text style={[styles.vatText, styles.bold]}>Steuer</Text>
                    <Text style={[styles.vatText, styles.bold]}>Brutto</Text>
                </View>
                {taxRates.map((rate, index) => (
                    <View key={index} style={styles.vatRow}>
                        <Text style={styles.vatText}>{rate.rate}% ({rate.rate >= 20 ? 'A' : (rate.rate >= 10 ? 'B' : 'C')})</Text>
                        <Text style={styles.vatText}>{formatCurrency(rate.netAmount)}</Text>
                        <Text style={styles.vatText}>{formatCurrency(rate.taxAmount)}</Text>
                        <Text style={styles.vatText}>{formatCurrency(rate.grossAmount)}</Text>
                    </View>
                ))}
            </View>

            {/* Payments */}
            <View style={styles.paymentsContainer}>
                {payments.map((payment, index) => (
                    <View key={index} style={styles.paymentRow}>
                        <Text style={styles.paymentMethod}>{payment.method === 'cash' ? 'Barzahlung' : payment.method}</Text>
                        <Text style={styles.paymentAmount}>{formatCurrency(payment.amount)} EUR</Text>
                    </View>
                ))}
                {payments.some(p => p.method === 'cash') && (
                    <>
                        <View style={styles.paymentRow}>
                            <Text style={styles.paymentMethod}>Gegeben Bar</Text>
                            <Text style={styles.paymentAmount}>{formatCurrency(payments.find(p => p.method === 'cash')?.tendered || 0)} EUR</Text>
                        </View>
                        <View style={styles.paymentRow}>
                            <Text style={styles.paymentMethod}>Rückgeld</Text>
                            <Text style={styles.paymentAmount}>{formatCurrency(payments.find(p => p.method === 'cash')?.change || 0)} EUR</Text>
                        </View>
                    </>
                )}
            </View>

            {/* RKSV Signature */}
            <View style={styles.signatureBlock}>
                <Text style={styles.signatureHeader}>Sicherheitseinrichtung</Text>
                {signature && (
                    <>
                        <View style={[styles.qrContainer, { alignItems: 'center', justifyContent: 'center', height: 150, width: 150, borderWidth: 1, borderColor: '#ccc' }]}>
                            <Text style={{ fontSize: 10, textAlign: 'center' }}>QR Code</Text>
                        </View>
                        <Text style={styles.signatureText}>{signature.value}</Text>
                        <Text style={styles.signatureMeta}>{signature.serialNumber} | {signature.timestamp}</Text>
                    </>
                )}
            </View>

            <Text style={styles.footerText}>Vielen Dank für Ihren Einkauf!</Text>
        </ScrollView>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#fff',
    },
    contentContainer: {
        padding: 20,
        paddingBottom: 40,
    },
    header: {
        alignItems: 'center',
        marginBottom: 20,
    },
    companyName: {
        fontSize: 18,
        fontWeight: 'bold',
        marginBottom: 5,
        textAlign: 'center',
    },
    companyAddress: {
        fontSize: 12,
        textAlign: 'center',
        marginBottom: 2,
    },
    companyUid: {
        fontSize: 12,
        textAlign: 'center',
        marginBottom: 10,
    },
    metaData: {
        alignItems: 'center',
        marginTop: 10,
    },
    metaText: {
        fontSize: 12,
        fontFamily: 'monospace',
    },
    itemsContainer: {
        marginBottom: 20,
    },
    itemHeader: {
        flexDirection: 'row',
        borderBottomWidth: 1,
        borderBottomColor: '#000',
        paddingBottom: 5,
        marginBottom: 5,
    },
    itemRow: {
        flexDirection: 'row',
        marginBottom: 5,
    },
    itemText: {
        fontSize: 12,
        fontFamily: 'monospace',
    },
    flex2: {
        flex: 2,
    },
    flexRight: {
        flex: 1,
        textAlign: 'right',
    },
    bold: {
        fontWeight: 'bold',
    },
    divider: {
        borderBottomWidth: 1,
        borderBottomColor: '#000',
        marginBottom: 10,
    },
    totalRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        marginBottom: 20,
    },
    totalLabel: {
        fontSize: 16,
        fontWeight: 'bold',
    },
    totalValue: {
        fontSize: 16,
        fontWeight: 'bold',
    },
    vatContainer: {
        marginBottom: 15,
    },
    vatHeader: {
        fontSize: 12,
        fontWeight: 'bold',
        marginBottom: 5,
        borderBottomWidth: 1,
        borderBottomColor: '#ddd',
    },
    vatRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        marginBottom: 2,
    },
    vatText: {
        fontSize: 10,
        fontFamily: 'monospace',
        flex: 1,
        textAlign: 'right',
    },
    paymentsContainer: {
        marginBottom: 20,
        borderTopWidth: 1,
        borderTopColor: '#000',
        paddingTop: 10,
    },
    paymentRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        marginBottom: 2,
    },
    paymentMethod: {
        fontSize: 12,
    },
    paymentAmount: {
        fontSize: 12,
        fontWeight: 'bold',
    },
    signatureBlock: {
        marginTop: 20,
        alignItems: 'center',
        borderTopWidth: 1,
        borderTopColor: '#ddd',
        paddingTop: 10,
    },
    signatureHeader: {
        fontSize: 10,
        marginBottom: 10,
        fontWeight: 'bold',
    },
    qrContainer: {
        marginBottom: 10,
        padding: 10,
        backgroundColor: '#fff',
    },
    signatureText: {
        fontSize: 8,
        fontFamily: 'monospace',
        textAlign: 'center',
        marginBottom: 5,
    },
    signatureMeta: {
        fontSize: 8,
        textAlign: 'center',
        color: '#666',
    },
    footerText: {
        textAlign: 'center',
        marginTop: 30,
        fontSize: 12,
        fontStyle: 'italic',
    },
});
