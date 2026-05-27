import React from 'react';
import { Modal, StyleSheet, Text, TouchableOpacity, View } from 'react-native';

import { CountdownTimer } from './CountdownTimer';

type Props = {
    visible: boolean;
    warningSeconds: number;
    onContinueSession: () => void;
    onCountdownComplete: () => void;
};

/**
 * RKSV POS: Warnung vor automatischer Abmeldung wegen Inaktivität (UI Deutsch).
 */
export function SessionTimeoutWarning({
    visible,
    warningSeconds,
    onContinueSession,
    onCountdownComplete,
}: Props) {
    return (
        <Modal visible={visible} transparent animationType="fade">
            <View style={styles.backdrop}>
                <View style={styles.card}>
                    <Text style={styles.title}>Sitzung läuft ab</Text>
                    <Text style={styles.body}>
                        Sie werden wegen Inaktivität automatisch abgemeldet. Drücken Sie „Sitzung fortsetzen“, um
                        angemeldet zu bleiben.
                    </Text>
                    <CountdownTimer
                        active={visible}
                        seconds={warningSeconds}
                        onComplete={onCountdownComplete}
                    />
                    <TouchableOpacity style={styles.button} onPress={onContinueSession}>
                        <Text style={styles.buttonText}>Sitzung fortsetzen</Text>
                    </TouchableOpacity>
                </View>
            </View>
        </Modal>
    );
}

const styles = StyleSheet.create({
    backdrop: {
        flex: 1,
        backgroundColor: 'rgba(0,0,0,0.45)',
        justifyContent: 'center',
        alignItems: 'center',
        padding: 24,
    },
    card: {
        backgroundColor: '#fff',
        borderRadius: 12,
        padding: 24,
        maxWidth: 400,
        width: '100%',
    },
    title: {
        fontSize: 18,
        fontWeight: '700',
        marginBottom: 12,
        color: '#1a1a1a',
    },
    body: {
        fontSize: 15,
        color: '#444',
        marginBottom: 8,
    },
    button: {
        backgroundColor: '#1677ff',
        paddingVertical: 12,
        borderRadius: 8,
        alignItems: 'center',
        marginTop: 8,
    },
    buttonText: {
        color: '#fff',
        fontWeight: '600',
        fontSize: 16,
    },
});
