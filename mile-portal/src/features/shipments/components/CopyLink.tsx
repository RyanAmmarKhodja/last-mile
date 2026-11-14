import { useState } from 'react';
import { Button } from '@/shared/components';

/** Copies the public tracking link (PRD persona 2: "a unique tracking link"). */
export function CopyTrackingLink({ trackingNumber }: { trackingNumber: string }) {
  const [copied, setCopied] = useState(false);
  const url = `${window.location.origin}/track/${trackingNumber}`;
  async function copy() {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      window.prompt('Copy the tracking link:', url);
    }
  }
  return (
    <Button size="sm" variant="secondary" onClick={copy} title={url}>
      {copied ? 'Link copied' : 'Copy tracking link'}
    </Button>
  );
}
