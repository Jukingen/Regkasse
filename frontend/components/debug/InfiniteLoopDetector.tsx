import React, { useRef, useEffect, useState } from 'react';
import { View, Text, Alert, StyleSheet, TouchableOpacity } from 'react-native';

// Türkçe Açıklama: Infinite loop tespiti için debug component'i
// RKSV uyumlu güvenlik kontrolü ile birlikte render count tracking

interface InfiniteLoopDetectorProps {
  threshold?: number; // Kaç render'dan sonra loop olarak kabul edilsin (varsayılan: 100)
  showInProduction?: boolean; // Production'da gösterilsin mi? (varsayılan: false)
}

export const InfiniteLoopDetector: React.FC<InfiniteLoopDetectorProps> = ({
  threshold = 100,
  showInProduction = false
}) => {
  const renderCountRef = useRef(0);
  const lastCheckTimeRef = useRef(Date.now());
  const [renderCount, setRenderCount] = useState(0);
  const [isLoopDetected, setIsLoopDetected] = useState(false);
  const [status, setStatus] = useState<'normal' | 'warning' | 'critical'>('normal');

  // Production mode kontrolü
  const isDevelopment = __DEV__ || process.env.NODE_ENV === 'development';

  if (!isDevelopment && !showInProduction) {
    return null; // Production'da gösterme (showInProduction false ise)
  }

  // Her render'da count'u artır
  renderCountRef.current += 1;
  const currentCount = renderCountRef.current;

  useEffect(() => {
    const now = Date.now();
    const timeSinceLastCheck = now - lastCheckTimeRef.current;

    // CRITICAL FIX: Throttle state updates to avoid self-induced infinite loops
    if (currentCount % 50 === 0 || currentCount > threshold) {
      setRenderCount(currentCount);
    }

    // Status belirleme
    if (currentCount > threshold) {
      setStatus('critical');
      if (!isLoopDetected) {
        setIsLoopDetected(true);
        console.warn('🚨 INFINITE LOOP DETECTED!', {
          renderCount: currentCount,
          threshold,
          timeElapsed: timeSinceLastCheck,
          component: 'InfiniteLoopDetector'
        });

        // Alert göster (sadece bir kez)
        Alert.alert(
          '🚨 Infinite Loop Detected',
          `Component rendered ${currentCount} times!\n\nThis indicates a potential infinite loop.\n\nCheck your useEffect dependencies and state updates.`,
          [
            { text: 'OK', style: 'default' },
            {
              text: 'Reset Counter',
              onPress: () => {
                renderCountRef.current = 0;
                setRenderCount(0);
                setIsLoopDetected(false);
                setStatus('normal');
                lastCheckTimeRef.current = Date.now();
                console.log('🔄 Loop detector reset');
              }
            }
          ]
        );
      }
    } else if (currentCount > threshold * 0.7) {
      setStatus('warning');
    } else {
      setStatus('normal');
    }

    // Her 10 saniyede bir log yaz (debug için)
    if (timeSinceLastCheck > 10000) {
      console.log('🔍 Loop Detector Stats:', {
        renderCount: currentCount,
        renderRate: currentCount / (timeSinceLastCheck / 1000),
        status,
        isLoopDetected
      });
      lastCheckTimeRef.current = now;
    }
  }, [currentCount, threshold, isLoopDetected, status]);

  // Reset fonksiyonu
  const resetCounter = () => {
    renderCountRef.current = 0;
    setRenderCount(0);
    setIsLoopDetected(false);
    setStatus('normal');
    lastCheckTimeRef.current = Date.now();
    console.log('🔄 Loop detector manually reset');
  };

  // Style belirleme
  const getStatusColor = () => {
    switch (status) {
      case 'critical': return '#FF4444';
      case 'warning': return '#FFA500';
      default: return '#4CAF50';
    }
  };

  const getStatusText = () => {
    switch (status) {
      case 'critical': return 'LOOP DETECTED!';
      case 'warning': return 'High Render Count';
      default: return 'Normal';
    }
  };

  return (
    <View style={[styles.container, { borderColor: getStatusColor() }]}>
      <View style={styles.header}>
        <Text style={styles.title}>🔍 Loop Detector</Text>
        <TouchableOpacity onPress={resetCounter} style={styles.resetButton}>
          <Text style={styles.resetText}>Reset</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.content}>
        <Text style={[styles.count, { color: getStatusColor() }]}>
          {renderCount} renders
        </Text>
        <Text style={[styles.status, { color: getStatusColor() }]}>
          {getStatusText()}
        </Text>
        <Text style={styles.threshold}>
          Threshold: {threshold}
        </Text>
      </View>

      {isLoopDetected && (
        <View style={styles.warning}>
          <Text style={styles.warningText}>
            ⚠️ Potential infinite loop detected!
          </Text>
          <Text style={styles.helpText}>
            Check useEffect dependencies and state updates
          </Text>
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    top: 50,
    right: 10,
    backgroundColor: 'rgba(0,0,0,0.8)',
    borderRadius: 8,
    borderWidth: 2,
    padding: 10,
    zIndex: 9999,
    minWidth: 150,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  title: {
    color: 'white',
    fontSize: 12,
    fontWeight: 'bold',
  },
  resetButton: {
    backgroundColor: '#555',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  resetText: {
    color: 'white',
    fontSize: 10,
  },
  content: {
    alignItems: 'center',
  },
  count: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  status: {
    fontSize: 12,
    fontWeight: '600',
    marginBottom: 4,
  },
  threshold: {
    color: '#ccc',
    fontSize: 10,
  },
  warning: {
    marginTop: 8,
    padding: 6,
    backgroundColor: 'rgba(255,68,68,0.2)',
    borderRadius: 4,
  },
  warningText: {
    color: '#FF4444',
    fontSize: 10,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: 2,
  },
  helpText: {
    color: '#FFB6B6',
    fontSize: 9,
    textAlign: 'center',
  },
});

export default InfiniteLoopDetector;
