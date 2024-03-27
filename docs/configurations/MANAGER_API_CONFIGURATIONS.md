# Manager Api Configurations

The manager api configurations are stored inside the [here](../../src/Application/Application.Manager.Api/Configurations/appsettings.json).

All the configurations can be overrided using the .Net Core environment variables [pattern](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0)


I'll not focus on the default .Net, Serilog configurations, if you want you can take a look in their official documentation.
- [Serilog](https://github.com/serilog/serilog-settings-configuration)
- [.Net Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0)


# AppSettings

The appsettings is designed to configure some of the bootstrap configurations of the manager api.

```csharp
    public SwaggerConfiguration SwaggerConfiguration { get; set; } = null!;
    public CorsConfiguration CorsConfiguration { get; set; } = null!;
    public AuthConfiguration? Auth { get; set; }
    public RedisConfiguration RedisConfiguration { get; set; } = null!;
    public EventbusConfiguration EventbusConfiguration { get; set; } = null!;
```


## SwaggerConfiguration

The swagger configuration is used to configure the swagger documentation of the manager api.

```csharp
    public string Title { get; set; }
    public string Name { get; set; }
    public string Endpoint { get; set; } = "/swagger/v1/swagger.json";
    public string Version { get; set; } = "v1";
    public string Description { get; set; }
    public string Project { get; set; } = string.Empty;
    public List<string> Servers { get; set; } = new();
    public OpenApiSecurityScheme? AuthenticationConfiguration { get; set; }
```

| Property | Description | Default | Required |
| --- | --- | --- | --- |
| Title | The title of the swagger documentation | - | true |
| Name | The name of the swagger documentation | - | true |
| Endpoint | The endpoint of the swagger documentation | /swagger/v1/swagger.json | false |
| Version | The version of the swagger documentation | v1 | false |
| Description | The description of the swagger documentation | - | true |
| Project | The project of the swagger documentation | - | false |
| Servers | The servers of the swagger documentation | - | false |
| AuthenticationConfiguration | The authentication configuration of the swagger documentation | - | false |


You can find more information about the AuthenticationConfiguration [here](https://learn.microsoft.com/en-us/dotnet/api/microsoft.openapi.models.openapisecurityscheme)


## Cors Configurations

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

## Redis Configuration

The redis configuration is used to help SignalR to use redis to persist the connections.
In the future, I plan to use redis to persist the server state, like total rooms, total players, etc.


```csharp
	public string Host { get; set; } = null!;
    public int? Port { get; set; }
    public string? ChannelPrefix { get; set; }
    public string? Password { get; set; }
```


| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| Host | The host of the redis server | - | true |
| Port | The port of the redis server | 6379 | false |
| ChannelPrefix | The channel prefix of the redis server | - | false |
| Password | The password of the redis server | - | false |

## Eventbus Configuration

The EventBus Configurations are used to configure the connection string(uri) of the event bus.

```csharp
	public string ConnectionString { get; set; } = null!;
```

| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| ConnectionString | The connection string of the event bus | null | true |

As we are using RabbitMQ as the event bus, you can take a look at the [RabbitMQ Connection String](https://www.rabbitmq.com/uri-spec.html) documentation.

## Auth Configuration
The auth configuration is used to define the valid api keys that the game server can use to authenticate with the manager api.

```csharp
    public List<string>? ApiKeys { get; set; }
```
As I said before, I didn't focus on adding a reliable authentication mechanism, so I'm using a simple api key to authenticate the game server with the management api and avoid unauthorized access.


| Property | Description | Default Value | Required |
| --- | --- | --- | --- |
| ApiKeys | The list of valid api keys | - | false |

If the ApiKeys is not set, the manager api will not require any api key to authenticate the game server.



