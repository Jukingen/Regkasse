import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Modal,
  Alert,
  Vibration,
  Dimensions,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';

interface VoiceCommandsProps {
  visible: boolean;
  onClose: () => void;
  onCommand: (command: string, params?: any) => void;
}

const { width: screenWidth } = Dimensions.get('window');

// Sesli komut tanımları
const VOICE_COMMANDS = {
  // Ürün komutları
  'add product': {
    description: t('voice.addProduct', 'Add product to cart'),
    examples: [t('voice.addCoffee', 'add coffee'), t('voice.addBread', 'add bread'), t('voice.addMilk', 'add milk')],
    action: 'add_product'
  },
  'remove product': {
    description: t('voice.removeProduct', 'Remove product from cart'),
    examples: [t('voice.removeCoffee', 'remove coffee'), t('voice.removeBread', 'remove bread')],
    action: 'remove_product'
  },
  'clear cart': {
    description: t('voice.clearCart', 'Clear shopping cart'),
    examples: [t('voice.clearCart1', 'clear cart'), t('voice.clearCart2', 'empty cart')],
    action: 'clear_cart'
  },
  
  // Ödeme komutları
  'process payment': {
    description: t('voice.processPayment', 'Process payment'),
    examples: [t('voice.processPayment1', 'process payment'), t('voice.processPayment2', 'pay'), t('voice.processPayment3', 'checkout')],
    action: 'process_payment'
  },
  'cash payment': {
    description: t('voice.cashPayment', 'Process cash payment'),
    examples: [t('voice.cashPayment1', 'cash payment'), t('voice.cashPayment2', 'pay cash')],
    action: 'cash_payment'
  },
  'card payment': {
    description: t('voice.cardPayment', 'Process card payment'),
    examples: [t('voice.cardPayment1', 'card payment'), t('voice.cardPayment2', 'pay card')],
    action: 'card_payment'
  },
  
  // Masa komutları
  'table number': {
    description: t('voice.tableNumber', 'Select table number'),
    examples: [t('voice.table5', 'table 5'), t('voice.tableNumber3', 'table number 3')],
    action: 'select_table'
  },
  'clear table': {
    description: t('voice.clearTable', 'Clear table'),
    examples: [t('voice.clearTable1', 'clear table'), t('voice.clearTable2', 'table clear')],
    action: 'clear_table'
  },
  
  // Genel komutlar
  'help': {
    description: t('voice.help', 'Show available commands'),
    examples: [t('voice.help1', 'help'), t('voice.help2', 'commands'), t('voice.help3', 'what can I say')],
    action: 'show_help'
  },
  'cancel': {
    description: t('voice.cancel', 'Cancel current operation'),
    examples: [t('voice.cancel1', 'cancel'), t('voice.cancel2', 'stop'), t('voice.cancel3', 'abort')],
    action: 'cancel'
  },
  'print receipt': {
    description: t('voice.printReceipt', 'Print receipt'),
    examples: [t('voice.printReceipt1', 'print receipt'), t('voice.printReceipt2', 'print'), t('voice.printReceipt3', 'receipt')],
    action: 'print_receipt'
  }
};

