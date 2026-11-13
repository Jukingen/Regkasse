import React, { useMemo } from 'react';
import { View, Text, StyleSheet, ScrollView } from 'react-native';

import { RECEIPT_FONT_FAMILY } from '../constants/fonts';
import {
  DEFAULT_THANK_YOU_MESSAGE,
  RECEIPT_SEP_DOUBLE,
  RECEIPT_SEP_SINGLE,
  formatPaymentMethodLabel,
  formatTseSignatureDisplay,
} from '../services/receiptFormatter';
import { Invoice, InvoiceItem } from '../types/invoice';
import { formatUserDate, formatUserTime } from '../utils/dateFormatter';

interface ReceiptPrintProps {
  invoice: Invoice;
  items: InvoiceItem[];
  isPreview?: boolean;
}

function formatMoney(value: number | undefined | null): string {
  if (value == null || Number.isNaN(Number(value))) return '0,00';
  return Number(value).toFixed(2).replace('.', ',');
}

function buildMwstRows(invoice: Invoice, items: InvoiceItem[]) {
  const summary = invoice.taxSummary;
  const fromSummary = [
    {
      rate: 20,
      net: summary.standardTaxBase,
      tax: summary.standardTaxAmount,
      gross: summary.standardTaxBase + summary.standardTaxAmount,
    },
    {
      rate: 13,
      net: summary.specialTaxBase,
      tax: summary.specialTaxAmount,
      gross: summary.specialTaxBase + summary.specialTaxAmount,
    },
    {
      rate: 10,
      net: summary.reducedTaxBase,
      tax: summary.reducedTaxAmount,
      gross: summary.reducedTaxBase + summary.reducedTaxAmount,
    },
  ].filter((row) => row.net !== 0 || row.tax !== 0 || row.gross !== 0);

  if (fromSummary.length > 0) return fromSummary;

  const grouped = new Map<number, { net: number; tax: number; gross: number }>();
  for (const item of items) {
    const rate = Number(item.taxType) || 0;
    const current = grouped.get(rate) ?? { net: 0, tax: 0, gross: 0 };
    const gross = item.totalAmount ?? 0;
    const tax = item.taxAmount ?? 0;
    current.gross += gross;
    current.tax += tax;
    current.net += gross - tax;
    grouped.set(rate, current);
  }

  return [...grouped.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([rate, row]) => ({ rate, ...row }));
}

