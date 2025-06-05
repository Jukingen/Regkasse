import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Ionicons } from '@expo/vector-icons';

interface Customer {
    id: string;
    name: string;
    taxNumber?: string;
    email?: string;
    phone?: string;
    address?: string;
    notes?: string;
}

export default function CustomersScreen() {
    const { t } = useTranslation();
    const [customers, setCustomers] = useState<Customer[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        loadCustomers();
    }, []);

    const loadCustomers = async () => {
        try {
            // TODO: API'den müşterileri yükle
            const mockCustomers: Customer[] = [
                {
                    id: '1',
                    name: 'Test Müşteri 1',
                    taxNumber: 'ATU12345678',
                    email: 'test1@example.com',
                    phone: '+43 123 456 789',
                    address: 'Wien, Österreich',
                    notes: 'VIP müşteri'
                },
                {
                    id: '2',
                    name: 'Test Müşteri 2',
                    taxNumber: 'ATU87654321',
                    email: 'test2@example.com',
                    phone: '+43 987 654 321',
                    address: 'Graz, Österreich'
                }
            ];
            setCustomers(mockCustomers);
        } catch (error) {
            Alert.alert(
                t('customers.error.title'),
                t('customers.error.load_failed')
            );
        } finally {
            setIsLoading(false);
        }
    };

    const handleAddCustomer = () => {
        // TODO: Müşteri ekleme modalını aç
        Alert.alert('Info', 'Müşteri ekleme özelliği yakında eklenecek');
    };

    const handleEditCustomer = (customer: Customer) => {
        // TODO: Müşteri düzenleme modalını aç
        Alert.alert('Info', `${customer.name} düzenleme özelliği yakında eklenecek`);
    };

    const renderCustomer = ({ item }: { item: Customer }) => (
        <TouchableOpacity
            style={styles.customerItem}
            onPress={() => handleEditCustomer(item)}
        >
            <View style={styles.customerInfo}>
                <Text style={styles.customerName}>{item.name}</Text>
                {item.taxNumber && (
                    <Text style={styles.customerDetail}>
                        {t('customers.tax_number')}: {item.taxNumber}
                    </Text>
                )}
                {item.email && (
                    <Text style={styles.customerDetail}>
                        {t('customers.email')}: {item.email}
                    </Text>
                )}
                {item.phone && (
                    <Text style={styles.customerDetail}>
                        {t('customers.phone')}: {item.phone}
                    </Text>
                )}
                {item.address && (
                    <Text style={styles.customerDetail}>
                        {t('customers.address')}: {item.address}
                    </Text>
                )}
                {item.notes && (
                    <Text style={styles.customerNotes}>{item.notes}</Text>
                )}
            </View>
            <Ionicons name="chevron-forward" size={24} color="#666" />
        </TouchableOpacity>
    );

    return (
        <View style={styles.container}>
            <View style={styles.header}>
                <Text style={styles.headerText}>{t('customers.title')}</Text>
                <TouchableOpacity
                    style={styles.addButton}
                    onPress={handleAddCustomer}
                >
                    <Ionicons name="add" size={24} color="white" />
                </TouchableOpacity>
            </View>

            <FlatList
                data={customers}
                renderItem={renderCustomer}
                keyExtractor={(item) => item.id}
                contentContainerStyle={styles.list}
                ListEmptyComponent={
                    <Text style={styles.emptyText}>
                        {isLoading ? t('customers.loading') : t('customers.empty')}
                    </Text>
                }
            />
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
    addButton: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: '#34C759',
        justifyContent: 'center',
        alignItems: 'center',
    },
    list: {
        padding: 20,
    },
    customerItem: {
        flexDirection: 'row',
        alignItems: 'center',
        padding: 15,
        backgroundColor: 'white',
        borderRadius: 10,
        marginBottom: 10,
        shadowColor: '#000',
        shadowOffset: {
            width: 0,
            height: 1,
        },
        shadowOpacity: 0.2,
        shadowRadius: 1.41,
        elevation: 2,
    },
    customerInfo: {
        flex: 1,
    },
    customerName: {
        fontSize: 16,
        fontWeight: 'bold',
        marginBottom: 4,
    },
    customerDetail: {
        fontSize: 14,
        color: '#666',
        marginBottom: 2,
    },
    customerNotes: {
        fontSize: 12,
        color: '#999',
        fontStyle: 'italic',
        marginTop: 4,
    },
    emptyText: {
        textAlign: 'center',
        fontSize: 16,
        color: '#666',
        marginTop: 20,
    },
}); 