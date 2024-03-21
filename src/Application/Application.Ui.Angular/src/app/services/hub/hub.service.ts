import { Injectable } from '@angular/core';
import { Nullable } from '@app/types/nullable';
import {
  HubConnection,
  LogLevel,
  HubConnectionBuilder,
  HttpClient,
  DefaultHttpClient,
  HttpResponse,
  HttpRequest,
  HttpTransportType,
} from '@microsoft/signalr';

@Injectable({
  providedIn: 'root',
})
export class HubService {
  public async buildHub(
    hubUrl: string,
    options?: {
      timeout?: number;
      reconnectPolicy?: number[];
      headers?: Nullable<Record<string, string>>;
    }
  ): Promise<HubConnection> {
    const normalizedRecconectPolicy = options?.reconnectPolicy ?? [
      1000, 1000, 1000, 1000, 1000, 1000,
    ];

    const normalizedTimeout = options?.timeout ?? 100000;
    let hubConnectionBuilder = new HubConnectionBuilder().withUrl(hubUrl, {
      headers: options?.headers ?? {},
    });
    hubConnectionBuilder.withAutomaticReconnect(normalizedRecconectPolicy);

    hubConnectionBuilder.configureLogging(LogLevel.Debug);
    let hub = hubConnectionBuilder.build();
    hub.serverTimeoutInMilliseconds = normalizedTimeout;
    hub
      .start()
      .then(async (p) => {})
      .catch((error) => {
        console.log(`error connecting on ${hubUrl}`, error);
      });
    return hub;
  }
}
