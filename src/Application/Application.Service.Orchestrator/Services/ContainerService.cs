using Application.Service.Orchestrator.Entities.Configurations;
using Docker.DotNet;
using Docker.DotNet.Models;
using Library.Commons.Game.Server.Entities;
using Library.Commons.Game.Server.Enums;
using Microsoft.Extensions.Options;

namespace Application.Service.Orchestrator.Services;

public class ContainerService
{
    private readonly IDockerClient _dockerClient;
    private readonly IOptions<AppSettings> _appSettings;

    public ContainerService(IDockerClient dockerClient, IOptions<AppSettings> appSettings)
    {
        _dockerClient = dockerClient;
        _appSettings = appSettings;
    }

    public async Task<IEnumerable<GameServerInformation>> GetRunningServersAsync(CancellationToken cancellationToken=default)
    {
        var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true
        }, cancellationToken);
        var availableGameServerContainers =  containers.Where(container => IsContainerRunning(container.State) && IsContainerGameServer(container.Image));
       return availableGameServerContainers.Select(containerServer=>
           new GameServerInformation(
               containerServer.Names.First(),
               GetContainerUri(containerServer)
               )).ToList();
    }
    private Uri GetContainerUri(ContainerListResponse container)
    {
        var port = container.Ports.FirstOrDefault(port=>port.PrivatePort == _appSettings.Value.DockerDeamonConfiguration.InternalPort)?.PublicPort;
        var domain = _appSettings.Value.GameServerConfiguration.Domain;
        var scheme = _appSettings.Value.GameServerConfiguration.Scheme;
        return new Uri($"{scheme}://{domain}:{port}");
    }
    private bool IsContainerGameServer(string containerImage)
    {
        return containerImage.Contains(_appSettings.Value.GameServerConfiguration.Image, StringComparison.InvariantCultureIgnoreCase);
    }

    public bool IsContainerRunning(string state)
    {
        return state == ContainerStateEnum.Running;
    }
}