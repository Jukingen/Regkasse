import React, { useState } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../contexts/AuthContext';

interface ReportSummary {
    totalSales: number;
    totalTransactions: number;
    averageTransaction: number;
    taxDetails: {
        standard: number;
        reduced: number;
        special: number;
    };
}

export default function ReportsScreen() {
    const { t } = useTranslation();
    const { user } = useAuth();
    const [selectedPeriod, setSelectedPeriod] = useState<'today' | 'week' | 'month'>('today');
    const [summary, setSummary] = useState<ReportSummary>({
        totalSales: 0,
        totalTransactions: 0,
        averageTransaction: 0,
        taxDetails: {
            standard: 0,
            reduced: 0,
            special: 0
        }
    });

    const handlePeriodChange = (period: 'today' | 'week' | 'month') => {
        setSelectedPeriod(period);
        // TODO: Seçilen periyoda göre rapor verilerini yükle
        loadReportData(period);
    };

    const loadReportData = async (period: 'today' | 'week' | 'month') => {
        try {
            // TODO: API'den rapor verilerini yükle
            const mockData: ReportSummary = {
                totalSales: 1234.56,
                totalTransactions: 42,
                averageTransaction: 29.39,
                taxDetails: {
                    standard: 205.76,
                    reduced: 0,
                    special: 0
                }
            };
            setSummary(mockData);
        } catch (error) {
            Alert.alert(
                t('reports.error.title'),
                t('reports.error.load_failed')
            );
        }
    };

    const handleExportReport = () => {
        // TODO: Raporu dışa aktar
        Alert.alert('Info', 'Rapor dışa aktarma özelliği yakında eklenecek');
    };

    const handlePrintReport = () => {
        // TODO: Raporu yazdır
        Alert.alert('Info', 'Rapor yazdırma özelliği yakında eklenecek');
    };

    return (
        <View style={styles.container}>
            <View style={styles.header}>
                <Text style={styles.headerText}>{t('reports.title')}</Text>
                <View style={styles.headerButtons}>
                    <TouchableOpacity
                        style={styles.headerButton}
                        onPress={handleExportReport}
                    >
                        <Ionicons name="download-outline" size={24} color="white" />
                    </TouchableOpacity>
                    <TouchableOpacity
                        style={styles.headerButton}
                        onPress={handlePrintReport}
                    >
                        <Ionicons name="print-outline" size={24} color="white" />
                    </TouchableOpacity>
                </View>
            </View>

            <View style={styles.periodSelector}>
                <TouchableOpacity
                    style={[
                        styles.periodButton,
                        selectedPeriod === 'today' && styles.periodButtonActive
                    ]}
                    onPress={() => handlePeriodChange('today')}
                >
                    <Text style={[
                        styles.periodButtonText,
                        selectedPeriod === 'today' && styles.periodButtonTextActive
                    ]}>
                        {t('reports.period.today')}
                    </Text>
                </TouchableOpacity>
                <TouchableOpacity
                    style={[
                        styles.periodButton,
                        selectedPeriod === 'week' && styles.periodButtonActive
                    ]}
                    onPress={() => handlePeriodChange('week')}
                >
                    <Text style={[
                        styles.periodButtonText,
                        selectedPeriod === 'week' && styles.periodButtonTextActive
                    ]}>
                        {t('reports.period.week')}
                    </Text>
                </TouchableOpacity>
                <TouchableOpacity
                    style={[
                        styles.periodButton,
                        selectedPeriod === 'month' && styles.periodButtonActive
                    ]}
                    onPress={() => handlePeriodChange('month')}
                >
                    <Text style={[
                        styles.periodButtonText,
                        selectedPeriod === 'month' && styles.periodButtonTextActive
                    ]}>
                        {t('reports.period.month')}
                    </Text>
                </TouchableOpacity>
            </View>

            <ScrollView style={styles.content}>
                <View style={styles.summaryCard}>
                    <Text style={styles.summaryTitle}>{t('reports.summary')}</Text>
                    <View style={styles.summaryRow}>
                        <Text style={styles.summaryLabel}>{t('reports.total_sales')}</Text>
                        <Text style={styles.summaryValue}>{summary.totalSales.toFixed(2)}€</Text>
                    </View>
                    <View style={styles.summaryRow}>
                        <Text style={styles.summaryLabel}>{t('reports.total_transactions')}</Text>
                        <Text style={styles.summaryValue}>{summary.totalTransactions}</Text>
                    </View>
                    <View style={styles.summaryRow}>
                        <Text style={styles.summaryLabel}>{t('reports.average_transaction')}</Text>
                        <Text style={styles.summaryValue}>{summary.averageTransaction.toFixed(2)}€</Text>
                    </View>
                </View>

                <View style={styles.taxCard}>
                    <Text style={styles.taxTitle}>{t('reports.tax_details')}</Text>
                    <View style={styles.taxRow}>
                        <Text style={styles.taxLabel}>{t('tax.standard')}</Text>
                        <Text style={styles.taxValue}>{summary.taxDetails.standard.toFixed(2)}€</Text>
                    </View>
                    <View style={styles.taxRow}>
                        <Text style={styles.taxLabel}>{t('tax.reduced')}</Text>
                        <Text style={styles.taxValue}>{summary.taxDetails.reduced.toFixed(2)}€</Text>
                    </View>
                    <View style={styles.taxRow}>
                        <Text style={styles.taxLabel}>{t('tax.special')}</Text>
                        <Text style={styles.taxValue}>{summary.taxDetails.special.toFixed(2)}€</Text>
                    </View>
                </View>
            </ScrollView>
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#f5f5f5',
    },
    header: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: 20,
        backgroundColor: '#007AFF',
    },
    headerText: {
        fontSize: 20,
        fontWeight: 'bold',
        color: 'white',
    },
    headerButtons: {
        flexDirection: 'row',
    },
    headerButton: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: 'rgba(255, 255, 255, 0.2)',
        justifyContent: 'center',
        alignItems: 'center',
        marginLeft: 10,
    },
    periodSelector: {
        flexDirection: 'row',
        padding: 20,
        backgroundColor: 'white',
        borderBottomWidth: 1,
        borderBottomColor: '#ddd',
    },
    periodButton: {
        flex: 1,
        paddingVertical: 10,
        paddingHorizontal: 15,
        borderRadius: 5,
        marginHorizontal: 5,
        backgroundColor: '#f5f5f5',
    },
    periodButtonActive: {
        backgroundColor: '#007AFF',
    },
    periodButtonText: {
        textAlign: 'center',
        color: '#666',
        fontWeight: 'bold',
    },
    periodButtonTextActive: {
        color: 'white',
    },
    content: {
        flex: 1,
        padding: 20,
    },
    summaryCard: {
        backgroundColor: 'white',
        borderRadius: 10,
        padding: 20,
        marginBottom: 20,
        shadowColor: '#000',
        shadowOffset: {
            width: 0,
            height: 1,
        },
        shadowOpacity: 0.2,
        shadowRadius: 1.41,
        elevation: 2,
    },
    summaryTitle: {
        fontSize: 18,
        fontWeight: 'bold',
        marginBottom: 15,
    },
    summaryRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        marginBottom: 10,
    },
    summaryLabel: {
        fontSize: 16,
        color: '#666',
    },
    summaryValue: {
        fontSize: 16,
        fontWeight: 'bold',
    },
    taxCard: {
        backgroundColor: 'white',
        borderRadius: 10,
        padding: 20,
        shadowColor: '#000',
        shadowOffset: {
            width: 0,
            height: 1,
        },
        shadowOpacity: 0.2,
        shadowRadius: 1.41,
        elevation: 2,
    },
    taxTitle: {
        fontSize: 18,
        fontWeight: 'bold',
        marginBottom: 15,
    },
    taxRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        marginBottom: 10,
    },
    taxLabel: {
        fontSize: 16,
        color: '#666',
    },
    taxValue: {
        fontSize: 16,
        fontWeight: 'bold',
    },
}); 