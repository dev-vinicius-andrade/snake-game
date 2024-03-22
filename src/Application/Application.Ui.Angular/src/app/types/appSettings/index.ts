export declare type AppSettings = {
  integrations: {
    api: IntegrationApiSettings;
  };
};
export declare type IntegrationApiSettings = {
  [key: string]: IntegrationApiDataSetting;
};
export declare type IntegrationApiDataSetting = {
  baseUrl: string;
};
