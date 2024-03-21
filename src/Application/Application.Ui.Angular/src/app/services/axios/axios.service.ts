import { Injectable } from '@angular/core';
import { ConfigurationService } from '@app/services/configuration/configuration.service';
import { Axios, AxiosRequestConfig, AxiosResponse } from 'axios';
@Injectable({
  providedIn: 'root',
})
export class AxiosService {
  public axios: Axios;
  constructor(configuration: ConfigurationService) {
    this.axios = new Axios({
      baseURL: configuration.value.integrations.api['manager'].baseUrl,
      headers: {
        'Content-Type': 'application/json',
      },
    });
  }
  public async get(
    url: string,
    config?: AxiosRequestConfig<any> | undefined
  ): Promise<AxiosResponse<any, any>> {
    return await this.axios.get(url, config);
  }
  public async post(
    url: string,
    data?: any,
    config?: AxiosRequestConfig<any> | undefined
  ): Promise<AxiosResponse<any, any>> {
    return await this.axios.post(url, data, config);
  }
  public async put(
    url: string,
    data?: any,
    config?: AxiosRequestConfig<any> | undefined
  ): Promise<AxiosResponse<any, any>> {
    return await this.axios.put(url, data, config);
  }
  public async delete(
    url: string,
    config?: AxiosRequestConfig<any> | undefined
  ): Promise<AxiosResponse<any, any>> {
    return await this.axios.delete(url, config);
  }
  public async patch(
    url: string,
    data?: any,
    config?: AxiosRequestConfig<any> | undefined
  ): Promise<AxiosResponse<any, any>> {
    return await this.axios.patch(url, data, config);
  }
}
