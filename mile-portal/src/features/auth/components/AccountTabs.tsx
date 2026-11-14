import { Tabs } from '@/shared/components';

export function AccountTabs() {
  return (
    <Tabs
      items={[
        { to: '/account', label: 'Profile', end: true },
        { to: '/account/security', label: 'Email & password' },
        { to: '/account/session', label: 'Session' },
      ]}
    />
  );
}
