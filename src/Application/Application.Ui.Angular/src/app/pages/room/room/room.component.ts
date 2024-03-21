import {
  AfterContentInit,
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ViewChild,
} from '@angular/core';
import { Params, Router } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { Nullable } from '@app/types/nullable';
import { GameCanvasComponent } from '@app/components/game/canvas/game-canvas/game-canvas.component';
import { GameEngineService } from '@app/services/game/game-engine.service';
import { ButtonModule } from 'primeng/button';
import { Score } from '@app/types/score';
import { OverlayPanelModule } from 'primeng/overlaypanel';
import { TooltipModule } from 'primeng/tooltip';
@Component({
  selector: 'app-room',
  standalone: true,
  imports: [
    GameCanvasComponent,
    ButtonModule,
    OverlayPanelModule,
    TooltipModule,
  ],
  templateUrl: './room.component.html',
  styleUrl: './room.component.scss',
})
export class RoomComponent implements AfterViewInit {
  @ViewChild(GameCanvasComponent, { static: false })
  gameCanvasComponent: Nullable<GameCanvasComponent>;
  private router: Router;
  private route: ActivatedRoute;
  protected roomId?: string;
  private userId?: string;
  protected userConnectionId?: Nullable<string>;
  protected connectedPlayers: string[] = [];
  protected roomWidth?: number;
  protected roomHeight?: number;
  protected gameEngine: GameEngineService;
  protected scores: Score[] = [];
  protected showInformationDialog: boolean = false;
  protected currentScore: Nullable<number>;
  protected died: boolean = false;
  protected connected = false;
  protected get connectionIconCssClasses(): string {
    const classes = [
      'pi',
      'pi-circle-fill',
      'animate-ping',
      'mr-2',
      'text-[5px]',
      'cursor-pointer',
      'scale-100',
    ];
    if (this.gameEngine.isConnected) {
      classes.push('text-green-500');
    } else {
      classes.push('text-red-500');
    }
    return classes.join(' ');
  }
  protected get connectionLabel(): string {
    return this.gameEngine.isConnected ? 'Connected' : 'Disconnected';
  }
  constructor(
    router: Router,
    route: ActivatedRoute,
    gameEngine: GameEngineService,
    private cdr: ChangeDetectorRef
  ) {
    this.router = router;
    this.route = route;
    this.gameEngine = gameEngine;
  }
  ngAfterViewInit(): void {
    this.route.params.subscribe(async (params) => {
      const { hubUrl, tokenData } = this.getRouteParams(params);
      await this.gameEngine.startHub(hubUrl);
      if (!this.gameCanvasComponent) return;
      this.userConnectionId = this.gameEngine.connectionId;
      this.gameEngine.setupEvents(this.gameCanvasComponent);

      await this.sendJoinRoomRequest(tokenData);
      this.cdr.detectChanges();
    });
  }
  async ngOnInit() {
    this.gameEngine.scoreChanged.subscribe((scores) => {
      this.scores = scores;
    });
    this.gameEngine.currentPlayerScoreChanged.subscribe((score) => {
      this.currentScore = score;
    });
    this.gameEngine.currentPlayerDied.subscribe((died) => {
      this.died = died;
    });
    this.gameEngine.isConnectedStatusChanged.subscribe((connected) => {
      this.connected = connected;
      this.cdr.detectChanges();
    });
  }
  private async sendJoinRoomRequest(tokenData: any) {
    await this.gameEngine.joinRoom(tokenData.nickname, this.roomId);
  }
  setShowInformationDialog(show: boolean) {
    this.showInformationDialog = show;
  }
  private getRouteParams(params: Params) {
    const token = params['token'];
    const decodedToken = atob(token);
    const tokenData = JSON.parse(decodedToken);
    this.roomId = tokenData.roomId;
    const hubUrl = tokenData.gameServerUrl;
    this.validateRouteParamsData(hubUrl, tokenData);
    return { hubUrl, tokenData };
  }

  private validateRouteParamsData(hubUrl: any, tokenData: any) {
    if (!hubUrl) {
      throw new Error('hubUrl is required');
    }
    if (!this.roomId) {
      throw new Error('roomId is required');
    }
    if (!tokenData.nickname) {
      throw new Error('nickname is required');
    }
  }
  protected async leaveRoom() {
    if (this.roomId) await this.gameEngine.leaveRoom(this.roomId);
    this.router.navigate(['']);
  }
  isRoomsDimensionsValid() {
    if (this.roomWidth == null || this.roomWidth == undefined) return false;
    if (this.roomHeight == null || this.roomHeight == undefined) return false;
    return this.roomWidth && this.roomHeight;
  }
}
