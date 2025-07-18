import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  Alert,
  Vibration,
  Dimensions,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { barcodeScanner, BarcodeResult } from '../services/BarcodeScanner';
import { Product } from '../services/api/productService';

interface BarcodeScannerModalProps {
  visible: boolean;
  onClose: () => void;
  onProductFound: (product: Product) => void;
  products: Product[];
}

const { width: screenWidth, height: screenHeight } = Dimensions.get('window');

const BarcodeScannerModal: React.FC<BarcodeScannerModalProps> = ({
  visible,
  onClose,
  onProductFound,
  products,
}) => {
  const { t } = useTranslation();
  const [isScanning, setIsScanning] = useState(false);
  const [lastScannedCode, setLastScannedCode] = useState<string>('');
  const [scanHistory, setScanHistory] = useState<BarcodeResult[]>([]);

  useEffect(() => {
    if (visible) {
      startScanning();
    } else {
      stopScanning();
    }
  }, [visible]);

  const startScanning = async () => {
    try {
      setIsScanning(true);
      await barcodeScanner.startScanning(
        handleBarcodeScanned,
        (error) => {
          Alert.alert('Scanner Error', error);
          setIsScanning(false);
        }
      );
    } catch (error) {
      console.error('Failed to start scanner:', error);
      setIsScanning(false);
    }
  };

  const stopScanning = () => {
    barcodeScanner.stopScanning();
    setIsScanning(false);
  };

  const handleBarcodeScanned = (result: BarcodeResult) => {
    Vibration.vibrate(100);
    setLastScannedCode(result.data);
    
    // Scan history'ye ekle
    setScanHistory(prev => [result, ...prev.slice(0, 9)]);
    
    // Ürünü bul
    const foundProduct = products.find(product => 
      product.barcode === result.data
    );
    
    if (foundProduct) {
      onProductFound(foundProduct);
      Alert.alert(
        'Product Found',
        `${foundProduct.name} - €${foundProduct.price.toFixed(2)}`,
        [
          { text: 'Add to Cart', onPress: () => onClose() },
          { text: 'Scan Another', style: 'cancel' }
        ]
      );
    } else {
      Alert.alert(
        'Product Not Found',
        `No product found with barcode: ${result.data}`,
        [
          { text: 'Manual Search', onPress: () => onClose() },
          { text: 'Try Again', style: 'cancel' }
        ]
      );
    }
  };

  const handleManualInput = () => {
    Alert.prompt(
      'Manual Barcode Entry',
      'Enter barcode manually:',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Search',
          onPress: (barcode) => {
            if (barcode) {
              const foundProduct = products.find(product => 
                product.barcode === barcode
              );
              if (foundProduct) {
                onProductFound(foundProduct);
                onClose();
              } else {
                Alert.alert('Product Not Found', `No product found with barcode: ${barcode}`);
              }
            }
          }
        }
      ],
      'plain-text'
    );
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      onRequestClose={onClose}
    >
      <View style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <TouchableOpacity style={styles.closeButton} onPress={onClose}>
            <Ionicons name="close" size={24} color="white" />
          </TouchableOpacity>
          <Text style={styles.headerTitle}>Barcode Scanner</Text>
          <TouchableOpacity style={styles.manualButton} onPress={handleManualInput}>
            <Ionicons name="keyboard" size={24} color="white" />
          </TouchableOpacity>
        </View>

        {/* Scanner Area */}
        <View style={styles.scannerArea}>
          <View style={styles.scannerFrame}>
            <View style={styles.cornerTL} />
            <View style={styles.cornerTR} />
            <View style={styles.cornerBL} />
            <View style={styles.cornerBR} />
            
            <Text style={styles.scannerText}>
              {isScanning ? 'Position barcode in frame' : 'Scanner not ready'}
            </Text>
          </View>
        </View>

        {/* Status Bar */}
        <View style={styles.statusBar}>
          <View style={styles.statusItem}>
            <Ionicons 
              name={isScanning ? "radio-button-on" : "radio-button-off"} 
              size={16} 
              color={isScanning ? Colors.light.success : Colors.light.textSecondary} 
            />
            <Text style={styles.statusText}>
              {isScanning ? 'Scanning' : 'Ready'}
            </Text>
          </View>
          
          {lastScannedCode && (
            <View style={styles.statusItem}>
              <Ionicons name="barcode" size={16} color={Colors.light.primary} />
              <Text style={styles.statusText} numberOfLines={1}>
                Last: {lastScannedCode}
              </Text>
            </View>
          )}
        </View>

        {/* Scan History */}
        {scanHistory.length > 0 && (
          <View style={styles.historySection}>
            <Text style={styles.historyTitle}>Recent Scans</Text>
            <View style={styles.historyList}>
              {scanHistory.slice(0, 3).map((scan, index) => (
                <View key={index} style={styles.historyItem}>
                  <Text style={styles.historyCode}>{scan.data}</Text>
                  <Text style={styles.historyTime}>
                    {scan.timestamp.toLocaleTimeString()}
                  </Text>
                </View>
              ))}
            </View>
          </View>
        )}

        {/* Instructions */}
        <View style={styles.instructions}>
          <Text style={styles.instructionText}>
            • Hold barcode steady in the frame{'\n'}
            • Ensure good lighting{'\n'}
            • Try different angles if needed
          </Text>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#000',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.lg,
    paddingTop: Spacing.xl,
  },
  closeButton: {
    padding: Spacing.sm,
  },
  headerTitle: {
    ...Typography.h3,
    color: 'white',
    fontWeight: '600',
  },
  manualButton: {
    padding: Spacing.sm,
  },
  scannerArea: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: Spacing.xl,
  },
  scannerFrame: {
    width: screenWidth * 0.8,
    height: screenWidth * 0.8,
    borderWidth: 2,
    borderColor: 'rgba(255, 255, 255, 0.3)',
    borderRadius: BorderRadius.lg,
    justifyContent: 'center',
    alignItems: 'center',
    position: 'relative',
  },
  cornerTL: {
    position: 'absolute',
    top: -2,
    left: -2,
    width: 30,
    height: 30,
    borderTopWidth: 4,
    borderLeftWidth: 4,
    borderColor: Colors.light.primary,
  },
  cornerTR: {
    position: 'absolute',
    top: -2,
    right: -2,
    width: 30,
    height: 30,
    borderTopWidth: 4,
    borderRightWidth: 4,
    borderColor: Colors.light.primary,
  },
  cornerBL: {
    position: 'absolute',
    bottom: -2,
    left: -2,
    width: 30,
    height: 30,
    borderBottomWidth: 4,
    borderLeftWidth: 4,
    borderColor: Colors.light.primary,
  },
  cornerBR: {
    position: 'absolute',
    bottom: -2,
    right: -2,
    width: 30,
    height: 30,
    borderBottomWidth: 4,
    borderRightWidth: 4,
    borderColor: Colors.light.primary,
  },
  scannerText: {
    color: 'white',
    textAlign: 'center',
    fontSize: 16,
  },
  statusBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    padding: Spacing.md,
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
  },
  statusItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.xs,
  },
  statusText: {
    color: 'white',
    fontSize: 14,
  },
  historySection: {
    padding: Spacing.md,
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
  },
  historyTitle: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
    marginBottom: Spacing.sm,
  },
  historyList: {
    gap: Spacing.xs,
  },
  historyItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.sm,
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    borderRadius: BorderRadius.sm,
  },
  historyCode: {
    color: 'white',
    fontSize: 14,
    fontFamily: 'monospace',
  },
  historyTime: {
    color: Colors.light.textSecondary,
    fontSize: 12,
  },
  instructions: {
    padding: Spacing.lg,
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
  },
  instructionText: {
    color: 'white',
    fontSize: 14,
    lineHeight: 20,
    textAlign: 'center',
  },
});

export default BarcodeScannerModal; 