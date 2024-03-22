import { Injectable } from '@angular/core';
import { AppSettings } from '@app/types/appSettings';
import { environment } from '@env/environment';
@Injectable({
  providedIn: 'root',
})
export class ConfigurationService {
  private settings: AppSettings;
  constructor() {
    this.settings = environment;
  }
  get value(): AppSettings {
    return this.settings;
  }
}
