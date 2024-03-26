using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Commons.Game.Server.Entities.Hub.Events;
using Library.Commons.Game.Server.Entities.Hub.Requests;
using Library.Commons.Game.Server.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Application.Game.Server.Hubs
{
    public class GameHub : Hub<IGameHubSendEvents>, IGameHubListenEvents
    {
        private readonly IPlayerService _playerService;
        private readonly IPlayerRoomService _playerRoomService;
        private readonly IRoomsService _roomsService;
        private readonly ITrackableIdGeneratorService _trackableIdGeneratorService;
        private readonly IGameStateService _gameStateService;

        public GameHub(IPlayerService playerService, IPlayerRoomService playerRoomService, IRoomsService roomsService, ITrackableIdGeneratorService trackableIdGeneratorService, IGameStateService gameStateService)
        {
            _playerService = playerService;
            _playerRoomService = playerRoomService;
            _roomsService = roomsService;
            _trackableIdGeneratorService = trackableIdGeneratorService;
            _gameStateService = gameStateService;
        }

        public Hub Hub => this;

        public async Task JoinRoom(JoinRoomRequest request)
        {
            if(!request.PlayerTrackableId.HasValue) 
                await JoinAsNewPlayer(request);
            else
                await JoinAsExistingPlayer(request.RoomId,request.PlayerTrackableId.Value);
        }

        private async Task JoinAsExistingPlayer(Guid roomId,Guid playerId)
        {
            var roomTrackableId = _trackableIdGeneratorService.Generate(roomId);
            var playerTrackableId = _trackableIdGeneratorService.Generate(playerId);
            var result =
                await _gameStateService.ReconnectPlayer(roomTrackableId, playerTrackableId, Context.ConnectionId);
            if(!result.HasValue) return;
            await Groups.RemoveFromGroupAsync(result.Value.oldPlayerConnectionId, roomId.ToString(),
                Context.ConnectionAborted);
            var gameState = _gameStateService.GetGameState(roomTrackableId);
            if(gameState == null) return;
            await Groups.AddToGroupAsync(Context.ConnectionId, gameState.Room.Guid.ToString(), Context.ConnectionAborted);
            await Clients.Client(Context.ConnectionId)
                .RoomDetailsUpdated(new() { Width = gameState.Room.Width, Height = gameState.Room.Height });
            await Clients.Group(gameState.Room.Guid.ToString()).GameStateUpdated(gameState);

            await Clients.Group(gameState.Room.Guid.ToString()).ConnectedPlayersUpdated(new()
            {
                Players = gameState.Players.Values.ToList()
            });
        }

        private async Task JoinAsNewPlayer(JoinRoomRequest request)
        {
            var player =
                await _gameStateService.CreatePlayerInRoom(Context.ConnectionId, request.Nickname, request.RoomId,
                    Context.ConnectionAborted);
            if (player is null)
                return;

            var gameState = _gameStateService.GetGameState(_trackableIdGeneratorService.Generate(request.RoomId));

            await Groups.AddToGroupAsync(Context.ConnectionId, gameState.Room.Guid.ToString(), Context.ConnectionAborted);
            await Clients.Client(Context.ConnectionId)
                .RoomDetailsUpdated(new() { Width = gameState.Room.Width, Height = gameState.Room.Height });
            await Clients.Group(gameState.Room.Guid.ToString()).PlayerJoined(new PlayerJoinedEvent
            {
                Player = player,
            });
            await Clients.Group(gameState.Room.Guid.ToString()).GameStateUpdated(gameState);

            await Clients.Group(gameState.Room.Guid.ToString()).ConnectedPlayersUpdated(new()
            {
                Players = gameState.Players.Values.ToList()
            });
        }

        public async Task ChangeDirection(ChangeDirectionRequest request)
        {
            var (player, gameState,room) = GetConnectionData();
            var direction = request.Direction;
            if (!_gameStateService.ChangeDirectionByPlayerId(Context.ConnectionId, direction))
                return;

        }

        public async Task LeaveRoom(LeaveRoomRequest request)
        {
            var player = _playerService.Get(Context.ConnectionId);
            if (player is null)
            {
                await base.OnDisconnectedAsync(null);
                return;
            }
            var trackableRoomId = _playerRoomService.GetRoomTrackableId(player);

            if (trackableRoomId is null)
            {
                await base.OnDisconnectedAsync(null);
                return;
            }
            var room = _roomsService.GetRoom(trackableRoomId);
            if (room is null)
            {
                await base.OnDisconnectedAsync(null);
                return;
            }
            _playerRoomService.DisconnectPlayer(player, room);
            var gameState = _gameStateService.GetGameState(room);
            if (gameState is null)
            {
                await base.OnDisconnectedAsync(null);
                return;
            }
            gameState.Players.Remove(player.Guid);
            await Clients.Group(gameState.Room.Guid.ToString()).PlayerLefted(new PlayerLeftedEvent
            {
                RoomId = gameState.Room.Guid,
                PlayerTrackableId = player.Guid,
                PlayerId = Context.ConnectionId

            });
            await Clients.Group(gameState.Room.Guid.ToString()).ConnectedPlayersUpdated(new()
            {
                Players = gameState.Players.Values.ToList()
            });

        }

        public async Task Shoot(ShootRequest request)
        {
            var (player, gameState, room) = GetConnectionData();
            if (player is null || gameState is null || room is null)
                return;
            await _gameStateService.Shoot(gameState,room,player);
        }

        private (IPlayer? player, IGameState? gameState, IRoom? room) GetConnectionData()
        {
            var player = _playerService.Get(Context.ConnectionId);
            if (player is null)
                return (null,null,null);
            var trackableRoomId = _playerRoomService.GetRoomTrackableId(player);

            if (trackableRoomId is null)
                return (player, null,null);
            var room = _roomsService.GetRoom(trackableRoomId);
            if (room is null)
                return (player, null,null);
            var gameState = _gameStateService.GetGameState(room);
            if (gameState is null)
                return (player, null,room);
            return (player, gameState,room);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
