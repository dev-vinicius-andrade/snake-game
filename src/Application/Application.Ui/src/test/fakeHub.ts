import type { HubClient, HubConnectionState } from "@/services/hub";

type Handler = (...args: any[]) => void;

/** In-memory HubClient for specs. `log` records the order of calls so specs can assert sequencing. */
export class FakeHubClient implements HubClient {
  connectionId: string | null = null;
  state: HubConnectionState = "disconnected";
  nextConnectionId = "conn-1";
  startError?: Error;
  invokeError?: Error;
  readonly log: string[] = [];
  readonly invocations: Array<{ method: string; args: unknown[] }> = [];
  private readonly handlers = new Map<string, Set<Handler>>();
  private closeHandlers: Array<(error?: Error) => void> = [];
  private reconnectingHandlers: Array<(error?: Error) => void> = [];
  private reconnectedHandlers: Array<(connectionId?: string) => void> = [];

  constructor(readonly url = "") {}

  async start() {
    this.log.push("start");
    if (this.startError) throw this.startError;
    this.state = "connected";
    this.connectionId = this.nextConnectionId;
  }

  async stop() {
    this.log.push("stop");
    this.state = "disconnected";
  }

  on(event: string, handler: Handler) {
    this.log.push(`on:${event}`);
    const handlers = this.handlers.get(event) ?? new Set<Handler>();
    handlers.add(handler);
    this.handlers.set(event, handlers);
  }

  off(event: string, handler: Handler) {
    this.log.push(`off:${event}`);
    this.handlers.get(event)?.delete(handler);
  }

  async invoke<T = void>(method: string, ...args: unknown[]): Promise<T> {
    this.log.push(`invoke:${method}`);
    this.invocations.push({ method, args });
    if (this.invokeError) throw this.invokeError;
    return undefined as T;
  }

  onClose(handler: (error?: Error) => void) {
    this.closeHandlers.push(handler);
  }

  onReconnecting(handler: (error?: Error) => void) {
    this.reconnectingHandlers.push(handler);
  }

  onReconnected(handler: (connectionId?: string) => void) {
    this.reconnectedHandlers.push(handler);
  }

  handlerCount(event: string) {
    return this.handlers.get(event)?.size ?? 0;
  }

  invocationsOf(method: string) {
    return this.invocations.filter((invocation) => invocation.method === method);
  }

  emit(event: string, ...args: unknown[]) {
    this.handlers.get(event)?.forEach((handler) => handler(...args));
  }

  simulateClose(error?: Error) {
    this.state = "disconnected";
    this.closeHandlers.forEach((handler) => handler(error));
  }

  simulateReconnecting() {
    this.state = "reconnecting";
    this.reconnectingHandlers.forEach((handler) => handler());
  }

  simulateReconnected(newConnectionId: string) {
    this.state = "connected";
    this.connectionId = newConnectionId;
    this.reconnectedHandlers.forEach((handler) => handler(newConnectionId));
  }
}
