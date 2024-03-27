# Game Server Configurations

The game server configurations are stored inside the [here](../../src/Application/Application.Game.Server/Configurations/appsettings.json).

All the configurations can be overrided using the .Net Core environment variables [pattern](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0)


I'll not focus on the default .Net, Serilog configurations, if you want you can take a look in their official documentation.
- [Serilog](https://github.com/serilog/serilog-settings-configuration)
- [.Net Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0)


# AppSettings

The appSettings object is designed to configure some of the bootstrap configurations of the game server.

```csharp 
    public CorsConfiguration CorsConfiguration { get; set; } = null!;
    public EventbusConfiguration EventbusConfiguration { get; set; } = null!;
    public ApiIntegrationConfiguration ManagementApiConfiguration { get; set; } = null!;
```

| Property | Description | Default Value | Required | Related Configurations |
| --- | --- | --- | --- | --- |
| CorsConfiguration | The Cors Configurations | null | true | [CorsConfigurations](#appsettings-cors-configurations) |
| EventbusConfiguration | The Event Bus Configurations | null | true | [EventBusConfigurations](#appsettings-event-bus-configurations) |
| ManagementApiConfiguration | The Management Api Configurations | null | true | [ManagementApiConfiguration](#appsettings-management-api-configuration) |


## AppSettings Cors Configurations

The Cors Configurations are used to configure the cross origin resource sharing configurations.

```csharp
    public bool AllowAll { get; set; } = true;
    public string PolicyName { get; set; } = CorsPolicyNames.DefaultCorsPolicy;
    public List<string> AllowedMethods { get; set; } = new();
    public List<string> AllowedOrigins { get; set; } = new();
    public List<string> AllowedCredentials { get; set; } = new();
    public List<string> AllowedHeaders { get; set; } = new();
```

| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| AllowAll | Allow all origins, headers, methods, and credentials | true | false |
| PolicyName | The name of the policy | DefaultCorsPolicy | false |
| AllowedMethods | The allowed methods that other cross origin resources can access | Empty List | false (true if **AllowAll** is set to false) |
| AllowedOrigins | The allowed origins that other cross origin resources can access | Empty List | false (true if **AllowAll** is set to false) |
| AllowedCredentials | The allowed credentials that other cross origin resources can access | Empty List | false (true if **AllowAll** is set to false) |
| AllowedHeaders | The allowed headers that other cross origin resources can access | Empty List | false (true if **AllowAll** is set to false) |

If the AllowAll is set to true, the AllowedMethods, AllowedOrigins, AllowedCredentials, and AllowedHeaders will be ignored.


## AppSettings Event Bus Configurations

The EventBus Configurations are used to configure the connection string(uri) of the event bus.

```csharp
	public string ConnectionString { get; set; } = null!;
```

| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| ConnectionString | The connection string of the event bus | null | true |

As we are using RabbitMQ as the event bus, you can take a look at the [RabbitMQ Connection String](https://www.rabbitmq.com/uri-spec.html) documentation.


## AppSettings Management Api Configuration

The Management Api Configurations is used to configure the communication between the game server and the management api.

```csharp
    public string BaseUrl { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
```

| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| BaseUrl | The base url of the management api | null | true |
| ApiKey | The api key of the management api | null | true |

As I said before, I didn't focus on adding a reliable authentication mechanism, so I'm using a simple api key to authenticate the game server with the management api and avoid unauthorized access.




# Server Configurations


The server configurations are used to define the core configurations of the game server.

- [Server Configurations Host Information](#server-configurations-host-information)
- [Server Configuration Rooms Configuration](#server-configuration-rooms-configuration)
- [Server Configuration Food Generation Configuration](#server-configuration-food-generation-configuration)
- [Server Configuration Snake Configuration](#server-configuration-snake-configuration)




## Server Configurations Host Information

In the root of the **ServerConfiguration** object, you can find the bellows properties that are used to define some of the host informations,
Those informations are used to build the request object that is send to the management api, to notify the user in which game server it cans connect.

```csharp
	public string Domain { get; set; } = "localhost";
	public string Scheme { get; set; } = null;
	public string? Path { get; set; } = null;
    public int? Port { get; set; };

```


| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| Domain | The domain of the game server | localhost | false |
| Scheme | The scheme of the game server | null | false |
| Path | The path of the game server | null | false |
| Port | The port of the game server | null | false |


## Server Configuration Rooms Configuration

The Rooms Configuration is mainly used to define the dimensions of the room and how many rooms, players per room, foods per room the game server can handle.


```csharp
	public int MaxRooms { get; set; } = null!;
	public int MaxPlayersPerRoom { get; set; } = null!;
	public int MaxFoodsPerRoom { get; set; } = null!;
	public int RoomWidth { get; set; } = null!;
	public int RoomHeight { get; set; } = null!;
```

| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| MaxRooms | The maximum number of rooms that the game server can handle | null | true |
| MaxPlayersPerRoom | The maximum number of players that a room can handle | null | true |
| MaxFoodsPerRoom | The maximum number of foods that a room can handle | null | true |
| RoomWidth | The width of the room | null | true |
| RoomHeight | The height of the room | null | true |


## Server Configuration Food Generation Configuration

The Food Generation Configuration is used to define the food generation configurations of the game server.

```csharp
	public int FoodGenerationInterval { get; set; } = null!;
	public int FoodSize { get; set; } = null!;
```

| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| FoodGenerationInterval | The interval of the food generation in milliseconds | null | true |
| FoodSize | The size of the food | null | true |


## Server Configuration Snake Configuration

The Snake Configuration is used to define the snake configurations of the game server.

```csharp
    public double Speed{get;set;}= null!;
    public int HeadSize{get;set;}= null!;
    public int InitialSnakeSize { get; set; }= null!;
```

| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| Speed | The speed of the snake | null | true |
| HeadSize | The size of the snake head | null | true |
| InitialSnakeSize | The initial size of the snake | null | true |



# Notes

Normally the **SnakeConfiguration.HeadSize** should be equals to the **FoodConfiguration.FoodSize** to avoid any collision issues between the snake head and the food.