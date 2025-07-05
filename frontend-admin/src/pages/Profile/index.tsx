import { useEffect, useState } from 'react';
import { Typography, CircularProgress, Alert, Box, Card, CardContent, Grid, Chip, Avatar } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import api from '../../services/api';

export default function Profile() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [profile, setProfile] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchProfile = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - user profile endpoint'inden veri al
        const response = await api.get('/api/users/profile');
        
        if (response.data) {
          setProfile(response.data);
        } else {
          setProfile(null);
        }
      } catch (err) {
        console.error('Profile API error:', err);
        setProfile(null);
        setError('API bağlantı hatası - Veriler yüklenemedi');
      } finally {
        setLoading(false);
      }
    };

    fetchProfile();
  }, []);

  const getRoleColor = (role: string) => {
    switch (role?.toLowerCase()) {
      case 'admin': return 'error';
      case 'manager': return 'warning';
      case 'cashier': return 'info';
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
          {t('navigation.profile')}
        </Typography>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      </Box>
    );
  }

  if (!profile) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.profile')}
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Henüz profil verisi bulunmuyor.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.profile')}
      </Typography>

      <Grid container spacing={3}>
        {/* Profile Info */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Box display="flex" alignItems="center" mb={3}>
                <Avatar 
                  sx={{ width: 80, height: 80, mr: 2 }}
                  alt={`${profile.firstName} ${profile.lastName}`}
                >
                  {profile.firstName?.[0]}{profile.lastName?.[0]}
                </Avatar>
                <Box>
                  <Typography variant="h6">
                    {profile.firstName} {profile.lastName}
                  </Typography>
                  <Chip 
                    label={profile.role || 'User'} 
                    color={getRoleColor(profile.role) as any}
                    size="small"
                  />
                </Box>
              </Box>
              
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Email: {profile.email}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Employee Number: {profile.employeeNumber}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Username: {profile.userName}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Email Confirmed: {profile.emailConfirmed ? 'Yes' : 'No'}
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        {/* Account Details */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Account Details
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                User ID: {profile.id}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Created: {new Date(profile.createdAt || Date.now()).toLocaleDateString()}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Last Login: {new Date(profile.lastLoginAt || Date.now()).toLocaleString()}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Account Status: {profile.isActive ? 'Active' : 'Inactive'}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Two Factor Enabled: {profile.twoFactorEnabled ? 'Yes' : 'No'}
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        {/* Permissions */}
        <Grid item xs={12}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Permissions & Roles
              </Typography>
              <Box display="flex" gap={1} flexWrap="wrap">
                {profile.roles?.map((role: string) => (
                  <Chip 
                    key={role} 
                    label={role} 
                    color="primary" 
                    variant="outlined"
                    size="small"
                  />
                )) || (
                  <Typography variant="body2" color="text.secondary">
                    No specific roles assigned
                  </Typography>
                )}
              </Box>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
} 