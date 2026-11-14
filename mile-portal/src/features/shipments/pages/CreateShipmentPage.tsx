import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { getErrorMessage, shipmentsApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { qk } from '@/shared/query/queryClient';
import { Alert, Button, Card, TextField } from '@/shared/components';
import type { ClientProfile } from '@/shared/types/profiles';
import type { CreateShipmentRequest } from '@/shared/types/shipments';
import { ClientPicker } from '../components/ClientPicker';
import { useMyClientProfile } from '../hooks/useMyClientProfile';
import '../styles/shipments.css';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** FR-4 / S1: CLIENT sends as themselves to a recipient email; ADMIN picks sender and recipient. */
export function CreateShipmentPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'ADMIN';
  const navigate = useNavigate();
  const client = useQueryClient();
  const me = useMyClientProfile();

  const [sender, setSender] = useState<ClientProfile | null>(null);
  const [recipient, setRecipient] = useState<ClientProfile | null>(null);
  const [recipientEmail, setRecipientEmail] = useState('');
  const [weight, setWeight] = useState('');
  const [origin, setOrigin] = useState('');
  const [destination, setDestination] = useState('');
  const [notes, setNotes] = useState('');
  const [formError, setFormError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: (body: CreateShipmentRequest) => shipmentsApi.create(body),
    onSuccess: (s) => {
      client.setQueryData(qk.shipment(s.id), s);
      void client.invalidateQueries({ queryKey: qk.shipments });
      navigate(`/shipments/${s.id}`, { state: { created: true } });
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    const weightKg = Number(weight.replace(',', '.'));
    if (!Number.isFinite(weightKg) || weightKg <= 0 || weightKg > 100_000) return setFormError('Weight must be between 0.01 and 100 000 kg.');
    if (isAdmin && !sender) return setFormError('Choose the sender.');
    const email = recipientEmail.trim();
    if (!recipient && !email) return setFormError(isAdmin ? 'Choose the recipient or enter their email.' : 'Enter the recipient’s email.');
    if (!recipient && !EMAIL_RE.test(email)) return setFormError('The recipient email is not valid.');
    if (!isAdmin && me.data && email.toLowerCase() === user?.email.toLowerCase()) return setFormError('You cannot send a shipment to yourself.');

    create.mutate({
      ...(isAdmin ? { senderClientId: sender!.id } : {}),
      ...(recipient ? { recipientClientId: recipient.id } : { recipientEmail: email }),
      weightKg,
      originAddress: origin.trim() || null,
      destinationAddress: destination.trim() || null,
      notes: notes.trim() || null,
    });
  }

  const defaultOrigin = isAdmin ? sender?.address : me.data?.address;

  return (
    <div className="stack">
      <div className="page-header">
        <h1>New shipment</h1>
        <p>
          {isAdmin
            ? 'Create a shipment on behalf of a client. It starts as Created and waits for dispatch.'
            : 'Send a parcel to another Last Mile customer. You will get a tracking number to share with them.'}
        </p>
      </div>

      <Card>
        <form className="form" onSubmit={submit} noValidate>
          <div className="form-grid">
            {isAdmin && <ClientPicker label="Sender" value={sender} onChange={setSender} excludeId={recipient?.id} />}
            {isAdmin ? (
              <div className="stack" style={{ gap: '0.5rem' }}>
                <ClientPicker label="Recipient" value={recipient} onChange={setRecipient} excludeId={sender?.id} />
                {!recipient && (
                  <TextField label="…or recipient email" type="email" value={recipientEmail} onChange={(e) => setRecipientEmail(e.target.value)} />
                )}
              </div>
            ) : (
              <TextField
                label="Recipient email"
                type="email"
                required
                value={recipientEmail}
                onChange={(e) => setRecipientEmail(e.target.value)}
                hint="The recipient must have a Last Mile customer account."
                autoComplete="off"
              />
            )}
            <TextField label="Weight (kg)" inputMode="decimal" required value={weight} onChange={(e) => setWeight(e.target.value)} placeholder="2.5" />
          </div>

          <div className="form-grid">
            <TextField
              label="Pick-up address (optional)"
              value={origin}
              onChange={(e) => setOrigin(e.target.value)}
              maxLength={1000}
              hint={defaultOrigin ? `Default: ${defaultOrigin}` : 'Default: the sender’s profile address'}
            />
            <TextField
              label="Delivery address (optional)"
              value={destination}
              onChange={(e) => setDestination(e.target.value)}
              maxLength={1000}
              hint={recipient ? `Default: ${recipient.address}` : 'Default: the recipient’s profile address'}
            />
          </div>

          <div className="field">
            <label htmlFor="notes">Notes (optional)</label>
            <textarea id="notes" maxLength={1000} value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Fragile, call before arriving…" />
            <span className="field-hint">Visible to the sender, the recipient and the driver. Never shown on the public tracking page.</span>
          </div>

          {(formError || create.error) && <Alert variant="error">{formError ?? getErrorMessage(create.error)}</Alert>}

          <div className="row">
            <Button type="submit" loading={create.isPending}>
              Create shipment
            </Button>
            <Link to="/shipments" className="btn btn-ghost">
              Cancel
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
