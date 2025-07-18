import React from 'react';
import { View } from 'react-native';

import { NotificationToast } from './NotificationToast';
import { useAppState } from '../../contexts/AppStateContext';

export const NotificationManager: React.FC = () => {
  const { notifications, removeNotification } = useAppState();

  return (
    <View style={{ position: 'absolute', top: 0, left: 0, right: 0, zIndex: 1000 }}>
      {notifications.map((notification, index) => (
        <NotificationToast
          key={notification.id}
          visible
          type={notification.type}
          title={notification.title}
          message={notification.message}
          duration={notification.duration}
          onClose={() => removeNotification(notification.id)}
        />
      ))}
    </View>
  );
}; 