const ReceiptPrint: React.FC<ReceiptPrintProps> = ({ invoice, items, isPreview = true }) => {
  const brutto = invoice.taxSummary.totalAmount ?? 0;
  const mwst = invoice.taxSummary.totalTaxAmount ?? 0;
  const netto = brutto - mwst;
  const mwstRows = useMemo(() => buildMwstRows(invoice, items), [invoice, items]);
  const paymentMethod = formatPaymentMethodLabel(
    invoice.paymentDetails?.paymentMethod || invoice.paymentMethod
  );
  const paymentAmount = invoice.paymentDetails?.amount ?? brutto;
  const given = invoice.paymentDetails?.cashAmount;
  const change = invoice.paymentDetails?.changeAmount;
  const thankYou =
    invoice.thankYouMessage?.trim() ||
    invoice.footerText?.trim() ||
    DEFAULT_THANK_YOU_MESSAGE;
  const companyDescription = invoice.companyDescription?.trim();
  const showCompanyDescription =
    !!companyDescription && companyDescription !== thankYou;
  return (
    <ScrollView
      style={[styles.container, !isPreview && styles.printContainer]}
      showsVerticalScrollIndicator={false}>
      <View style={styles.receipt}>
        <Text style={styles.sep}>{RECEIPT_SEP_DOUBLE}</Text>
        <View style={styles.header}>
          <Text style={styles.companyName}>{invoice.customerDetails?.companyName || ''}</Text>
          <Text style={styles.address}>{invoice.customerDetails?.address || ''}</Text>
          {invoice.customerDetails?.taxNumber ? (
            <Text style={styles.taxNumber}>UID: {invoice.customerDetails.taxNumber}</Text>
          ) : null}
        </View>
        <Text style={styles.sep}>{RECEIPT_SEP_DOUBLE}</Text>

        <View style={styles.metaBlock}>
          <Text style={styles.metaText}>Beleg: {invoice.receiptNumber}</Text>
          <Text style={styles.metaText}>
            Datum:{' '}
            {invoice.createdAt
              ? `${formatUserDate(new Date(invoice.createdAt))} ${formatUserTime(new Date(invoice.createdAt), { includeSeconds: true })}`
              : formatUserDate(new Date())}
          </Text>
          {invoice.branchName ? (
            <Text style={styles.metaText}>Filiale: {invoice.branchName}</Text>
          ) : null}
          <Text style={styles.metaText}>Kassen-ID: {invoice.kasseId || 'N/A'}</Text>
          {invoice.terminalNumber ? (
            <Text style={styles.metaText}>Terminal: {invoice.terminalNumber}</Text>
          ) : null}
          <Text style={styles.metaText}>Kassierer: {invoice.cashierName || 'N/A'}</Text>
        </View>

        <Text style={styles.sep}>{RECEIPT_SEP_SINGLE}</Text>
        <View style={styles.tableHeader}>
          <Text style={[styles.cellName, styles.bold]}>Artikel</Text>
          <Text style={[styles.cellQty, styles.bold]}>Menge</Text>
          <Text style={[styles.cellNum, styles.bold]}>Einh.</Text>
          <Text style={[styles.cellNum, styles.bold]}>Betrag</Text>
        </View>
        {items.map((item, index) => (
          <View key={item.id || index} style={styles.tableRow}>
            <Text style={styles.cellName}>{item.productName}</Text>
            <Text style={styles.cellQty}>{item.quantity}</Text>
            <Text style={styles.cellNum}>{formatMoney(item.unitPrice)}</Text>
            <Text style={styles.cellNum}>{formatMoney(item.totalAmount)}</Text>
          </View>
        ))}

        <Text style={styles.sep}>{RECEIPT_SEP_SINGLE}</Text>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>Netto:</Text>
          <Text style={styles.summaryValue}>{formatMoney(netto)}</Text>
        </View>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>MwSt:</Text>
          <Text style={styles.summaryValue}>{formatMoney(mwst)}</Text>
        </View>
        <View style={styles.summaryRow}>
          <Text style={[styles.summaryLabel, styles.bold]}>SUMME / Brutto:</Text>
          <Text style={[styles.summaryValue, styles.bold]}>EUR {formatMoney(brutto)}</Text>
        </View>

        <Text style={styles.sep}>{RECEIPT_SEP_SINGLE}</Text>
        <View style={styles.tableHeader}>
          <Text style={[styles.cellMwst, styles.bold]}>MwSt%</Text>
          <Text style={[styles.cellMwst, styles.bold]}>Netto</Text>
          <Text style={[styles.cellMwst, styles.bold]}>MwSt</Text>
          <Text style={[styles.cellMwst, styles.bold]}>Brutto</Text>
        </View>
        {mwstRows.map((row) => (
          <View key={row.rate} style={styles.tableRow}>
            <Text style={styles.cellMwst}>{formatMoney(row.rate)}%</Text>
            <Text style={styles.cellMwst}>{formatMoney(row.net)}</Text>
            <Text style={styles.cellMwst}>{formatMoney(row.tax)}</Text>
            <Text style={styles.cellMwst}>{formatMoney(row.gross)}</Text>
          </View>
        ))}

        <Text style={styles.sep}>{RECEIPT_SEP_SINGLE}</Text>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>{paymentMethod}:</Text>
          <Text style={styles.summaryValue}>{formatMoney(paymentAmount)}</Text>
        </View>
        {given != null ? (
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLabel}>Gegeben:</Text>
            <Text style={styles.summaryValue}>{formatMoney(given)}</Text>
          </View>
        ) : null}
        {change != null ? (
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLabel}>Rückgeld:</Text>
            <Text style={styles.summaryValue}>{formatMoney(change)}</Text>
          </View>
        ) : null}

        <Text style={styles.sep}>{RECEIPT_SEP_SINGLE}</Text>
        <Text style={styles.sectionTitle}>Registrierkassensicherheitsverordnung</Text>
        <Text style={styles.tseInfo}>TSE-Seriennummer: {invoice.tseSerialNumber || '—'}</Text>
        <Text style={styles.tseInfo}>{formatTseSignatureDisplay(invoice.tseSignature)}</Text>
        <Text style={styles.tseInfo}>
          TSE-Zeitstempel:{' '}
          {invoice.tseTime
            ? `${formatUserDate(new Date(invoice.tseTime))} ${formatUserTime(new Date(invoice.tseTime), { includeSeconds: true })}`
            : '—'}
        </Text>

        <Text style={styles.sep}>{RECEIPT_SEP_SINGLE}</Text>
        <Text style={styles.footerText}>{thankYou}</Text>
        {showCompanyDescription ? (
          <Text style={styles.footerText}>{companyDescription}</Text>
        ) : null}
        <Text style={styles.sep}>{RECEIPT_SEP_DOUBLE}</Text>
      </View>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f4f4f4',
  },
  printContainer: {
    backgroundColor: '#fff',
  },
  receipt: {
    paddingHorizontal: 12,
    paddingVertical: 14,
    backgroundColor: '#fff',
    margin: 8,
  },
  sep: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 11,
    textAlign: 'center',
    marginVertical: 6,
  },
  header: {
    alignItems: 'center',
  },
  companyName: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 15,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: 2,
  },
  address: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 12,
    textAlign: 'center',
  },
  taxNumber: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 12,
    textAlign: 'center',
    marginTop: 2,
  },
  metaBlock: {
    marginBottom: 2,
  },
  metaText: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 12,
    marginBottom: 1,
  },
  tableHeader: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    marginBottom: 4,
  },
  tableRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    marginBottom: 3,
  },
  cellName: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 11,
    flex: 1.6,
    paddingRight: 4,
  },
  cellQty: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 11,
    width: 40,
    textAlign: 'center',
  },
  cellNum: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 11,
    width: 56,
    textAlign: 'right',
  },
  cellMwst: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 11,
    flex: 1,
    textAlign: 'right',
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 2,
  },
  summaryLabel: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 12,
  },
  summaryValue: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 12,
    textAlign: 'right',
    minWidth: 72,
  },
  sectionTitle: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 11,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  tseInfo: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 9,
    lineHeight: 13,
    marginBottom: 4,
  },
  footerText: {
    fontFamily: RECEIPT_FONT_FAMILY,
    fontSize: 12,
    textAlign: 'center',
    marginVertical: 4,
  },
  bold: {
    fontWeight: 'bold',
  },
});

export default ReceiptPrint;
