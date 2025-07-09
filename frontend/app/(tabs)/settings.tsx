import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Switch, Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import LanguageSelector from '../../components/LanguageSelector';
import PrinterSettings from '../../components/PrinterSettings';
import { Colors, Spacing, BorderRadius, Typography } from '../../constants/Colors';
import AsyncStorage from '@react-native-async-storage/async-storage';

interface CashierSettings {
    language: string;
    theme: 'light' | 'dark' | 'system';
    notifications: boolean;
    printerSettings: {
        enabled: boolean;
        model: string;
        paperSize: string;
        autoCut: boolean;
        printLogo: boolean;
        printTaxDetails: boolean;
        footerText: string;
    };
    tseSettings: {
        enabled: boolean;
        connected: boolean;
        deviceId: string;
    };
}

export default function SettingsScreen() {
    const { t, i18n } = useTranslation();
    const { user, logout } = useAuth();
    const [settings, setSettings] = useState<CashierSettings>({
        language: 'de',
        theme: 'system',
        notifications: true,
        printerSettings: {
            enabled: false,
            model: 'EPSON TM-T88VI',
            paperSize: '80mm',
            autoCut: true,
            printLogo: true,
            printTaxDetails: true,
            footerText: 'Vielen Dank für Ihren Einkauf!'
        },
        tseSettings: {
            enabled: true,
            connected: false,
            deviceId: ''
        }
    });

    const [showLanguageSelector, setShowLanguageSelector] = useState(false);
    const [showPrinterSettings, setShowPrinterSettings] = useState(false);

    useEffect(() => {
        // Mevcut dil ayarını yükle
        setSettings(prev => ({ ...prev, language: i18n.language }));
    }, [i18n.language]);

    const handleLogout = async () => {
        console.log('Settings: Logout button pressed'); // Debug log
        
        try {
            console.log('Settings: Calling logout function directly...'); // Debug log
            await logout();
            console.log('Settings: Logout completed'); // Debug log
        } catch (error) {
            console.error('Settings: Logout error:', error); // Debug log
        }
    };

    const handleLanguageChange = (languageCode: string) => {
        i18n.changeLanguage(languageCode);
        setSettings(prev => ({ ...prev, language: languageCode }));
    };

    const handleThemeChange = () => {
        Alert.alert('Theme', 'Theme selection will be available in the next update.');
    };

    const handlePrinterSettingsSave = (printerConfig: any) => {
        setSettings(prev => ({
            ...prev,
            printerSettings: { ...prev.printerSettings, ...printerConfig }
        }));
    };

    const handleTseSettings = () => {
        Alert.alert('TSE Settings', 'TSE device settings will be available in the next update.');
    };

    const handleAbout = () => {
        Alert.alert(
            'About Registrierkasse',
            'Version 1.0.0\n\nRKSV Compliant Cash Register System\n\n© 2024 Registrierkasse GmbH'
        );
    };

    return (
        <ScrollView style={styles.container}>
            {/* User Info Section */}
            <View style={styles.section}>
                <Text style={styles.sectionTitle}>Cashier Information</Text>
                <View style={styles.userInfo}>
                    <View style={styles.userAvatar}>
                        <Ionicons name="person" size={32} color={Colors.light.primary} />
                    </View>
                    <View style={styles.userDetails}>
                        <Text style={styles.userName}>{user?.firstName} {user?.lastName}</Text>
                        <Text style={styles.userEmail}>{user?.email}</Text>
                        <Text style={styles.userRole}>Cashier</Text>
                    </View>
                </View>
            </View>

            {/* General Settings */}
            <View style={styles.section}>
                <Text style={styles.sectionTitle}>General Settings</Text>
                
                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={() => setShowLanguageSelector(true)}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="language-outline" size={24} color={Colors.light.textSecondary} />
                        <Text style={styles.settingLabel}>{t('settings.language')}</Text>
                    </View>
                    <View style={styles.settingValue}>
                        <Text style={styles.settingValueText}>
                            {settings.language === 'de' ? 'Deutsch' : 
                             settings.language === 'en' ? 'English' : 'Türkçe'}
                        </Text>
                        <Ionicons name="chevron-forward" size={20} color={Colors.light.textSecondary} />
                    </View>
                </TouchableOpacity>

                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={handleThemeChange}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="color-palette-outline" size={24} color={Colors.light.textSecondary} />
                        <Text style={styles.settingLabel}>{t('settings.theme')}</Text>
                    </View>
                    <View style={styles.settingValue}>
                        <Text style={styles.settingValueText}>{t(`settings.themes.${settings.theme}`)}</Text>
                        <Ionicons name="chevron-forward" size={20} color={Colors.light.textSecondary} />
                    </View>
                </TouchableOpacity>

                <View style={styles.settingItem}>
                    <View style={styles.settingInfo}>
                        <Ionicons name="notifications-outline" size={24} color={Colors.light.textSecondary} />
                        <Text style={styles.settingLabel}>{t('settings.notifications')}</Text>
                    </View>
                    <Switch
                        value={settings.notifications}
                        onValueChange={(value) => setSettings({ ...settings, notifications: value })}
                        trackColor={{ false: Colors.light.border, true: Colors.light.primary + '40' }}
                        thumbColor={settings.notifications ? Colors.light.primary : Colors.light.textSecondary}
                    />
                </View>
            </View>

            {/* Hardware Settings */}
            <View style={styles.section}>
                <Text style={styles.sectionTitle}>Hardware Settings</Text>
                
                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={() => setShowPrinterSettings(true)}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="print-outline" size={24} color={Colors.light.textSecondary} />
                        <Text style={styles.settingLabel}>{t('settings.printer')}</Text>
                    </View>
                    <View style={styles.settingValue}>
                        <Text style={styles.settingValueText}>
                            {settings.printerSettings.enabled ? settings.printerSettings.model : 'Disabled'}
                        </Text>
                        <Ionicons name="chevron-forward" size={20} color={Colors.light.textSecondary} />
                    </View>
                </TouchableOpacity>

                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={handleTseSettings}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="hardware-chip-outline" size={24} color={Colors.light.textSecondary} />
                        <Text style={styles.settingLabel}>TSE Device</Text>
                    </View>
                    <View style={styles.settingValue}>
                        <View style={styles.tseStatus}>
                            <View style={[
                                styles.statusIndicator, 
                                { backgroundColor: settings.tseSettings.connected ? Colors.light.success : Colors.light.error }
                            ]} />
                            <Text style={styles.settingValueText}>
                                {settings.tseSettings.connected ? 'Connected' : 'Disconnected'}
                            </Text>
                        </View>
                        <Ionicons name="chevron-forward" size={20} color={Colors.light.textSecondary} />
                    </View>
                </TouchableOpacity>
            </View>

            {/* About */}
            <View style={styles.section}>
                <TouchableOpacity
                    style={styles.settingItem}
                    onPress={handleAbout}
                >
                    <View style={styles.settingInfo}>
                        <Ionicons name="information-circle-outline" size={24} color={Colors.light.textSecondary} />
                        <Text style={styles.settingLabel}>About</Text>
                    </View>
                    <Ionicons name="chevron-forward" size={20} color={Colors.light.textSecondary} />
                </TouchableOpacity>
            </View>

            {/* Logout Button */}
            <TouchableOpacity
                style={styles.logoutButton}
                onPress={handleLogout}
            >
                <Ionicons name="log-out-outline" size={24} color="white" />
                <Text style={styles.logoutButtonText}>Logout</Text>
            </TouchableOpacity>

            {/* Modals */}
            <LanguageSelector
                visible={showLanguageSelector}
                onClose={() => setShowLanguageSelector(false)}
                onLanguageChange={handleLanguageChange}
                currentLanguage={settings.language}
            />

            <PrinterSettings
                visible={showPrinterSettings}
                onClose={() => setShowPrinterSettings(false)}
                onSave={handlePrinterSettingsSave}
                currentSettings={settings.printerSettings}
            />
        </ScrollView>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: Colors.light.background,
    },
    section: {
        backgroundColor: Colors.light.surface,
        marginBottom: Spacing.md,
        borderRadius: BorderRadius.md,
        overflow: 'hidden',
    },
    sectionTitle: {
        ...Typography.h3,
        color: Colors.light.text,
        padding: Spacing.md,
        paddingBottom: Spacing.sm,
        borderBottomWidth: 1,
        borderBottomColor: Colors.light.border,
    },
    userInfo: {
        flexDirection: 'row',
        alignItems: 'center',
        padding: Spacing.md,
    },
    userAvatar: {
        width: 60,
        height: 60,
        borderRadius: 30,
        backgroundColor: Colors.light.primary + '20',
        justifyContent: 'center',
        alignItems: 'center',
        marginRight: Spacing.md,
    },
    userDetails: {
        flex: 1,
    },
    userName: {
        ...Typography.h3,
        color: Colors.light.text,
        marginBottom: Spacing.xs,
    },
    userEmail: {
        ...Typography.bodySmall,
        color: Colors.light.textSecondary,
        marginBottom: Spacing.xs,
    },
    userRole: {
        ...Typography.bodySmall,
        color: Colors.light.primary,
        fontWeight: '600',
    },
    settingItem: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: Spacing.md,
        borderBottomWidth: 1,
        borderBottomColor: Colors.light.border,
    },
    settingInfo: {
        flexDirection: 'row',
        alignItems: 'center',
        flex: 1,
    },
    settingLabel: {
        ...Typography.body,
        color: Colors.light.text,
        marginLeft: Spacing.sm,
    },
    settingValue: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    settingValueText: {
        ...Typography.bodySmall,
        color: Colors.light.textSecondary,
        marginRight: Spacing.xs,
    },
    tseStatus: {
        flexDirection: 'row',
        alignItems: 'center',
        marginRight: Spacing.xs,
    },
    statusIndicator: {
        width: 8,
        height: 8,
        borderRadius: 4,
        marginRight: Spacing.xs,
    },
    logoutButton: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: Colors.light.error,
        margin: Spacing.md,
        padding: Spacing.md,
        borderRadius: BorderRadius.md,
        gap: Spacing.sm,
    },
    logoutButtonText: {
        ...Typography.button,
        color: 'white',
        fontWeight: '600',
    },
}); 