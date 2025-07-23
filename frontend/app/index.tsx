import { Redirect } from 'expo-router';

export default function Index() {
  // Varsayılan olarak login sayfasına yönlendir
  return <Redirect href="/(auth)/login" />;
} 