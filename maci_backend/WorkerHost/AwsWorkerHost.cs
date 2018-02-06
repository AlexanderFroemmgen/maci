using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;

namespace Backend.WorkerHost
{
    public class AwsWorkerHost : IWorkerHost
    {
        private readonly string _accessKey;
        private readonly string _backend;
        private readonly AmazonEC2Client _client;
        private readonly string _instanceType;
        private readonly string _launchScript;
        private readonly string _keyName;
        private readonly string _securityGroup;

        public AwsWorkerHost(string accessKey, string secretKey, string backend, string instanceType = "t2.micro", string launchScript = "", string keyName = "", string securityGroup = "")
        {
            _accessKey = accessKey;
            _backend = backend;
            _instanceType = instanceType;
            _launchScript = launchScript;
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            _client = new AmazonEC2Client(credentials, RegionEndpoint.EUCentral1);
            _keyName = keyName;
            _securityGroup = securityGroup;
        }

        public string Id => $"aws-{_accessKey.Substring(0, 8)}";

        public async Task<IEnumerable<ImageDto>> GetAvailableImagesAsync()
        {
            var request = new DescribeImagesRequest
            {
                // Ignore all images that don't have the tag "maci".
                Filters = {new Filter {Name = "tag-key", Values = {"maci"}}},
            };

            var imageResult = await _client.DescribeImagesAsync(request);

            var images = imageResult.Images.Select(i => new ImageDto
            {
                HostId = Id,
                Id = i.ImageId,
                Name = i.Name,
                Description = i.Description,
                Capabilities = i.Tags.Single(t => t.Key == "maci").Value.Split(',').Select(c => c.Trim())
            });
            
            return images;
        }

        public async Task<IEnumerable<InstanceDto>> GetRunningInstancesAsync()
        {
            var request = new DescribeInstancesRequest
            {
                // Ignore all instances that don't have the tag "maci".
                Filters = {new Filter {Name = "tag-key", Values = {"maci"}}}
            };

            var instanceResult = await _client.DescribeInstancesAsync(request);

            var instances = instanceResult.Reservations.SelectMany(r => r.Instances);
            var instanceDtos = instances.Select(i => new InstanceDto
            {
                HostId = Id,
                Id = i.InstanceId,
                ImageId = i.ImageId,
                Status = i.State.Name
            });

            return instanceDtos;
        }

        public async Task LaunchInstanceAsync(string imageId, int numberOfInstances = 1, int maxIdleTimeSec = 3600)
        {
            // Retrieve capabilities for the selected image.
            var tagResult = await _client.DescribeTagsAsync(new DescribeTagsRequest
            {
                Filters =
                {
                    new Filter {Name = "resource-id", Values = {imageId}},
                    new Filter {Name = "tag-key", Values = {"maci"}}
                }
            });
            
            var capabilities = tagResult.Tags.Single().Value.Split(',').Select(c => c.Trim());
            var maxIdleTime = maxIdleTimeSec.ToString();

            // Prepare launch script.
            var launchScript = _launchScript
                .Replace("{{Backend}}", _backend)
                .Replace("{{Capabilities}}", string.Join(" ", capabilities))
                .Replace("{{MaxIdleTime}}", maxIdleTime);

            var encodedScript = Convert.ToBase64String(Encoding.UTF8.GetBytes(launchScript));

            // Fetch the metadata of the image and prepare the block device mappings to delete the
            // volumes on termination.
            var describeImages = _client.DescribeImagesAsync(new DescribeImagesRequest { ImageIds = { imageId } });
            var mappings = describeImages.Result.Images[0].BlockDeviceMappings;

            foreach (var mapping in mappings)
            {
                mapping.Ebs.DeleteOnTermination = true;
            }

            // Launch the instance.
            var instanceRequest = new RunInstancesRequest {
                ImageId = imageId,
                InstanceInitiatedShutdownBehavior = ShutdownBehavior.Terminate,
                InstanceType = new InstanceType(_instanceType),
                MinCount = 1,
                MaxCount = numberOfInstances,
                UserData = encodedScript,
                BlockDeviceMappings = mappings
            };

            /* Provide SSH key? */
            if (!string.IsNullOrEmpty(_keyName)) {
                instanceRequest.KeyName = _keyName;
            }

            /* Use security group? */
            if(!string.IsNullOrEmpty(_securityGroup))
            {
                instanceRequest.SecurityGroupIds.Add(_securityGroup);
            }

            var launchResult = await _client.RunInstancesAsync(instanceRequest);
            var instanceIds = launchResult.Reservation.Instances.Select(i => i.InstanceId).ToList();

            var tags = new List<Tag> { new Tag("maci"), new Tag("maxIdleTime", maxIdleTime)};
            await _client.CreateTagsAsync(new CreateTagsRequest(instanceIds, tags));
        }

        public async Task TerminateInstanceAsync(string instanceId)
        {
            await
                _client.TerminateInstancesAsync(new TerminateInstancesRequest
                {
                    InstanceIds = new List<string> {instanceId}
                });
        }
    }
}
