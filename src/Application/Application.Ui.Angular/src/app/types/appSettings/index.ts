export declare type AppSettings = {
  integrations: {
    api: IntegrationApiSettings;
    hubs: {};
  };
};
export declare type IntegrationApiSettings = {
  [key: string]: IntegrationApiDataSetting;
};
export declare type IntegrationApiDataSetting = {
  baseUrl: string;
};
