import { useEffect, useState } from 'react';
import { BridgeError, invoke, type HostKind } from './bridge';
import { errorMessage, strings } from './strings';

type Status = { kind: 'connecting' } | { kind: 'connected'; host: HostKind } | { kind: 'failed'; message: string };

export function App() {
  const [status, setStatus] = useState<Status>({ kind: 'connecting' });

  useEffect(() => {
    let active = true;
    invoke('system.ping', { message: 'ping' })
      .then((response) => active && setStatus({ kind: 'connected', host: response.host }))
      .catch((error: unknown) => {
        const code = error instanceof BridgeError ? error.code : 'internal';
        if (active) setStatus({ kind: 'failed', message: errorMessage(code) });
      });
    return () => {
      active = false;
    };
  }, []);

  return (
    <main>
      <h1>{strings.appName}</h1>
      <p>{strings.appTagline}</p>
      <p role="status" data-testid="bridge-status">
        {status.kind === 'connecting' && strings.bridge.connecting}
        {status.kind === 'connected' && `${strings.bridge.connected}: ${strings.bridge.hosts[status.host]}`}
        {status.kind === 'failed' && status.message}
      </p>
    </main>
  );
}
