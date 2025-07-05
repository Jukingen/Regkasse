import { useEffect, useState } from 'react';
import { Typography, CircularProgress, Alert, Box, Card, CardContent, Grid, Chip } from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

export default function AuditLogs() {
  const { t } = useTranslation();
  const [auditLogs, setAuditLogs] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchAuditLogs = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - audit logs endpoint'inden veri al
        const response = await api.get('/api/audit-logs');
        
        if (response.data && response.data.length > 0) {
          setAuditLogs(response.data);
        } else {
          setAuditLogs([]);
        }
      } catch (err) {
        console.error('Audit Logs API error:', err);
        setAuditLogs([]);
        setError('API bağlantı hatası - Veriler yüklenemedi');
      } finally {
        setLoading(false);
      }
    };

    fetchAuditLogs();
  }, []);

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'success': return 'success';
      case 'error': return 'error';
      case 'warning': return 'warning';
      default: return 'default';
    }
  };

  const getActionColor = (action: string) => {
    switch (action.toLowerCase()) {
      case 'create': return 'success';
      case 'update': return 'info';
      case 'delete': return 'error';
      case 'login': return 'primary';
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
          {t('navigation.auditLogs')}
        </Typography>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      </Box>
    );
  }

  if (auditLogs.length === 0) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.auditLogs')}
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Henüz audit log verisi bulunmuyor.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.auditLogs')}
      </Typography>

      <Grid container spacing={3}>
        {auditLogs.map((log) => (
          <Grid item xs={12} md={6} lg={4} key={log.id}>
            <Card>
              <CardContent>
                <Box display="flex" justifyContent="space-between" alignItems="flex-start" mb={2}>
                  <Typography variant="h6" gutterBottom>
                    {log.action} - {log.entityType}
                  </Typography>
                  <Box>
                    <Chip 
                      label={log.action} 
                      color={getActionColor(log.action) as any}
                      size="small"
                      sx={{ mr: 1 }}
                    />
                    <Chip 
                      label={log.status} 
                      color={getStatusColor(log.status) as any}
                      size="small"
                    />
                  </Box>
                </Box>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  User: {log.userName} ({log.userId})
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Entity ID: {log.entityId}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  IP Address: {log.ipAddress}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  User Agent: {log.userAgent?.substring(0, 50)}...
                </Typography>
                {log.oldValues && (
                  <Typography variant="body2" color="text.secondary" gutterBottom>
                    Old Values: {log.oldValues.substring(0, 100)}...
                  </Typography>
                )}
                {log.newValues && (
                  <Typography variant="body2" color="text.secondary" gutterBottom>
                    New Values: {log.newValues.substring(0, 100)}...
                  </Typography>
                )}
                <Typography variant="caption" color="text.secondary">
                  {new Date(log.createdAt).toLocaleString()}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
} 