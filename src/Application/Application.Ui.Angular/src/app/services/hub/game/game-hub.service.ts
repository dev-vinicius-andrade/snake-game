import { Injectable } from '@angular/core';
import { HubService } from '@app/services/hub/hub.service';
import { Nullable } from '@app/types/nullable';
import { HubConnection } from '@microsoft/signalr';

@Injectable({
  providedIn: 'root',
})
export class GameHubService {
  private hubService: HubService;
  private hub?: HubConnection;
  constructor(hubService: HubService) {
    this.hubService = hubService;
  }
  public async start(
    hubUrl: string,
    headers?: Nullable<Record<string, string>>
  ) {
    this.hub = await this.hubService.buildHub(hubUrl, { headers });
  }
  public async stop() {
    await this.hub?.stop();
  }
  public on(event: string, listener: (...args: any[]) => void) {
    this.hub?.on(event, listener);
  }
  public async send(event: string, ...args: any[]) {
    await this.hub?.send(event, ...args);
  }
  public isConnected(): boolean {
    return this.hub?.state === 'Connected';
  }
  public connectionId(): Nullable<string> {
    return this.hub?.connectionId;
  }
}
