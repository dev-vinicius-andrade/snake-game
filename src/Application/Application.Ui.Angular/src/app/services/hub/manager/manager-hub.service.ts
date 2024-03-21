import { Injectable } from '@angular/core';
import { ConfigurationService } from '@app/services/configuration/configuration.service';
import { HubService } from '@app/services/hub/hub.service';
import { HubConnection } from '@microsoft/signalr';
import { Nullable } from 'primeng/ts-helpers';

@Injectable({
  providedIn: 'root',
})
export class ManagerHubService {
  private hubService: HubService;
  private hub?: HubConnection;
  private configuration: ConfigurationService;
  constructor(hubService: HubService, configuration: ConfigurationService) {
    this.hubService = hubService;
    this.configuration = configuration;
  }
  public async start() {
    this.hub = await this.hubService.buildHub(
      `${this.configuration.value.integrations.api['manager'].baseUrl}/manager`
    );
  }
  public async stop() {
    await this.hub?.stop();
  }
  public connectionId(): Nullable<string> {
    return this.hub?.connectionId;
  }
  public on(event: string, listener: (...args: any[]) => void) {
    this.hub?.on(event, listener);
  }
  public isConnected(): boolean {
    return this.hub?.state === 'Connected';
  }
}
