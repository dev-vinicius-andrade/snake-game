import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

type Handler = (...args: any[]) => void;

export type HubConnectionState = "disconnected" | "connecting" | "connected" | "reconnecting" | "disconnecting";

/** The slice of a SignalR connection the app depends on. Everything is read lazily, so values are never stale. */
export interface HubClient {
  readonly connectionId: string | null;
  readonly state: HubConnectionState;
  start(): Promise<void>;
  stop(): Promise<void>;
  on(event: string, handler: Handler): void;
  off(event: string, handler: Handler): void;
  invoke<T = void>(method: string, ...args: unknown[]): Promise<T>;
  onClose(handler: (error?: Error) => void): void;
  onReconnecting(handler: (error?: Error) => void): void;
  onReconnected(handler: (connectionId?: string) => void): void;
}

export type HubClientFactory = (url: string) => HubClient;

/** Structural subset of `@microsoft/signalr`'s `HubConnection`, so it can be faked in specs. */
export interface SignalRConnectionLike {
  readonly connectionId: string | null;
  readonly state: string;
  start(): Promise<void>;
  stop(): Promise<void>;
  on(methodName: string, handler: Handler): void;
  off(methodName: string, handler: Handler): void;
  invoke(methodName: string, ...args: any[]): Promise<any>;
  onclose(callback: (error?: Error) => void): void;
  onreconnecting(callback: (error?: Error) => void): void;
  onreconnected(callback: (connectionId?: string) => void): void;
}

const STATES: readonly HubConnectionState[] = [
  "disconnected",
  "connecting",
  "connected",
  "reconnecting",
  "disconnecting",
];

function mapState(state: string): HubConnectionState {
  const normalized = state.toLowerCase();
  return STATES.find((known) => known === normalized) ?? "disconnected";
}

export function adaptConnection(connection: SignalRConnectionLike): HubClient {
  return {
    get connectionId() {
      return connection.connectionId;
    },
    get state() {
      return mapState(connection.state);
    },
    start: () => connection.start(),
    stop: () => connection.stop(),
    on: (event, handler) => connection.on(event, handler),
    off: (event, handler) => connection.off(event, handler),
    invoke: <T = void>(method: string, ...args: unknown[]) => connection.invoke(method, ...args) as Promise<T>,
    onClose: (handler) => connection.onclose(handler),
    onReconnecting: (handler) => connection.onreconnecting(handler),
    onReconnected: (handler) => connection.onreconnected(handler),
  };
}

export const createHubClient: HubClientFactory = (url) =>
  adaptConnection(
    new HubConnectionBuilder().withUrl(url).withAutomaticReconnect().configureLogging(LogLevel.Warning).build()
  );
