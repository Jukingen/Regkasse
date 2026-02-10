import React from 'react';
import { View, Text, StyleSheet, ScrollView } from 'react-native';

import { Invoice, InvoiceItem } from '../types/invoice';
import { Colors, Spacing, BorderRadius } from '../constants/Colors';

interface ReceiptPrintProps {
  invoice: Invoice;
  items: InvoiceItem[];
  isPreview?: boolean;
}

const ReceiptPrint: React.FC<ReceiptPrintProps> = ({
  invoice,
  items,
  isPreview = true,
}) => {
  const formatDate = (date: Date) => {
    return date.toLocaleDateString('de-DE', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  };

  const formatTime = (date: Date) => {
    return date.toLocaleTimeString('de-DE', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  };

  const calculateTaxAmount = (items: InvoiceItem[], taxType: number) => {
    return items
      .filter(item => item.taxType === taxType)
      .reduce((sum, item) => sum + item.taxAmount, 0);
  };

  const standardTaxItems = items.filter(item => item.taxType === 20);
  const reducedTaxItems = items.filter(item => item.taxType === 10);
  const specialTaxItems = items.filter(item => item.taxType === 13);

  const standardTaxAmount = calculateTaxAmount(items, 20);
  const reducedTaxAmount = calculateTaxAmount(items, 10);
  const specialTaxAmount = calculateTaxAmount(items, 13);

  return (
    <ScrollView style={styles.container} showsVerticalScrollIndicator={false}>
      <View style={styles.receipt}>
        {/* Header */}
        <View style={styles.header}>
          <Text style={styles.companyName}>{invoice.customerDetails?.companyName || 'DEMO GMBH'}</Text>
          <Text style={styles.address}>{invoice.customerDetails?.address || 'Hauptstraße 1, 1010 Wien'}</Text>
          <Text style={styles.taxNumber}>{invoice.customerDetails?.taxNumber || 'UID: ATU12345678'}</Text>
        </View>

        <View style={styles.separator} />

        {/* Receipt Info */}
        <View style={styles.receiptInfo}>
          <Text style={styles.receiptTitle}>KASSA BELEG</Text>
          <Text style={styles.receiptNumber}>Beleg-Nr: {invoice.receiptNumber}</Text>
          <Text style={styles.receiptDate}>
            Datum: {invoice.createdAt ? formatDate(new Date(invoice.createdAt)) : new Date().toLocaleDateString('de-DE')}
          </Text>
          <View style={styles.metaRow}>
            <Text style={styles.metaText}>
              Kasse: {invoice.kasseId || 'N/A'} | Kassierer: {invoice.cashierName || 'N/A'}
            </Text>
          </View>
        </View>

        <View style={styles.separator} />

        {/* Items */}
        <View style={styles.itemsSection}>
          <Text style={styles.sectionTitle}>ARTIKEL</Text>
          {items.map((item, index) => (
            <View key={index} style={styles.itemRow}>
              <View style={styles.itemInfo}>
                <Text style={styles.itemName}>{item.productName}</Text>
                <Text style={styles.itemDetails}>
                  {item.quantity} x €{item.unitPrice.toFixed(2)}
                </Text>
              </View>
              <Text style={styles.itemTotal}>€{item.totalAmount.toFixed(2)}</Text>
            </View>
          ))}
        </View>

        <View style={styles.separator} />

        {/* Tax Summary */}
        <View style={styles.taxSection}>
          <Text style={styles.sectionTitle}>STEUERÜBERSICHT</Text>
          {/* Fallback tax display if breakdown not available */}
          <View style={styles.taxRow}>
            <Text style={styles.taxLabel}>Steuer Gesamt:</Text>
            <Text style={styles.taxAmount}>€{invoice.taxSummary.totalTaxAmount?.toFixed(2) || '0.00'}</Text>
          </View>
        </View>

        <View style={styles.separator} />

        {/* Total */}
        <View style={styles.totalSection}>
          <View style={styles.totalRow}>
            <Text style={styles.totalLabel}>GESAMT:</Text>
            <Text style={styles.totalAmount}>€{invoice.taxSummary.totalAmount.toFixed(2)}</Text>
          </View>
        </View>

        <View style={styles.separator} />

        {/* TSE Information */}
        <View style={styles.tseSection}>
          <Text style={styles.sectionTitle}>TSE INFORMATIONEN</Text>
          <Text style={styles.tseInfo}>TSE-Seriennummer: {invoice.tseSerialNumber}</Text>
          <Text style={styles.tseInfo}>TSE-Signatur: {invoice.tseSignature}</Text>
          <Text style={styles.tseInfo}>
            TSE-Zeitstempel: {invoice.tseTime ? formatDate(new Date(invoice.tseTime)) + ' ' + formatTime(new Date(invoice.tseTime)) : '-'}
          </Text>
          <Text style={styles.tseInfo}>TSE-Prozessart: {invoice.tseProcessType}</Text>
        </View>

        <View style={styles.separator} />

        {/* Footer */}
        <View style={styles.footer}>
          <Text style={styles.footerText}>{(invoice as any).footerText || 'Vielen Dank für Ihren Einkauf!'}</Text>
          <Text style={styles.footerText}>Bitte bewahren Sie diesen Beleg auf.</Text>
        </View>

        <View style={styles.separator} />
      </View>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  receipt: {
    padding: Spacing.md,
    backgroundColor: 'white',
    margin: Spacing.sm,
    borderRadius: BorderRadius.sm,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  header: {
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  companyName: {
    fontFamily: 'OCRA-B',
    fontSize: 16,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: Spacing.xs,
  },
  address: {
    fontFamily: 'OCRA-B',
    fontSize: 12,
    textAlign: 'center',
    marginBottom: Spacing.xs,
  },
  taxNumber: {
    fontFamily: 'OCRA-B',
    fontSize: 12,
    textAlign: 'center',
  },
  separator: {
    height: 1,
    backgroundColor: Colors.light.border,
    marginVertical: Spacing.sm,
  },
  receiptInfo: {
    marginBottom: Spacing.sm,
  },
  receiptTitle: {
    fontFamily: 'OCRA-B',
    fontSize: 14,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: Spacing.xs,
  },
  receiptNumber: {
    fontFamily: 'OCRA-B',
    fontSize: 12,
    marginBottom: Spacing.xs,
  },
  receiptDate: {
    fontFamily: 'OCRA-B',
    fontSize: 12,
    marginBottom: Spacing.xs,
  },
  receiptTime: {
    fontFamily: 'OCRA-B',
    fontSize: 12,
  },
  metaRow: {
    flexDirection: 'row',
    justifyContent: 'center',
    marginTop: Spacing.xs,
  },
  metaText: {
    fontFamily: 'OCRA-B',
    fontSize: 10,
    color: Colors.light.textSecondary,
  },
  itemsSection: {
    marginBottom: Spacing.sm,
  },
  sectionTitle: {
    fontFamily: 'OCRA-B',
    fontSize: 12,
    fontWeight: 'bold',
    marginBottom: Spacing.xs,
  },
  itemRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: Spacing.xs,
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    fontFamily: 'OCRA-B',
    fontSize: 11,
    fontWeight: '600',
  },
  itemDetails: {
    fontFamily: 'OCRA-B',
    fontSize: 10,
    color: Colors.light.textSecondary,
  },
  itemTotal: {
    fontFamily: 'OCRA-B',
    fontSize: 11,
    fontWeight: '600',
  },
  taxSection: {
    marginBottom: Spacing.sm,
  },
  taxRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: Spacing.xs,
  },
  taxLabel: {
    fontFamily: 'OCRA-B',
    fontSize: 11,
  },
  taxAmount: {
    fontFamily: 'OCRA-B',
    fontSize: 11,
    fontWeight: '600',
  },
  totalSection: {
    marginBottom: Spacing.sm,
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  totalLabel: {
    fontFamily: 'OCRA-B',
    fontSize: 14,
    fontWeight: 'bold',
  },
  totalAmount: {
    fontFamily: 'OCRA-B',
    fontSize: 14,
    fontWeight: 'bold',
  },
  tseSection: {
    marginBottom: Spacing.sm,
  },
  tseInfo: {
    fontFamily: 'OCRA-B',
    fontSize: 10,
    marginBottom: Spacing.xs,
  },
  footer: {
    alignItems: 'center',
  },
  footerText: {
    fontFamily: 'OCRA-B',
    fontSize: 11,
    textAlign: 'center',
    marginBottom: Spacing.xs,
  },
});

export default ReceiptPrint; 