using System.Collections.Generic;
using System.Threading.Tasks;

namespace Backend.WorkerHost
{
    public interface IWorkerHost
    {
        string Id { get; }

        Task<IEnumerable<ImageDto>> GetAvailableImagesAsync();

        Task<IEnumerable<InstanceDto>> GetRunningInstancesAsync();

        Task LaunchInstanceAsync(string imageId, int numberOfInstances, int maxIdleTimeSec);

        Task TerminateInstanceAsync(string instanceId);
    }

    public class ImageDto
    {
        public string HostId { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public IEnumerable<string> Capabilities { get; set; }
        public bool AutoScale { get; set; }
    }

    public class InstanceDto
    {
        public string HostId { get; set; }
        public string Id { get; set; }
        public string ImageId { get; set; }
        public string Status { get; set; }
    }
}