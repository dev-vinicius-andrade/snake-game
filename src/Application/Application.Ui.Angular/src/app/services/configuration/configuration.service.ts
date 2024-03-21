import { Injectable } from '@angular/core';
import { AppSettings } from '@app/types/appSettings';

@Injectable({
  providedIn: 'root',
})
export class ConfigurationService {
  private settings: AppSettings;
  constructor() {
    this.settings = {
      integrations: {
        api: {
          manager: {
            baseUrl: 'https://localhost:60516',
          },
        },
        hubs: {},
      },
    };
  }
  get value(): AppSettings {
    return this.settings;
  }
}