const VoiceCommands: React.FC<VoiceCommandsProps> = ({
  visible,
  onClose,
  onCommand,
}) => {
  const { t } = useTranslation();
  const [isListening, setIsListening] = useState(false);
  const [transcript, setTranscript] = useState('');
  const [recognizedCommand, setRecognizedCommand] = useState<string>('');
  const [showHelp, setShowHelp] = useState(false);
  const [confidence, setConfidence] = useState(0);

  // Mock speech recognition (gerçek uygulamada Web Speech API veya native library kullanılır)
  const mockSpeechRecognition = () => {
    if (!isListening) return;

    const commands = Object.keys(VOICE_COMMANDS);
    const randomCommand = commands[Math.floor(Math.random() * commands.length)];
    const mockTranscript = `"${randomCommand}"`;
    
    setTimeout(() => {
      setTranscript(mockTranscript);
      setRecognizedCommand(randomCommand);
      setConfidence(0.85 + Math.random() * 0.15);
      
      // Komutu işle
      processCommand(randomCommand);
      
      // Dinlemeyi durdur
      setIsListening(false);
    }, 2000);
  };

  useEffect(() => {
    if (isListening) {
      mockSpeechRecognition();
    }
  }, [isListening]);

  const startListening = () => {
    setIsListening(true);
    setTranscript('');
    setRecognizedCommand('');
    setConfidence(0);
    Vibration.vibrate(100);
  };

  const stopListening = () => {
    setIsListening(false);
    Vibration.vibrate(50);
  };

  const processCommand = (command: string) => {
    const commandConfig = VOICE_COMMANDS[command as keyof typeof VOICE_COMMANDS];
    
    if (commandConfig) {
      onCommand(commandConfig.action, { command, confidence });
      
      // Başarılı komut geri bildirimi
      Vibration.vibrate([100, 50, 100]);
      
      setTimeout(() => {
        setTranscript('');
        setRecognizedCommand('');
        setConfidence(0);
      }, 2000);
    }
  };

  const handleManualCommand = (command: string) => {
    processCommand(command);
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
          <Text style={styles.headerTitle}>{t('voice.header', 'Voice Commands')}</Text>
          <TouchableOpacity 
            style={styles.helpButton} 
            onPress={() => setShowHelp(!showHelp)}
          >
            <Ionicons name="help-circle" size={24} color="white" />
          </TouchableOpacity>
        </View>

        {/* Main Content */}
        <View style={styles.content}>
          {/* Microphone Button */}
          <TouchableOpacity
            style={[
              styles.microphoneButton,
              isListening && styles.microphoneButtonListening
            ]}
            onPress={isListening ? stopListening : startListening}
          >
            <Ionicons 
              name={isListening ? "mic" : "mic-outline"} 
              size={48} 
              color="white" 
            />
            <Text style={styles.microphoneText}>
              {isListening ? t('voice.listening', 'Listening...') : t('voice.tapToSpeak', 'Tap to Speak')}
            </Text>
          </TouchableOpacity>

          {/* Transcript Display */}
          {transcript && (
            <View style={styles.transcriptContainer}>
              <Text style={styles.transcriptLabel}>{t('voice.youSaid', 'You said:')}</Text>
              <Text style={styles.transcriptText}>{transcript}</Text>
              {confidence > 0 && (
                <Text style={styles.confidenceText}>
                  {t('voice.confidence', 'Confidence')}: {Math.round(confidence * 100)}%
                </Text>
              )}
            </View>
          )}

          {/* Recognized Command */}
          {recognizedCommand && (
            <View style={styles.commandContainer}>
              <Text style={styles.commandLabel}>{t('voice.command', 'Command:')}</Text>
              <Text style={styles.commandText}>
                {VOICE_COMMANDS[recognizedCommand as keyof typeof VOICE_COMMANDS]?.description}
              </Text>
            </View>
          )}

          {/* Quick Commands */}
          <View style={styles.quickCommands}>
            <Text style={styles.quickCommandsTitle}>{t('voice.quickCommands', 'Quick Commands')}</Text>
            <View style={styles.quickCommandsGrid}>
              {Object.entries(VOICE_COMMANDS).slice(0, 6).map(([command, config]) => (
                <TouchableOpacity
                  key={command}
                  style={styles.quickCommandButton}
                  onPress={() => handleManualCommand(command)}
                >
                  <Text style={styles.quickCommandText}>{config.description}</Text>
                </TouchableOpacity>
              ))}
            </View>
          </View>
        </View>

        {/* Help Modal */}
        {showHelp && (
          <View style={styles.helpOverlay}>
            <View style={styles.helpModal}>
              <View style={styles.helpHeader}>
                <Text style={styles.helpTitle}>{t('voice.availableCommands', 'Available Commands')}</Text>
                <TouchableOpacity onPress={() => setShowHelp(false)}>
                  <Ionicons name="close" size={24} color={Colors.light.text} />
                </TouchableOpacity>
              </View>
              
              <View style={styles.helpContent}>
                {Object.entries(VOICE_COMMANDS).map(([command, config]) => (
                  <View key={command} style={styles.helpItem}>
                    <Text style={styles.helpCommand}>{command}</Text>
                    <Text style={styles.helpDescription}>{config.description}</Text>
                    <Text style={styles.helpExamples}>
                      {t('voice.examples', 'Examples')}: {config.examples.join(', ')}
                    </Text>
                  </View>
                ))}
              </View>
            </View>
          </View>
        )}
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
  helpButton: {
    padding: Spacing.sm,
  },
  content: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: Spacing.xl,
  },
  microphoneButton: {
    width: 120,
    height: 120,
    borderRadius: 60,
    backgroundColor: Colors.light.primary,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: Spacing.xl,
  },
  microphoneButtonListening: {
    backgroundColor: Colors.light.error,
    transform: [{ scale: 1.1 }],
  },
  microphoneText: {
    color: 'white',
    fontSize: 14,
    marginTop: Spacing.sm,
    textAlign: 'center',
  },
  transcriptContainer: {
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.md,
    width: '100%',
  },
  transcriptLabel: {
    color: 'white',
    fontSize: 14,
    marginBottom: Spacing.xs,
  },
  transcriptText: {
    color: 'white',
    fontSize: 18,
    fontWeight: '600',
  },
  confidenceText: {
    color: Colors.light.textSecondary,
    fontSize: 12,
    marginTop: Spacing.xs,
  },
  commandContainer: {
    backgroundColor: 'rgba(0, 255, 0, 0.2)',
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.lg,
    width: '100%',
  },
  commandLabel: {
    color: 'white',
    fontSize: 14,
    marginBottom: Spacing.xs,
  },
  commandText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
  quickCommands: {
    width: '100%',
  },
  quickCommandsTitle: {
    color: 'white',
    fontSize: 18,
    fontWeight: '600',
    marginBottom: Spacing.md,
    textAlign: 'center',
  },
  quickCommandsGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'center',
    gap: Spacing.sm,
  },
  quickCommandButton: {
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    minWidth: 120,
    alignItems: 'center',
  },
  quickCommandText: {
    color: 'white',
    fontSize: 12,
    textAlign: 'center',
  },
  helpOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
    justifyContent: 'center',
    alignItems: 'center',
    zIndex: 1000,
  },
  helpModal: {
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.lg,
    padding: Spacing.lg,
    width: '90%',
    maxHeight: '80%',
  },
  helpHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.lg,
  },
  helpTitle: {
    ...Typography.h4,
    color: Colors.light.text,
  },
  helpContent: {
    maxHeight: 400,
  },
  helpItem: {
    marginBottom: Spacing.md,
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
  },
  helpCommand: {
    ...Typography.body,
    color: Colors.light.primary,
    fontWeight: '600',
    marginBottom: Spacing.xs,
  },
  helpDescription: {
    ...Typography.body,
    color: Colors.light.text,
    marginBottom: Spacing.xs,
  },
  helpExamples: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    fontStyle: 'italic',
  },
});

export default VoiceCommands; 