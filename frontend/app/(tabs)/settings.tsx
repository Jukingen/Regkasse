import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Switch, Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';

interface UserSettings {
    language: string;
    theme: 'light' | 'dark' | 'system';
    notifications: boolean;
    printerSettings: {
        enabled: boolean;
        model: string;
        paperSize: string;
    };
    receiptSettings: {
        showLogo: boolean;
        showTaxDetails: boolean;
        footerText: string;
    };
}

export default function SettingsScreen() {
    const { t } = useTranslation();
    const { user, logout } = useAuth();
    const [settings, setSettings] = useState<UserSettings>({
        language: 'de-DE',
        theme: 'system',
        notifications: true,
        printerSettings: {
            enabled: false,
            model: 'EPSON TM-T88VI',
            paperSize: '80mm'
        },
        receiptSettings: {
            showLogo: true,
            showTaxDetails: true,
            footerText: 'Vielen Dank für Ihren Einkauf!'
        }
    });

    const handleLogout = async () => {
        try {
            await logout();
            router.replace('/(auth)/login');
        } catch (error) {
            Alert.alert(
                t('settings.error.title'),
                t('settings.error.logout_failed')
            );
        }
    };

    const handleLanguageChange = () => {
        // TODO: Dil değiştirme modalını aç
        Alert.alert('Info', 'Dil değiştirme özelliği yakında eklenecek');
    };

    const handleThemeChange = () => {
        // TODO: Tema değiştirme modalını aç
        Alert.alert('Info', 'Tema değiştirme özelliği yakında eklenecek');
    };

    const handlePrinterSettings = () => {
        // TODO: Yazıcı ayarları modalını aç
        Alert.alert('Info', 'Yazıcı ayarları özelliği yakında eklenecek');
    };

    const handleReceiptSettings = () => {
        // TODO: Fiş ayarları modalını aç
        Alert.alert('Info', 'Fiş ayarları özelliği yakında eklenecek');
    };

    const handleTseSettings = () => {
        // TODO: TSE ayarları modalını aç
        Alert.alert('Info', 'TSE ayarları özelliği yakında eklenecek');
    };

    return (
        <ScrollView style={styles.container}>
            <View style={styles.section}>
                <Text style={styles.sectionTitle}>{t('settings.user')}</Text>
                <View style={styles.userInfo}>
                    <Text style={styles.userName}>{user?.username}</Text>
                    <Text style={styles.userRole}>{t(`roles.${user?.role}`)}</Text>
                </View>
            </View>

            <View style={styles.section}>
                <Text style={styles.sectionTitle}>{t('settings.general')}</Text>
                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={handleLanguageChange}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="language-outline" size={24} color="#666" />
                        <Text style={styles.settingLabel}>{t('settings.language')}</Text>
                    </View>
                    <View style={styles.settingValue}>
                        <Text style={styles.settingValueText}>{t(`languages.${settings.language}`)}</Text>
                        <Ionicons name="chevron-forward" size={24} color="#666" />
                    </View>
                </TouchableOpacity>

                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={handleThemeChange}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="color-palette-outline" size={24} color="#666" />
                        <Text style={styles.settingLabel}>{t('settings.theme')}</Text>
                    </View>
                    <View style={styles.settingValue}>
                        <Text style={styles.settingValueText}>{t(`themes.${settings.theme}`)}</Text>
                        <Ionicons name="chevron-forward" size={24} color="#666" />
                    </View>
                </TouchableOpacity>

                <View style={styles.settingItem}>
                    <View style={styles.settingInfo}>
                        <Ionicons name="notifications-outline" size={24} color="#666" />
                        <Text style={styles.settingLabel}>{t('settings.notifications')}</Text>
                    </View>
                    <Switch
                        value={settings.notifications}
                        onValueChange={(value) => setSettings({ ...settings, notifications: value })}
                    />
                </View>
            </View>

            <View style={styles.section}>
                <Text style={styles.sectionTitle}>{t('settings.hardware')}</Text>
                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={handlePrinterSettings}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="print-outline" size={24} color="#666" />
                        <Text style={styles.settingLabel}>{t('settings.printer')}</Text>
                    </View>
                    <View style={styles.settingValue}>
                        <Text style={styles.settingValueText}>
                            {settings.printerSettings.enabled ? settings.printerSettings.model : t('settings.disabled')}
                        </Text>
                        <Ionicons name="chevron-forward" size={24} color="#666" />
                    </View>
                </TouchableOpacity>

                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={handleTseSettings}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="hardware-chip-outline" size={24} color="#666" />
                        <Text style={styles.settingLabel}>{t('settings.tse')}</Text>
                    </View>
                    <View style={styles.settingValue}>
                        <Text style={styles.settingValueText}>{t('settings.configure')}</Text>
                        <Ionicons name="chevron-forward" size={24} color="#666" />
                    </View>
                </TouchableOpacity>
            </View>

            <View style={styles.section}>
                <Text style={styles.sectionTitle}>{t('settings.receipt')}</Text>
                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={handleReceiptSettings}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="receipt-outline" size={24} color="#666" />
                        <Text style={styles.settingLabel}>{t('settings.receipt_settings')}</Text>
                    </View>
                    <View style={styles.settingValue}>
                        <Text style={styles.settingValueText}>{t('settings.configure')}</Text>
                        <Ionicons name="chevron-forward" size={24} color="#666" />
                    </View>
                </TouchableOpacity>
            </View>

            <TouchableOpacity
                style={styles.logoutButton}
                onPress={handleLogout}
            >
                <Ionicons name="log-out-outline" size={24} color="white" />
                <Text style={styles.logoutButtonText}>{t('settings.logout')}</Text>
            </TouchableOpacity>
        </ScrollView>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#f5f5f5',
    },
    section: {
        backgroundColor: 'white',
        marginBottom: 20,
        paddingVertical: 10,
    },
    sectionTitle: {
        fontSize: 16,
        fontWeight: 'bold',
        color: '#666',
        marginLeft: 20,
        marginBottom: 10,
    },
    userInfo: {
        padding: 20,
        borderBottomWidth: 1,
        borderBottomColor: '#ddd',
    },
    userName: {
        fontSize: 18,
        fontWeight: 'bold',
        marginBottom: 4,
    },
    userRole: {
        fontSize: 14,
        color: '#666',
    },
    settingItem: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: 15,
        borderBottomWidth: 1,
        borderBottomColor: '#ddd',
    },
    settingInfo: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    settingLabel: {
        fontSize: 16,
        marginLeft: 10,
    },
    settingValue: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    settingValueText: {
        fontSize: 14,
        color: '#666',
        marginRight: 5,
    },
    logoutButton: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: '#FF3B30',
        margin: 20,
        padding: 15,
        borderRadius: 10,
    },
    logoutButtonText: {
        color: 'white',
        fontSize: 16,
        fontWeight: 'bold',
        marginLeft: 8,
    },
}); 