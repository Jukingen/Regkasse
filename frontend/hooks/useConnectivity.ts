import { useEffect, useRef, useState } from 'react';
import NetInfo from '@react-native-community/netinfo';
import { Alert } from 'react-native';

import { isDevSimulatePosNetworkOffline } from '../constants/devSimulatePosOffline';

/**
 * Subscribes to connectivity changes and surfaces German operator alerts for offline/online transitions.
 */
export const useConnectivity = () => {
  const [isConnected, setIsConnected] = useState(true);
  const [wasOffline, setWasOffline] = useState(false);
  const wasOfflineRef = useRef(false);

  useEffect(() => {
    const unsubscribe = NetInfo.addEventListener((state) => {
      const connectedRaw = state.isConnected ?? false;
      const connected = connectedRaw && !isDevSimulatePosNetworkOffline();

      if (!connected && !wasOfflineRef.current) {
        Alert.alert(
          'Offline-Modus',
          'Keine Verbindung zum Server. Gutscheine können nicht eingelöst werden. Zahlungen werden in Warteschlange gestellt.',
          [{ text: 'Verstanden' }]
        );
        wasOfflineRef.current = true;
        setWasOffline(true);
      } else if (connected && wasOfflineRef.current) {
        Alert.alert('Online-Modus wiederhergestellt', 'Ausstehende Zahlungen werden jetzt verarbeitet.', [{ text: 'OK' }]);
        wasOfflineRef.current = false;
        setWasOffline(false);
      }

      setIsConnected(connected);
    });

    return () => unsubscribe();
  }, []);

  return { isConnected, wasOffline };
};
