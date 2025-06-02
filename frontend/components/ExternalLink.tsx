import { Link } from 'expo-router';
import { type ComponentProps } from 'react';
import { Platform, Linking } from 'react-native';

type Props = Omit<ComponentProps<typeof Link>, 'href'> & { href: string };

export function ExternalLink(props: Props) {
  const { href, ...rest } = props;

  const handlePress = () => {
    if (Platform.OS === 'web') {
      window.open(href, '_blank');
    } else {
      Linking.openURL(href);
    }
  };

  return <Link {...rest} href={href} onPress={handlePress} />;
}
