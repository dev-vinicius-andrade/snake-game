import { Injectable } from '@angular/core';
import { AxiosService } from '@app/services/axios/axios.service';
import { Nullable } from '@app/types/nullable';

@Injectable({
  providedIn: 'root',
})
export class JoinApiService {
  private axios: AxiosService;

  constructor(axios: AxiosService) {
    this.axios = axios;
  }
  async joinAnyRoom(connectionId: string, nickname: Nullable<string>) {
    if (!nickname) throw new Error('nickname is required');
    console.log(
      `Joining any room with connectionId ${connectionId} and nickname ${nickname}`
    );
    return await this.axios.post(
      '/join',
      JSON.stringify({
        id: connectionId,
        nickname,
      })
    );
  }
}
