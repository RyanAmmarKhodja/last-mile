import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { fleetApi, getErrorMessage, shipmentsApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { qk } from '@/shared/query/queryClient';
import { Alert, Button, Dialog, SelectField } from '@/shared/components';
import { formatWeight, fullName } from '@/shared/utils/format';
import { FINAL_STATUSES, type Shipment } from '@/shared/types/shipments';
import { useShipmentMutation } from '../hooks/useShipmentMutation';

type Action = 'dispatch' | 'pickup' | 'deliver' | 'fail' | 'cancel';

/** §6: shows only the transitions the signed-in role may make; the backend still decides (403 / 409 shown). */
export function availableActions(s: Shipment, role: string | undefined, userId: string | undefined, isAssignedDriver: boolean): Action[] {
  const admin = role === 'ADMIN';
  const driver = role === 'DRIVER' && isAssignedDriver;
  const actions: Action[] = [];
  if (admin && s.status === 'CREATED') actions.push('dispatch');
  if ((admin || driver) && s.status === 'DISPATCHED') actions.push('pickup');
  if ((admin || driver) && s.status === 'IN_TRANSIT') actions.push('deliver', 'fail');
  if (!FINAL_STATUSES.includes(s.status) && (admin || (s.status === 'CREATED' && s.senderUserId === userId))) actions.push('cancel');
  return actions;
}

const LABEL: Record<Action, string> = {
  dispatch: 'Dispatch',
  pickup: 'Confirm pick-up',
  deliver: 'Mark delivered',
  fail: 'Report failed delivery',
  cancel: 'Cancel shipment',
};

export function ShipmentActions({ shipment, isAssignedDriver = false }: { shipment: Shipment; isAssignedDriver?: boolean }) {
  const { user } = useAuth();
  const [open, setOpen] = useState<Action | null>(null);
  const [text, setText] = useState('');
  const [driverId, setDriverId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const actions = availableActions(shipment, user?.role, user?.id, isAssignedDriver);

  const mutation = useShipmentMutation(async (action: Action) => {
    switch (action) {
      case 'dispatch':
        return shipmentsApi.dispatch(shipment.id, driverId || null);
      case 'pickup':
        return shipmentsApi.pickup(shipment.id);
      case 'deliver':
        return shipmentsApi.deliver(shipment.id, text.trim());
      case 'fail':
        return shipmentsApi.fail(shipment.id, text.trim());
      case 'cancel':
        return shipmentsApi.cancel(shipment.id, text.trim());
    }
  });

  const availableDrivers = useQuery({
    queryKey: qk.drivers({ available: true, pageSize: 100 }),
    queryFn: () => fleetApi.listDrivers({ available: true, pageSize: 100 }),
    enabled: open === 'dispatch',
  });

  if (actions.length === 0) return null;

  function start(action: Action) {
    setError(null);
    setText('');
    setDriverId('');
    if (action === 'pickup') return void run(action);
    setOpen(action);
  }

  function run(action: Action) {
    mutation.mutate(action, {
      onSuccess: () => setOpen(null),
      onError: (err) => setError(getErrorMessage(err)),
    });
  }

  function submit(e: FormEvent) {
    e.preventDefault();
    if (open) run(open);
  }

  const fits = (availableDrivers.data?.items ?? []).filter((d) => d.vehicle && d.vehicle.capacityKg >= shipment.weightKg);

  return (
    <>
      <div className="row">
        {actions.map((a) => (
          <Button
            key={a}
            size="sm"
            variant={a === 'cancel' || a === 'fail' ? 'ghost' : 'primary'}
            loading={mutation.isPending && mutation.variables === a}
            disabled={mutation.isPending}
            onClick={() => start(a)}
          >
            {LABEL[a]}
          </Button>
        ))}
      </div>
      {error && !open && (
        <Alert variant="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Dialog open={open !== null} title={open ? LABEL[open] : ''} onClose={() => setOpen(null)}>
        <form className="form" onSubmit={submit}>
          {open === 'dispatch' && (
            <>
              <p className="muted">
                {shipment.trackingNumber} weighs {formatWeight(shipment.weightKg)}. Leave the driver on automatic to let ShipmentService
                pick the available driver with the smallest vehicle that fits.
              </p>
              <SelectField
                label="Driver"
                value={driverId}
                onChange={(e) => setDriverId(e.target.value)}
                options={[
                  { value: '', label: availableDrivers.isLoading ? 'Loading drivers…' : 'Automatic (smallest vehicle that fits)' },
                  ...fits.map((d) => ({
                    value: d.id,
                    label: `${fullName(d)} · ${d.vehicle!.licensePlate} (${formatWeight(d.vehicle!.capacityKg)})`,
                  })),
                ]}
                hint={
                  availableDrivers.data
                    ? `${fits.length} available driver${fits.length === 1 ? '' : 's'} with enough capacity.`
                    : availableDrivers.error
                      ? getErrorMessage(availableDrivers.error)
                      : undefined
                }
              />
            </>
          )}
          {(open === 'deliver' || open === 'fail' || open === 'cancel') && (
            <div className="field">
              <label htmlFor="action-text">{open === 'fail' ? 'Reason (required)' : open === 'deliver' ? 'Proof-of-delivery note (optional)' : 'Reason (optional)'}</label>
              <textarea
                id="action-text"
                maxLength={1000}
                required={open === 'fail'}
                value={text}
                onChange={(e) => setText(e.target.value)}
                placeholder={open === 'fail' ? 'Recipient absent' : open === 'deliver' ? 'Left with the concierge' : ''}
              />
            </div>
          )}
          {open === 'cancel' && <p className="muted">Cancelling is final. A cancelled shipment cannot be reopened.</p>}
          {error && <Alert variant="error">{error}</Alert>}
          <div className="dialog-actions">
            <Button type="button" variant="ghost" onClick={() => setOpen(null)}>
              Back
            </Button>
            <Button type="submit" variant={open === 'cancel' || open === 'fail' ? 'danger' : 'primary'} loading={mutation.isPending}>
              {open ? LABEL[open] : ''}
            </Button>
          </div>
        </form>
      </Dialog>
    </>
  );
}
