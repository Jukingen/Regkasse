import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Redirect } from 'expo-router';

export default function Index() {
    // Ana sayfayı kasa sayfasına yönlendir
    return <Redirect href="/(tabs)/cash-register" />;
} 