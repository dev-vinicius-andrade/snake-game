import { Component, Signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { JoinApiService } from '@app/services/api/join/join-api.service';

import { Nullable } from '@app/types/nullable';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { Router } from '@angular/router';
import { ManagerHubService } from '@app/services/hub/manager/manager-hub.service';
import { LocalStorageService } from '@app/services/localStorage/local-storage.service';
import { LocalStorageKey } from '@app/types/enums';
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [ButtonModule, InputTextModule, FormsModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  protected loading: boolean = false;
  protected joingPrivateRoom: boolean = false;
  protected privateRoomId?: string;
  protected joinRoomLabel: string;
  private managerHubService: ManagerHubService;
  private joinApi: JoinApiService;
  protected nickname?: Nullable<string>;
  protected roomId?: Nullable<string>;
  private localStorageService: LocalStorageService;
  constructor(
    managerService: ManagerHubService,
    joinApi: JoinApiService,
    private router: Router,
    localStorageService: LocalStorageService
  ) {
    this.localStorageService = localStorageService;
    this.joinRoomLabel = this.getJoinRoomLabel();
    this.managerHubService = managerService;
    this.joinApi = joinApi;
  }
  clearUserInfo() {
    this.localStorageService.remove(LocalStorageKey.playerTrackableId);
  }
  async onJoinAnyRoomClicked() {
    this.loading = true;
    this.joinRoomLabel = this.getJoinRoomLabel();
    this.clearUserInfo();
    await this.managerHubService.start();
    while (!this.managerHubService.isConnected()) {
      await new Promise((resolve) => setTimeout(resolve, 1000));
    }
    const connectionId = this.managerHubService.connectionId();
    if (!connectionId) {
      this.loading = false;
      this.joinRoomLabel = this.getJoinRoomLabel();
      return;
    }
    try {
      const response = await this.joinApi.joinAnyRoom(
        connectionId,
        this.nickname?.trim()
      );
      this.managerHubService.on('onJoinedRoom', (data: any) => {
        this.loading = false;
        this.joinRoomLabel = this.getJoinRoomLabel();
        const token = btoa(JSON.stringify(data));
        this.router.navigate([`/room/${token}`]);
      });
      console.log(response);
    } catch (error) {
      console.error(error);
    }
  }
  onJoinRoomClicked() {}
  private getJoinRoomLabel() {
    if (this.loading) return 'Joining...';
    return this.joingPrivateRoom ? 'Join Private Room' : 'Join Any Room';
  }
  isNicknameValid() {
    if (!this.nickname) return false;
    return this.nickname.trim().length > 0;
  }
}
