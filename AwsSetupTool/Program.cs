using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;

namespace AwsSetupTool
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("This tool allows you to import images as AMIs and set up your AWS account accordingly.");

            string accessKey;
            string secretKey;

            if (args.Length == 2)
            {
                accessKey = args[0];
                secretKey = args[1];
            }
            else
            {
                Console.WriteLine(
                    "Please enter your AWS access key and secret key. (You can skip this step by launching with the params '<accessKey> <secretKey>'.");

                Console.Write("Access key: ");
                accessKey = Console.ReadLine().Trim();

                Console.Write("Secret key: ");
                secretKey = Console.ReadLine().Trim();
            }

            SelectOptions:

            Console.WriteLine("---");
            Console.WriteLine("You have the following options:");
            Console.WriteLine("<1> Create role 'vmimport'. (This role has to exist, otherwise importing will fail.)");
            Console.WriteLine("<2> Import image from S3 to AMI.");
            Console.WriteLine("<3> Check status of imports.");

            Console.Write("> ");
            var selection = Console.ReadLine().Trim();

            try
            {
                if (selection == "1")
                {
                    SetupVmimportRole(accessKey, secretKey).GetAwaiter().GetResult();
                }
                else if (selection == "2")
                {
                    Console.WriteLine(
                        "Your image needs to be in OVA file format (for example by exporting with VirtualBox) and uploaded to Amazon S3.");
                    Console.WriteLine("After importing has finished, you can delete the image from S3.");
                    Console.Write("S3 Bucket name> ");
                    var bucketName = Console.ReadLine().Trim();
                    Console.Write("S3 File name> ");
                    var fileName = Console.ReadLine().Trim();

                    ImportFromS3(accessKey, secretKey, bucketName, fileName).GetAwaiter().GetResult();
                }
                else if (selection == "3")
                {
                    CheckImportStatus(accessKey, secretKey).GetAwaiter().GetResult();
                }
                else
                {
                    Console.WriteLine("Invalid input.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.GetType()}: {e.Message}");
            }

            goto SelectOptions;
        }

        private static string ReadEmbeddedFile(string name)
        {
            var assembly = Assembly.GetEntryAssembly();
            var resourceStream = assembly.GetManifestResourceStream(name);

            using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static async Task SetupVmimportRole(string accessKey, string secretKey)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);

            var iamClient = new AmazonIdentityManagementServiceClient(credentials, RegionEndpoint.EUCentral1);

            Console.WriteLine("Creating role...");

            var createRole = await iamClient.CreateRoleAsync(new CreateRoleRequest
            {
                RoleName = "vmimport",
                AssumeRolePolicyDocument = ReadEmbeddedFile("AwsSetupTool.trust-policy.json")
            });

            Console.WriteLine($"HTTP {createRole.HttpStatusCode}: {createRole}");

            Console.WriteLine("Creating role policy...");

            var putRolePolicy =
                await
                    iamClient.PutRolePolicyAsync(new PutRolePolicyRequest
                    {
                        RoleName = "vmimport",
                        PolicyName = "vmimport",
                        PolicyDocument = ReadEmbeddedFile("AwsSetupTool.role-policy.json")
                    });

            Console.WriteLine($"HTTP {putRolePolicy.HttpStatusCode}: {putRolePolicy}");
        }

        private static async Task ImportFromS3(string accessKey, string secretKey, string bucketName, string fileName)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);

            var client = new AmazonEC2Client(credentials, RegionEndpoint.EUCentral1);

            Console.WriteLine("Creating import task...");

            var importImage = await client.ImportImageAsync(new ImportImageRequest
            {
                Description = $"{bucketName}/{fileName}",
                DiskContainers = new List<ImageDiskContainer>
                {
                    new ImageDiskContainer
                    {
                        Format = "OVA",
                        UserBucket = new UserBucket
                        {
                            S3Bucket = bucketName,
                            S3Key = fileName
                        },
                        Description = $"{bucketName}/{fileName}"
                    }
                }
            });

            Console.WriteLine($"HTTP {importImage.HttpStatusCode}: {importImage}");

            // TODO add tag to the created AMI
        }

        public static async Task CheckImportStatus(string accessKey, string secretKey)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);

            var client = new AmazonEC2Client(credentials, RegionEndpoint.EUCentral1);

            var describeImportImageTasks =
                await client.DescribeImportImageTasksAsync(new DescribeImportImageTasksRequest());

            Console.WriteLine($"HTTP {describeImportImageTasks.HttpStatusCode}: {describeImportImageTasks}");

            if (!describeImportImageTasks.ImportImageTasks.Any())
            {
                Console.WriteLine("There are no import tasks.");
            }

            foreach (var task in describeImportImageTasks.ImportImageTasks)
            {
                Console.WriteLine($"<{task.Description}> {task.Progress}% {task.Status} {task.StatusMessage}");
            }
        }
    }
}