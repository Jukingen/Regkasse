import { useEffect, useState } from 'react';
import { Typography, CircularProgress, Alert, Box, Card, CardContent, Grid, Chip } from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

export default function TSE() {
  const { t } = useTranslation();
  const [tseData, setTseData] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchTseData = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - TSE status endpoint'inden veri al
        const response = await api.get('/api/tse/status');
        
        if (response.data) {
          setTseData([response.data]);
        } else {
          setTseData([]);
        }
      } catch (err) {
        console.error('TSE API error:', err);
        setTseData([]);
        setError('API bağlantı hatası - Veriler yüklenemedi');
      } finally {
        setLoading(false);
      }
    };

    fetchTseData();
  }, []);

  const getConnectionColor = (isConnected: boolean) => {
    return isConnected ? 'success' : 'error';
  };

  const getMemoryStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'normal': return 'success';
      case 'warning': return 'warning';
      case 'critical': return 'error';
      default: return 'default';
    }
  };

  const getCertificateStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'valid': return 'success';
      case 'expiring soon': return 'warning';
      case 'expired': return 'error';
      default: return 'default';
    }
  };

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="400px">
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.tse')}
        </Typography>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      </Box>
    );
  }

  if (tseData.length === 0) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.tse')}
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Henüz TSE verisi bulunmuyor.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.tse')}
      </Typography>

      <Grid container spacing={3}>
        {tseData.map((device) => (
          <Grid item xs={12} md={6} key={device.id}>
            <Card>
              <CardContent>
                <Box display="flex" justifyContent="space-between" alignItems="flex-start" mb={2}>
                  <Typography variant="h6" gutterBottom>
                    {device.deviceName || 'TSE Device'}
                  </Typography>
                  <Chip 
                    label={device.isConnected ? 'Connected' : 'Disconnected'} 
                    color={getConnectionColor(device.isConnected) as any}
                    size="small"
                  />
                </Box>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Serial Number: {device.serialNumber}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Firmware Version: {device.firmwareVersion}
                </Typography>
                <Typography variant="body1" fontWeight="bold">
                  Last Signature Counter: {device.lastSignatureCounter}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Last Signature: {new Date(device.lastSignatureTime).toLocaleString()}
                </Typography>
                <Box display="flex" gap={1} mb={1}>
                  <Chip 
                    label={`Memory: ${device.memoryStatus}`} 
                    color={getMemoryStatusColor(device.memoryStatus) as any}
                    size="small"
                  />
                  <Chip 
                    label={`Certificate: ${device.certificateStatus}`} 
                    color={getCertificateStatusColor(device.certificateStatus) as any}
                    size="small"
                  />
                </Box>
                <Typography variant="body2" color="text.secondary">
                  Certificate Expiry: {new Date(device.certificateExpiry).toLocaleDateString()}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Daily Report Status: {device.dailyReportStatus}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Last Daily Report: {new Date(device.lastDailyReport).toLocaleString()}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
} 