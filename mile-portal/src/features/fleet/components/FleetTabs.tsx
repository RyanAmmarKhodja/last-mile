import { Tabs } from '@/shared/components';

export function FleetTabs() {
  return (
    <Tabs
      items={[
        { to: '/fleet/drivers', label: 'Drivers' },
        { to: '/fleet/vehicles', label: 'Vehicles' },
      ]}
    />
  );
}
