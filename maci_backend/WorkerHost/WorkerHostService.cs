using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Util;
using Backend.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Backend.Config;

namespace Backend.WorkerHost
{
    /// <summary>
    /// This service instanciates the respective worker host implementations from the configuration,
    /// allows interaction with the worker hosts in a general way and wraps exceptions.
    /// </summary>
    public class WorkerHostService
    {
        private readonly IList<IWorkerHost> _hosts;
        private readonly ILogger<WorkerHostService> _logger;
        private readonly DirectoryOptions _directoryOptions;

        public WorkerHostService(IConfiguration config, ILogger<WorkerHostService> logger, DirectoryOptions directoryOptions)
        {
            _logger = logger;
            _directoryOptions = directoryOptions;
            _hosts = InstantiateWorkerHostsFromConfig(config.GetSection("WorkerHosts")).ToList();
            _logger.LogInformation($"Instantiated {_hosts.Count} worker hosts.");
        }

        private IEnumerable<IWorkerHost> InstantiateWorkerHostsFromConfig(IConfigurationSection config)
        {
            foreach (var workerHostConfig in config.GetChildren())
            {
                var type = workerHostConfig.GetRequiredSetting("Type");

                if (type == "aws")
                {
                    _logger.LogInformation($"Instantiated AWS worker host. (Key: {workerHostConfig.GetRequiredSetting("AccessKey").Substring(0, 8)}...)");

                    var accessKey = workerHostConfig.GetRequiredSetting("AccessKey");
                    var secretKey = workerHostConfig.GetRequiredSetting("SecretKey");
                    var backend = workerHostConfig.GetRequiredSetting("Backend");
                    var instanceType = workerHostConfig.GetRequiredSetting("InstanceType");
                    var script = _directoryOptions.GetFileContents("AppData/CloudInit/bootstrap.sh");
                    var keyName = workerHostConfig.GetSetting("KeyName", "");
                    var securityGroup = workerHostConfig.GetSetting("SecurityGroup", "");

                    yield return new AwsWorkerHost(accessKey, secretKey, backend, instanceType, script, keyName);
                }
                else if (type == "proxmox")
                {
                    var username = workerHostConfig.GetRequiredSetting("Username");
                    var password = workerHostConfig.GetRequiredSetting("Password");
                    var server = workerHostConfig.GetRequiredSetting("Server");
                    var port = workerHostConfig.GetRequiredSetting("Port");
                    var node = workerHostConfig.GetRequiredSetting("Node");
                    var realm = workerHostConfig.GetRequiredSetting("ReAlm");

                    yield return new ProxmoxWorkerHost(username, password, server, port, node, realm);
                }
                else if (type == "openstack")
                {
                    throw new NotImplementedException();
                }
                else
                {
                    throw new ArgumentException($"Unknown worker host type '{type}'.");
                }
            }
        }

        public async Task<IEnumerable<ImageDto>> GetAvailableImagesAsync()
        {
            try
            {
                return await _hosts.SelectManyAsync(h => h.GetAvailableImagesAsync());
            }
            catch (Exception e)
            {
                throw new WorkerHostException(e);
            }
        }

        public async Task<IEnumerable<InstanceDto>> GetRunningInstancesAsync()
        {
            try
            {
                return await _hosts.SelectManyAsync(h => h.GetRunningInstancesAsync());
            }
            catch (Exception e)
            {
                throw new WorkerHostException(e);
            }
        }

        public async Task LaunchInstanceAsync(string hostId, string imageId, int numberOfInstances, int maxIdleTimeSec)
        {
            var host = _hosts.Single(h => h.Id == hostId);

            try
            {
                await host.LaunchInstanceAsync(imageId, numberOfInstances, maxIdleTimeSec);
            }
            catch (Exception e)
            {
                throw new WorkerHostException(e);
            }
        }

        public async Task TerminateInstanceAsync(string hostId, string instanceId)
        {
            var host = _hosts.Single(h => h.Id == hostId);

            try
            {
                await host.TerminateInstanceAsync(instanceId);
            }
            catch (Exception e)
            {
                throw new WorkerHostException(e);
            }
        }
    }
}
