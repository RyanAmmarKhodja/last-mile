import { TrackForm } from './TrackForm';

export function TrackLookupPage() {
  return (
    <div className="track-hero">
      <h1>Track a parcel</h1>
      <p className="muted">Enter the tracking number from your shipment. No account needed.</p>
      <TrackForm />
    </div>
  );
}
