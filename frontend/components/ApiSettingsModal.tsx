import React, { useState, useEffect } from 'react';
import {
  Modal,
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  Alert,
  ScrollView
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { setApiServerIP } from '../services/api/config';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { getLocalIPInstructions, getTestIPs, isValidIPAddress, findWorkingIP } from '../utils/networkUtils';

interface ApiSettingsModalProps {
  visible: boolean;
  onClose: () => void;
}

export const ApiSettingsModal: React.FC<ApiSettingsModalProps> = ({
  visible,
  onClose
}) => {
  const { t } = useTranslation(['settings']);
  const [ipAddress, setIpAddress] = useState('192.168.1.100');
  const [port, setPort] = useState('5184');

  useEffect(() => {
    loadCurrentSettings();
  }, [visible]);

  const loadCurrentSettings = async () => {
    try {
      const savedIP = await AsyncStorage.getItem('api_server_ip');
      if (savedIP) {
        setIpAddress(savedIP);
      }
    } catch (error) {
      console.log('Error loading API settings:', error);
    }
  };

  const saveSettings = async () => {
    if (!isValidIPAddress(ipAddress)) {
      Alert.alert(
        t('settings:apiServer.invalidIpTitle'),
        t('settings:apiServer.invalidIpMessage')
      );
      return;
    }

    try {
      await setApiServerIP(ipAddress);
      Alert.alert(
        t('settings:apiServer.saveSuccessTitle'),
        t('settings:apiServer.saveSuccessMessage'),
        [{ text: t('settings:apiServer.ok'), onPress: onClose }]
      );
    } catch {
      Alert.alert(
        t('settings:apiServer.saveFailedTitle'),
        t('settings:apiServer.saveFailedMessage')
      );
    }
  };

  const getCurrentIP = async () => {
    try {
      const instructions = getLocalIPInstructions();
      Alert.alert(t('settings:apiServer.findIpTitle'), instructions);
    } catch {
      Alert.alert(
        t('settings:apiServer.findIpFailedTitle'),
        t('settings:apiServer.findIpFailedMessage')
      );
    }
  };

  const testIPAddress = async (ip: string) => {
    setIpAddress(ip);
  };

  const autoFindIP = async () => {
    try {
      Alert.alert(
        t('settings:apiServer.searchingTitle'),
        t('settings:apiServer.searchingMessage')
      );

      const workingIP = await findWorkingIP();
      if (workingIP) {
        setIpAddress(workingIP);
        Alert.alert(
          t('settings:apiServer.autoFindSuccessTitle'),
          t('settings:apiServer.autoFindSuccessMessage', { ip: workingIP })
        );
      } else {
        Alert.alert(
          t('settings:apiServer.autoFindFailedTitle'),
          t('settings:apiServer.autoFindFailedMessage')
        );
      }
    } catch {
      Alert.alert(
        t('settings:apiServer.autoFindErrorTitle'),
        t('settings:apiServer.autoFindErrorMessage')
      );
    }
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent={true}
      onRequestClose={onClose}
    >
      <View style={styles.centeredView}>
        <View style={styles.modalView}>
          <Text style={styles.title}>{t('settings:apiServer.title')}</Text>

          <ScrollView style={styles.scrollView}>
            <Text style={styles.label}>{t('settings:apiServer.serverIpLabel')}</Text>
            <TextInput
              style={styles.input}
              value={ipAddress}
              onChangeText={setIpAddress}
              placeholder="192.168.1.100"
              keyboardType="numeric"
              autoCapitalize="none"
            />

            <Text style={styles.label}>{t('settings:apiServer.portLabel')}</Text>
            <TextInput
              style={styles.input}
              value={port}
              onChangeText={setPort}
              placeholder="5184"
              keyboardType="numeric"
              editable={false}
            />

            <TouchableOpacity style={styles.helpButton} onPress={getCurrentIP}>
              <Text style={styles.helpButtonText}>{t('settings:apiServer.findIpHelp')}</Text>
            </TouchableOpacity>

            <TouchableOpacity style={styles.autoFindButton} onPress={autoFindIP}>
              <Text style={styles.autoFindButtonText}>{t('settings:apiServer.autoFindIp')}</Text>
            </TouchableOpacity>

            <View style={styles.infoContainer}>
              <Text style={styles.infoTitle}>{t('settings:apiServer.testIpsTitle')}</Text>
              {getTestIPs().map((group, groupIndex) => (
                <View key={groupIndex} style={styles.ipGroup}>
                  <Text style={styles.ipGroupTitle}>{group.label}:</Text>
                  {group.ips.map((ip, ipIndex) => (
                    <TouchableOpacity
                      key={ipIndex}
                      style={styles.ipButton}
                      onPress={() => testIPAddress(ip)}
                    >
                      <Text style={styles.ipButtonText}>{ip}</Text>
                    </TouchableOpacity>
                  ))}
                </View>
              ))}
            </View>
          </ScrollView>

          <View style={styles.buttonContainer}>
            <TouchableOpacity style={styles.cancelButton} onPress={onClose}>
              <Text style={styles.cancelButtonText}>{t('settings:apiServer.cancel')}</Text>
            </TouchableOpacity>
            <TouchableOpacity style={styles.saveButton} onPress={saveSettings}>
              <Text style={styles.saveButtonText}>{t('settings:apiServer.save')}</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  centeredView: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
  },
  modalView: {
    width: '90%',
    maxHeight: '80%',
    backgroundColor: 'white',
    borderRadius: 20,
    padding: 20,
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.25,
    shadowRadius: 4,
    elevation: 5,
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold',
    marginBottom: 20,
    color: '#333',
  },
  scrollView: {
    width: '100%',
    maxHeight: 400,
  },
  label: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 8,
    color: '#333',
  },
  input: {
    width: '100%',
    height: 50,
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 8,
    paddingHorizontal: 15,
    marginBottom: 20,
    fontSize: 16,
    backgroundColor: '#f9f9f9',
  },
  helpButton: {
    backgroundColor: '#007AFF',
    padding: 12,
    borderRadius: 8,
    marginBottom: 20,
  },
  helpButtonText: {
    color: 'white',
    textAlign: 'center',
    fontSize: 14,
    fontWeight: '600',
  },
  autoFindButton: {
    backgroundColor: '#28a745',
    padding: 12,
    borderRadius: 8,
    marginBottom: 20,
  },
  autoFindButtonText: {
    color: 'white',
    textAlign: 'center',
    fontSize: 14,
    fontWeight: '600',
  },
  infoContainer: {
    backgroundColor: '#f0f0f0',
    padding: 15,
    borderRadius: 8,
    marginBottom: 20,
  },
  infoTitle: {
    fontSize: 14,
    fontWeight: 'bold',
    marginBottom: 8,
    color: '#333',
  },
  infoText: {
    fontSize: 12,
    color: '#666',
    marginBottom: 4,
  },
  ipGroup: {
    marginBottom: 15,
  },
  ipGroupTitle: {
    fontSize: 13,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 8,
  },
  ipButton: {
    backgroundColor: '#e9ecef',
    padding: 8,
    borderRadius: 6,
    marginBottom: 5,
    marginRight: 8,
  },
  ipButtonText: {
    fontSize: 12,
    color: '#495057',
    textAlign: 'center',
  },
  buttonContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    width: '100%',
    marginTop: 20,
  },
  cancelButton: {
    flex: 1,
    backgroundColor: '#ff3b30',
    padding: 15,
    borderRadius: 8,
    marginRight: 10,
  },
  cancelButtonText: {
    color: 'white',
    textAlign: 'center',
    fontSize: 16,
    fontWeight: '600',
  },
  saveButton: {
    flex: 1,
    backgroundColor: '#34c759',
    padding: 15,
    borderRadius: 8,
    marginLeft: 10,
  },
  saveButtonText: {
    color: 'white',
    textAlign: 'center',
    fontSize: 16,
    fontWeight: '600',
  },
});
