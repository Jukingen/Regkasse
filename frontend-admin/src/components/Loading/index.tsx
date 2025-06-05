import { Box, CircularProgress, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';

interface LoadingProps {
  message?: string;
}

export default function Loading({ message }: LoadingProps) {
  const { t } = useTranslation();

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '200px',
      }}
    >
      <CircularProgress size={40} />
      <Typography variant="body1" color="text.secondary" sx={{ mt: 2 }}>
        {message || t('common.loading')}
      </Typography>
    </Box>
  );
} 