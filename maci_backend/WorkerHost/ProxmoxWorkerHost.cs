using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.WorkerHost
{
    public class ProxmoxWorkerHost : IWorkerHost
    {
        private readonly string _username;
        private readonly string _password;
        private readonly string _server;
        private readonly string _port;
        private readonly string _node;
        private readonly string _realm;

        public ProxmoxWorkerHost(string username, string password, string server, string port, string node, string realm = "pve")
        {
            _username = username;
            _password = password;
            _server = server;
            _port = port;
            _node = node;
            _realm = realm;
        }

        public string Id => $"proxmox-{_username}@{_server}-{_node}";

        public async Task<IEnumerable<ImageDto>> GetAvailableImagesAsync()
        {
            ProxmoxApi.ProxmoxApi api = new ProxmoxApi.ProxmoxApi();
            api.Login(_server, UInt32.Parse(_port), _username, _password, _realm);

            var images = api.ListVm(_node).Select(i => new ImageDto {
                HostId = Id,
                Id = i.VmId,
                Name = "name",
                Description = "CPUs: " + i.Cpus + " Status: " + i.Status,
                Capabilities = new List<string>()
            });

            return images;
        }

        public async Task<IEnumerable<InstanceDto>> GetRunningInstancesAsync()
        {
            ProxmoxApi.ProxmoxApi api = new ProxmoxApi.ProxmoxApi();
            api.Login(_server, UInt32.Parse(_port), _username, _password, _realm);

            var instanceDtos = api.ListVm(_node).Where(i => i.Status == "running").Select(i => new InstanceDto
            {
                HostId = Id,
                Id = i.VmId,
                ImageId = "name",
                Status =  i.Status
            });

            return instanceDtos;
        }

        public async Task LaunchInstanceAsync(string imageId, int numberOfInstances = 1, int maxIdleTimeSec = 3600)
        {
            for (int i = 0; i < numberOfInstances; i++)
            {
                string newVmid = new Random().Next(1000, 9999).ToString();
                Console.WriteLine("Launch instance " + newVmid + " as copy of " + imageId);

                ProxmoxApi.ProxmoxApi api = new ProxmoxApi.ProxmoxApi();
                api.Login(_server, UInt32.Parse(_port), _username, _password, _realm);
                api.CloneVm(imageId, _node, newVmid, "name", "desc");
                // Some problems as cloning requires time... we just hope that it is finished after n seconds
                new Thread(() => { Console.WriteLine("#######"); Thread.Sleep(20000); Console.WriteLine("??????????"); api.StartVm(newVmid, _node); }).Start();
            }
        }

        public async Task TerminateInstanceAsync(string instanceId)
        {
            Console.WriteLine("Terminating instance: " + instanceId);

            ProxmoxApi.ProxmoxApi api = new ProxmoxApi.ProxmoxApi();
            api.Login(_server, UInt32.Parse(_port), _username, _password, _realm);
            api.StopVm(instanceId, _node);
        }
    }
}
