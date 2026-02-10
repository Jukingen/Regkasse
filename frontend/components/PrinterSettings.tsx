import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Modal,
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Switch,
  TextInput,
  Alert,
  ScrollView,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';

interface PrinterSettingsProps {
  visible: boolean;
  onClose: () => void;
  onSave: (settings: PrinterConfig) => void;
  currentSettings: PrinterConfig;
}

interface PrinterConfig {
  enabled: boolean;
  model: string;
  paperSize: string;
  autoCut: boolean;
  printLogo: boolean;
  printTaxDetails: boolean;
  footerText: string;
}

const PrinterSettings: React.FC<PrinterSettingsProps> = ({
  visible,
  onClose,
  onSave,
  currentSettings,
}) => {
  const { t } = useTranslation();
  const [settings, setSettings] = useState<PrinterConfig>(currentSettings);
  const [isConnecting, setIsConnecting] = useState(false);

  const printerModels = [
    'EPSON TM-T88VI',
    'Star TSP 700',
    'Citizen CT-S310II',
    'Custom Printer',
  ];

  const paperSizes = ['80mm', '58mm', '112mm'];

  const handleConnect = async () => {
    setIsConnecting(true);
    try {
      // Simulate printer connection
      await new Promise(resolve => setTimeout(resolve, 2000));
      Alert.alert(t('printer.success', 'Success'), t('printer.connected', 'Printer connected successfully!'));
    } catch (error) {
      Alert.alert(t('printer.error', 'Error'), t('printer.connectFailed', 'Failed to connect to printer. Please check the connection.'));
    } finally {
      setIsConnecting(false);
    }
  };

  const handleSave = () => {
    onSave(settings);
    onClose();
    Alert.alert(t('printer.success', 'Success'), t('printer.saved', 'Printer settings saved successfully!'));
  };

  const handleTestPrint = () => {
    Alert.alert(t('printer.testPrint', 'Test Print'), t('printer.testPrintMsg', 'Test receipt will be printed. Please check your printer.'));
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={onClose}
    >
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.title}>{t('printer.title', 'Printer Settings')}</Text>
          <TouchableOpacity onPress={onClose} style={styles.closeButton}>
            <Ionicons name="close" size={24} color={Colors.light.text} />
          </TouchableOpacity>
        </View>

        <ScrollView style={styles.content} showsVerticalScrollIndicator={false}>
          {/* Connection Status */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>{t('printer.connectionStatus', 'Connection Status')}</Text>
            <View style={styles.statusCard}>
              <View style={styles.statusInfo}>
                <Ionicons 
                  name={settings.enabled ? "checkmark-circle" : "close-circle"} 
                  size={24} 
                  color={settings.enabled ? Colors.light.success : Colors.light.error} 
                />
                <Text style={styles.statusText}>
                  {settings.enabled ? t('printer.connected', 'Connected') : t('printer.disconnected', 'Disconnected')}
                </Text>
              </View>
              <TouchableOpacity
                style={[styles.connectButton, isConnecting && styles.connectButtonDisabled]}
                onPress={handleConnect}
                disabled={isConnecting}
              >
                <Text style={styles.connectButtonText}>
                  {isConnecting ? t('printer.connecting', 'Connecting...') : t('printer.connect', 'Connect')}
                </Text>
              </TouchableOpacity>
            </View>
          </View>

          {/* Printer Configuration */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>{t('printer.config', 'Printer Configuration')}</Text>
            
            <View style={styles.settingItem}>
              <Text style={styles.settingLabel}>{t('printer.enable', 'Enable Printer')}</Text>
              <Switch
                value={settings.enabled}
                onValueChange={(value) => setSettings({ ...settings, enabled: value })}
              />
            </View>

            <View style={styles.settingItem}>
              <Text style={styles.settingLabel}>{t('printer.model', 'Printer Model')}</Text>
              <View style={styles.dropdown}>
                <Text style={styles.dropdownText}>{settings.model}</Text>
                <Ionicons name="chevron-down" size={20} color={Colors.light.textSecondary} />
              </View>
            </View>

            <View style={styles.settingItem}>
              <Text style={styles.settingLabel}>{t('printer.paperSize', 'Paper Size')}</Text>
              <View style={styles.dropdown}>
                <Text style={styles.dropdownText}>{settings.paperSize}</Text>
                <Ionicons name="chevron-down" size={20} color={Colors.light.textSecondary} />
              </View>
            </View>

            <View style={styles.settingItem}>
              <Text style={styles.settingLabel}>{t('printer.autoCut', 'Auto Cut')}</Text>
              <Switch
                value={settings.autoCut}
                onValueChange={(value) => setSettings({ ...settings, autoCut: value })}
              />
            </View>
          </View>

          {/* Receipt Settings */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>{t('printer.receiptSettings', 'Receipt Settings')}</Text>
            
            <View style={styles.settingItem}>
              <Text style={styles.settingLabel}>{t('printer.printLogo', 'Print Logo')}</Text>
              <Switch
                value={settings.printLogo}
                onValueChange={(value) => setSettings({ ...settings, printLogo: value })}
              />
            </View>

            <View style={styles.settingItem}>
              <Text style={styles.settingLabel}>{t('printer.printTaxDetails', 'Print Tax Details')}</Text>
              <Switch
                value={settings.printTaxDetails}
                onValueChange={(value) => setSettings({ ...settings, printTaxDetails: value })}
              />
            </View>

            <View style={styles.settingItem}>
              <Text style={styles.settingLabel}>{t('printer.footerText', 'Footer Text')}</Text>
              <TextInput
                style={styles.textInput}
                value={settings.footerText}
                onChangeText={(text) => setSettings({ ...settings, footerText: text })}
                placeholder={t('printer.footerTextPlaceholder', 'Enter footer text...')}
                multiline
                numberOfLines={2}
              />
            </View>
          </View>

          {/* Test Print */}
          <View style={styles.section}>
            <TouchableOpacity style={styles.testButton} onPress={handleTestPrint}>
              <Ionicons name="print-outline" size={20} color="white" />
              <Text style={styles.testButtonText}>{t('printer.testPrint', 'Test Print')}</Text>
            </TouchableOpacity>
          </View>
        </ScrollView>

        {/* Save Button */}
        <View style={styles.footer}>
          <TouchableOpacity style={styles.saveButton} onPress={handleSave}>
            <Ionicons name="checkmark" size={20} color="white" />
            <Text style={styles.saveButtonText}>{t('printer.saveSettings', 'Save Settings')}</Text>
          </TouchableOpacity>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  title: {
    ...Typography.h2,
    color: Colors.light.text,
  },
  closeButton: {
    padding: Spacing.xs,
  },
  content: {
    flex: 1,
    padding: Spacing.md,
  },
  section: {
    marginBottom: Spacing.lg,
  },
  sectionTitle: {
    ...Typography.h3,
    color: Colors.light.text,
    marginBottom: Spacing.sm,
  },
  statusCard: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
  },
  statusInfo: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  statusText: {
    ...Typography.body,
    color: Colors.light.text,
    marginLeft: Spacing.sm,
  },
  connectButton: {
    backgroundColor: Colors.light.primary,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    borderRadius: BorderRadius.sm,
  },
  connectButtonDisabled: {
    backgroundColor: Colors.light.textSecondary,
  },
  connectButtonText: {
    ...Typography.bodySmall,
    color: 'white',
    fontWeight: '600',
  },
  settingItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: Spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  settingLabel: {
    ...Typography.body,
    color: Colors.light.text,
    flex: 1,
  },
  dropdown: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: Spacing.sm,
    paddingVertical: Spacing.xs,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.sm,
    minWidth: 120,
  },
  dropdownText: {
    ...Typography.bodySmall,
    color: Colors.light.text,
    marginRight: Spacing.xs,
  },
  textInput: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.sm,
    padding: Spacing.sm,
    minHeight: 60,
    textAlignVertical: 'top',
    ...Typography.bodySmall,
    color: Colors.light.text,
  },
  testButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.info,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    gap: Spacing.sm,
  },
  testButtonText: {
    ...Typography.button,
    color: 'white',
    fontWeight: '600',
  },
  footer: {
    padding: Spacing.md,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  saveButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.success,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    gap: Spacing.sm,
  },
  saveButtonText: {
    ...Typography.button,
    color: 'white',
    fontWeight: '600',
  },
});

export default PrinterSettings; 