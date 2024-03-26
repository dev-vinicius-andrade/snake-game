import { AppSettings } from '@app/types/appSettings';
import { NG_ENV } from 'angular-server-side-configuration/ng-env';

console.log('environment', NG_ENV);
export const environment: AppSettings = {
  integrations: {
    api: {
      manager: {
        baseUrl:
          NG_ENV['INTEGRATIONS_API_MANAGER_BASEURL'] ??
          'https://localhost:60511',
      },
    },
  },
};
