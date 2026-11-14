import { refreshAccessToken } from './client';
import { tokenStore } from './tokenStore';

export interface SseMessage {
  event: string;
  data: string;
}

export type SseState = 'connecting' | 'open' | 'reconnecting' | 'forbidden' | 'closed';

export interface AuthorizedStreamOptions {
  url: string;
  onMessage: (msg: SseMessage) => void;
  onStateChange?: (state: SseState) => void;
  /** Backoff ceiling in ms (default 30 s). */
  maxBackoffMs?: number;
}

/**
 * Server-Sent Events over fetch, so the request can carry `Authorization` (EventSource cannot).
 * - 401 → one token refresh (shared single-flight refresh), then reconnect.
 * - 403 → stop for good.
 * - Anything else (network error, stream ended) → reconnect with exponential backoff.
 * Returns a function that closes the stream.
 */
export function openAuthorizedStream({ url, onMessage, onStateChange, maxBackoffMs = 30_000 }: AuthorizedStreamOptions): () => void {
  let closed = false;
  let controller: AbortController | null = null;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let attempt = 0;
  let refreshedThisAttempt = false;

  const setState = (s: SseState) => {
    if (!closed) onStateChange?.(s);
  };

  const scheduleReconnect = () => {
    if (closed) return;
    setState('reconnecting');
    const delay = Math.min(maxBackoffMs, 1000 * 2 ** Math.min(attempt, 5));
    attempt++;
    timer = setTimeout(connect, delay);
  };

  async function connect() {
    if (closed) return;
    controller = new AbortController();
    try {
      const token = tokenStore.getAccessToken();
      const res = await fetch(url, {
        headers: { Accept: 'text/event-stream', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        signal: controller.signal,
        cache: 'no-store',
      });

      if (res.status === 401 && !refreshedThisAttempt) {
        refreshedThisAttempt = true;
        try {
          await refreshAccessToken();
        } catch {
          tokenStore.clear();
          tokenStore.notifySessionExpired();
          setState('closed');
          return;
        }
        return void connect();
      }
      if (res.status === 403) return setState('forbidden');
      if (!res.ok || !res.body) throw new Error(`Stream failed with ${res.status}`);

      refreshedThisAttempt = false;
      attempt = 0;
      setState('open');
      await readEvents(res.body, onMessage);
      // The server ended the stream (deploy, timeout): reconnect.
      scheduleReconnect();
    } catch {
      if (closed) return;
      refreshedThisAttempt = false;
      scheduleReconnect();
    }
  }

  setState('connecting');
  void connect();

  return () => {
    closed = true;
    clearTimeout(timer);
    controller?.abort();
  };
}

/** Parses a text/event-stream body (event/data fields, blank line = dispatch). Comments and ids are ignored. */
async function readEvents(body: ReadableStream<Uint8Array>, onMessage: (msg: SseMessage) => void) {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let event = '';
  let data: string[] = [];
  for (;;) {
    const { value, done } = await reader.read();
    if (done) return;
    buffer += decoder.decode(value, { stream: true });
    let newline: number;
    while ((newline = buffer.search(/\r?\n/)) >= 0) {
      const line = buffer.slice(0, newline);
      buffer = buffer.slice(buffer[newline] === '\r' ? newline + 2 : newline + 1);
      if (line === '') {
        if (data.length) onMessage({ event: event || 'message', data: data.join('\n') });
        event = '';
        data = [];
      } else if (line.startsWith('event:')) {
        event = line.slice(6).trim();
      } else if (line.startsWith('data:')) {
        data.push(line.slice(5).replace(/^ /, ''));
      }
    }
  }
}
