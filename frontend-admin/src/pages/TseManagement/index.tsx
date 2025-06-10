import React, { useEffect, useState } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Button,
  Grid,
  Alert,
  CircularProgress,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

interface TseStatus {
  isConnected: boolean;
  serialNumber: string;
  lastSignatureCounter: number;
  lastSignatureTime: string;
  memoryStatus: string;
  certificateStatus: string;
}

interface TseSignatureResult {
  signature: string;
  signatureCounter: number;
  time: string;
  processType: string;
  serialNumber: string;
}

const TseManagement: React.FC = () => {
  const { t } = useTranslation();
  const [status, setStatus] = useState<TseStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dailyReport, setDailyReport] = useState<TseSignatureResult | null>(null);
  const [generatingReport, setGeneratingReport] = useState(false);

  const fetchStatus = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await api.get('/api/tse/status');
      setStatus(response.data);
    } catch (err) {
      setError(t('errors.tse_status_failed'));
      console.error('TSE durum bilgisi alınamadı:', err);
    } finally {
      setLoading(false);
    }
  };

  const generateDailyReport = async () => {
    try {
      setGeneratingReport(true);
      setError(null);
      const response = await api.post('/api/tse/daily-report');
      setDailyReport(response.data);
    } catch (err) {
      setError(t('errors.daily_report_failed'));
      console.error('Günlük rapor oluşturulamadı:', err);
    } finally {
      setGeneratingReport(false);
    }
  };

  useEffect(() => {
    fetchStatus();
    const interval = setInterval(fetchStatus, 30000); // Her 30 saniyede bir güncelle
    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="400px">
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box p={3}>
      <Typography variant="h4" gutterBottom>
        {t('tse.management_title')}
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Box sx={{ display: 'flex', flexDirection: { xs: 'column', md: 'row' }, gap: 3 }}>
        <Box sx={{ flex: 1 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('tse.status')}
              </Typography>
              {status && (
                <TableContainer component={Paper}>
                  <Table>
                    <TableBody>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.connection_status')}
                        </TableCell>
                        <TableCell>
                          <Alert severity={status.isConnected ? 'success' : 'error'}>
                            {status.isConnected ? t('tse.connected') : t('tse.disconnected')}
                          </Alert>
                        </TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.serial_number')}
                        </TableCell>
                        <TableCell>{status.serialNumber}</TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.last_signature_counter')}
                        </TableCell>
                        <TableCell>{status.lastSignatureCounter}</TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.last_signature_time')}
                        </TableCell>
                        <TableCell>
                          {new Date(status.lastSignatureTime).toLocaleString()}
                        </TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.memory_status')}
                        </TableCell>
                        <TableCell>
                          <Alert severity={status.memoryStatus === 'OK' ? 'success' : 'warning'}>
                            {status.memoryStatus}
                          </Alert>
                        </TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.certificate_status')}
                        </TableCell>
                        <TableCell>
                          <Alert severity={status.certificateStatus === 'VALID' ? 'success' : 'error'}>
                            {status.certificateStatus}
                          </Alert>
                        </TableCell>
                      </TableRow>
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </CardContent>
          </Card>
        </Box>

        <Box sx={{ flex: 1 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('tse.daily_report')}
              </Typography>
              <Button
                variant="contained"
                color="primary"
                onClick={generateDailyReport}
                disabled={generatingReport || !status?.isConnected}
                sx={{ mb: 2 }}
              >
                {generatingReport ? (
                  <>
                    <CircularProgress size={24} sx={{ mr: 1 }} />
                    {t('tse.generating_report')}
                  </>
                ) : (
                  t('tse.generate_daily_report')
                )}
              </Button>

              {dailyReport && (
                <TableContainer component={Paper}>
                  <Table>
                    <TableBody>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.signature')}
                        </TableCell>
                        <TableCell>{dailyReport.signature}</TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.signature_counter')}
                        </TableCell>
                        <TableCell>{dailyReport.signatureCounter}</TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.time')}
                        </TableCell>
                        <TableCell>
                          {new Date(dailyReport.time).toLocaleString()}
                        </TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell component="th" scope="row">
                          {t('tse.process_type')}
                        </TableCell>
                        <TableCell>{dailyReport.processType}</TableCell>
                      </TableRow>
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </CardContent>
          </Card>
        </Box>
      </Box>
    </Box>
  );
};

export default TseManagement; 