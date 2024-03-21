import { PositionWithColor } from '@app/types/position';
import { EventEmitter, Injectable } from '@angular/core';
import { GameCanvasComponent } from '@app/components/game/canvas/game-canvas/game-canvas.component';
import { KeyboardEventHandlerService } from '@app/services/event/handler/keyboard-event-handler.service';
import { GameHubService } from '@app/services/hub/game/game-hub.service';
import { Direction, LocalStorageKey } from '@app/types/enums';
import { GameState } from '@app/types/game/state';
import { Nullable } from '@app/types/nullable';
import { EventGamePlayerJoined } from '@app/types/events/game/player/joined';
import { LocalStorageService } from '@app/services/localStorage/local-storage.service';
import { EventGamePlayerLefted } from '@app/types/events/game/player/lefted';
import { Router } from '@angular/router';
import { EventGameScoreUpdated } from '@app/types/events/game/score/updated';
import { Score } from '@app/types/score';
import { EventGamePlayerDied } from '@app/types/events/game/player/died';

@Injectable({
  providedIn: 'root',
})
export class GameEngineService {
  keyboardEventHandler: KeyboardEventHandlerService;
  private currentDirection: Nullable<Direction> = null;
  private localStorageService: LocalStorageService;
  private router: Router;
  public scoreChanged = new EventEmitter<Score[]>();
  public currentPlayerScoreChanged = new EventEmitter<Nullable<number>>();
  public currentPlayerDied = new EventEmitter<boolean>();
  public isConnectedStatusChanged = new EventEmitter<boolean>();

  public get isConnected(): boolean {
    if (!this.gameService) return false;
    return this.gameService.isConnected();
  }

  gameService: GameHubService;
  constructor(
    router: Router,
    gameHubService: GameHubService,
    keyboardEventHandler: KeyboardEventHandlerService,
    localStorageService: LocalStorageService
  ) {
    this.router = router;
    this.gameService = gameHubService;
    this.keyboardEventHandler = keyboardEventHandler;
    this.localStorageService = localStorageService;
  }
  async startHub(hubUrl: any) {
    await this.gameService.start(hubUrl);
    while (!this.gameService.isConnected()) {
      await new Promise((resolve) => setTimeout(resolve, 1000));
    }
    this.isConnectedStatusChanged.emit(this.gameService.isConnected());
  }
  async stopHub() {
    await this.gameService?.stop();
    this.isConnectedStatusChanged.emit(this.gameService.isConnected());
  }
  get connectionId(): Nullable<string> {
    return this.gameService.connectionId();
  }
  async joinRoom(nickname: any, roomId: Nullable<string>) {
    const playerTrackableId = this.localStorageService.get(
      LocalStorageKey.playerTrackableId
    );
    await this.gameService.send('JoinRoom', {
      roomId: roomId,
      nickname: nickname,
      playerTrackableId,
    });
  }
  async leaveRoom(roomId: string) {
    const playerTrackableId = this.localStorageService.get(
      LocalStorageKey.playerTrackableId
    );
    await this.gameService.send('LeaveRoom', {
      roomId,
      playerTrackableId,
    });
  }
  clearUserInfo() {
    this.localStorageService.remove(LocalStorageKey.playerTrackableId);
  }
  setupEvents(canvas: GameCanvasComponent) {
    this.setupRoomDetailsUpdatedEvent(canvas);
    this.setupPlayerJoinedEvent(canvas);
    this.setupFoodGeneratedEvent(canvas);
    this.setupGameStateUpdatedEvent(canvas);
    this.setupPlayerLeftEvent();
    this.setupScoreUpdatedEvent();
    this.setupPlayerScoreUpdatedEvent();
    this.setupPlayerDiedEvent();
    canvas.keyboardEvent.subscribe((event) => {
      if (!event) return;
      if (event.code === 'Space') {
        this.gameService.send('Shoot', {});
        return;
      }
      const direction = this.keyboardEventHandler.buildDiretion(event);
      if (!direction) return;
      if (this.currentDirection === direction) return;
      this.currentDirection = direction;
      this.gameService.send('ChangeDirection', { direction });
    });
  }
  setupPlayerDiedEvent() {
    this.gameService.on('PlayerDied', async (data: EventGamePlayerDied) => {
      this.currentPlayerScoreChanged?.emit(data.score);
      this.currentPlayerDied.emit(true);
      this.clearUserInfo();
    });
  }
  setupPlayerScoreUpdatedEvent() {
    this.gameService.on('PlayerScoreUpdated', async (score: number) => {
      this.currentPlayerScoreChanged?.emit(score);
    });
  }
  setupScoreUpdatedEvent() {
    this.gameService.on('ScoreUpdated', async (data: EventGameScoreUpdated) => {
      this.scoreChanged.emit(data);
    });
  }

  setupGameStateUpdatedEvent(canvas: GameCanvasComponent) {
    this.gameService.on('GameStateUpdated', async (data: GameState) => {
      canvas.clear();
      this.drawPlayers(data, canvas);
      this.drawFoods(data, canvas);
      this.drawObstacles(data, canvas);
    });
  }
  drawObstacles(data: GameState, canvas: GameCanvasComponent) {
    Object.keys(data.obstacles).forEach((obstacleId) => {
      const obstacle = data.obstacles[obstacleId];
      canvas?.drawRectangle(
        '#fff',
        {
          x: obstacle.currentPosition.x,
          y: obstacle.currentPosition.y,
        },
        obstacle.size / 3
      );
    });
  }
  drawFoods(data: GameState, canvas: GameCanvasComponent) {
    Object.keys(data.foods).forEach((foodId) => {
      const food = data.foods[foodId];
      canvas?.drawRectangle(
        food.color.fillColor,
        {
          x: food.position.x,
          y: food.position.y,
        },
        10,
        food.color.border
      );
    });
  }
  private drawPlayers(data: GameState, canvas: GameCanvasComponent) {
    Object.keys(data.players).forEach((playerGuid) => {
      const player = data.players[playerGuid];
      const paths = player.path.map((p) => {
        return {
          x: p.x,
          y: p.y,
          color: player.backgroundColor,
        } as PositionWithColor;
      });
      canvas.drawPath(paths, 10);
    });
  }

  private setupFoodGeneratedEvent(canvas: GameCanvasComponent) {
    this.gameService.on('FoodGenerated', (data: any) => {});
  }

  private setupPlayerJoinedEvent(canvas: GameCanvasComponent) {
    this.gameService.on('PlayerJoined', (data: EventGamePlayerJoined) => {
      if (data.player.id === this.connectionId) {
        this.localStorageService.set(
          LocalStorageKey.playerTrackableId,
          data.player.guid
        );
      }
    });
  }
  setupPlayerLeftEvent() {
    this.gameService.on('PlayerLefted', async (data: EventGamePlayerLefted) => {
      if (data.playerId === this.connectionId) {
        this.clearUserInfo();
        await this.stopHub();
        this.router.navigate(['']);
      }
    });
  }
  private setupRoomDetailsUpdatedEvent(canvas: GameCanvasComponent) {
    this.gameService.on('RoomDetailsUpdated', (data: any) => {
      canvas.setDimensions(data.width, data.height);
    });
  }
}
