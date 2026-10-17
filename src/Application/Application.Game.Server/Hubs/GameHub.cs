using Domain.Game.Simulation;
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
        private readonly IRoomInputQueue _roomInputQueue;
        private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);

        public GameHub(IPlayerService playerService, IPlayerRoomService playerRoomService, IRoomsService roomsService, ITrackableIdGeneratorService trackableIdGeneratorService, IGameStateService gameStateService, IRoomInputQueue roomInputQueue)
        {
            _roomInputQueue = roomInputQueue;
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
            var result = await RunInRoomAsync(roomId,
                new ReconnectPlayerCommand(roomTrackableId, playerTrackableId, Context.ConnectionId));
            if(result is null) return;
            await Groups.RemoveFromGroupAsync(result.OldConnectionId, roomId.ToString(),
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
            var player = await RunInRoomAsync(request.RoomId,
                new JoinNewPlayerCommand(Context.ConnectionId, request.Nickname, request.RoomId));
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

        public Task ChangeDirection(ChangeDirectionRequest request)
        {
            var (_, _, room) = GetConnectionData();
            if (room is not null)
                _roomInputQueue.Enqueue(room.Guid, new ChangeDirectionCommand(Context.ConnectionId, request.Direction));
            return Task.CompletedTask;
        }

        public async Task LeaveRoom(LeaveRoomRequest request)
        {
            var (_, gameState, room) = GetConnectionData();
            if (gameState is null || room is null) return;
            var player = await RunInRoomAsync(room.Guid, new LeaveCommand(Context.ConnectionId));
            if (player is null) return;
            await Clients.Group(room.Guid.ToString()).PlayerLefted(new PlayerLeftedEvent
            {
                RoomId = room.Guid,
                PlayerTrackableId = player.Guid,
                PlayerId = Context.ConnectionId
            });
            await Clients.Group(room.Guid.ToString()).ConnectedPlayersUpdated(new()
            {
                Players = gameState.Players.Values.ToList()
            });
        }

        public Task Boost(BoostRequest request)
        {
            var (_, _, room) = GetConnectionData();
            if (room is not null)
                _roomInputQueue.Enqueue(room.Guid, new SetBoostCommand(Context.ConnectionId, request.Active));
            return Task.CompletedTask;
        }

        public Task Shoot(ShootRequest request)
        {
            var (_, _, room) = GetConnectionData();
            if (room is not null)
                _roomInputQueue.Enqueue(room.Guid, new ShootCommand(Context.ConnectionId));
            return Task.CompletedTask;
        }

        private async Task<T> RunInRoomAsync<T>(Guid roomId, PlayerCommand<T> command)
        {
            _roomInputQueue.Enqueue(roomId, command);
            return await command.Completion.WaitAsync(CommandTimeout, Context.ConnectionAborted);
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
