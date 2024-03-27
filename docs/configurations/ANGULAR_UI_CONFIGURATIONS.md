# Angular Ui Configurations 

The angular configurations are stored in [environment.ts](https://github.com/dev-vinicius-andrade/snake-game/blob/main/src/Application/Application.Ui.Angular/src/environments/environment.ts) file

But mainly is just the Manager Api Url

```typescript
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
```


As we are using the [angular-server-side-configuration](https://www.npmjs.com/package/angular-server-side-configuration) we can use the environment variables to set the configurations

So to override the value of the manager api url we can use the `INTEGRATIONS_API_MANAGER_BASEURL` environment variable

