using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Backend.WorkerHost.ProxmoxApi
{
    public class ProxmoxApi
    {
        private string _baseurl;
        private string _ticket;
        private string _csrfpt;
        private string _server;
        private uint _port;

        public bool Login(string server, uint port, string username, string password, string realm)
        {
            _server = server;
            _port = port;
            _baseurl = "https://" + server + ":" + port.ToString() + "/api2/json/";
            using (var httpClientHandler = new HttpClientHandler())
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                using (var client = new HttpClient(httpClientHandler))
                {
                    var result = client.PostAsync(_baseurl + $"access/ticket?username={username}&password={password}&realm={realm}", new StringContent("")).Result;
                    if (result.IsSuccessStatusCode)
                    {
                        string strResult = result.Content.ReadAsStringAsync().Result;
                        var obj = JObject.Parse(strResult)["data"];
                        _ticket = obj["ticket"].ToString();
                        _csrfpt = obj["CSRFPreventionToken"].ToString();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        public class VmDto
        {
            public string VmId { get; set; }
            public string Status { get; set; }
            public string Cpus { get; set; }
        }

        public List<VmDto> ListVm(string node)
        {
            var list = new List<VmDto>();
            foreach (var x in ExecuteGet($"nodes/{node}/qemu"))
            {
                list.Add(new VmDto { VmId = x["vmid"].ToString(), Status = x["status"].ToString(), Cpus = x["cpus"].ToString() });
            }
            return list;
        }
        public bool StopVm(string vmId, string node)
        {
            return ExecutePost($"nodes/{node}/qemu/{vmId}/status/stop");
        }

        public bool StartVm(string vmId, string node)
        {
            return ExecutePost($"nodes/{node}/qemu/{vmId}/status/start");
        }

        public bool CloneVm(string vmId, string node, string newId, string name, string description)
        {
            return ExecutePost($"nodes/{node}/qemu/{vmId}/clone", $"newid={newId}&full=1&name={name}&description={description}");
        }

        private bool ExecutePost(string urlExtension, string contentStr = "")
        {
            var cookieContainer = new CookieContainer();
            using (var httpClientHandler = new HttpClientHandler() { CookieContainer = cookieContainer })
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                using (var client = new HttpClient(httpClientHandler))
                {
                    HttpContent content = null;
                    if (contentStr == "")
                    {
                        content = new StringContent("");
                    }
                    else
                    {
                        content = new StringContent(contentStr, Encoding.UTF8, "application/x-www-form-urlencoded");
                    }
                    content.Headers.Add("CSRFPreventionToken", _csrfpt);
                    cookieContainer.Add(new Uri("https://" + _server + ":" + _port.ToString()), new Cookie("PVEAuthCookie", _ticket));

                    var result = client.PostAsync(_baseurl + urlExtension, content).Result;
                    if (result.IsSuccessStatusCode)
                    {
                        string strResult = result.Content.ReadAsStringAsync().Result;
                        var obj = JObject.Parse(strResult)["data"];
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Proxmox request failed: " + result.ReasonPhrase);
                        return false;
                    }
                }
            }
        }

        private JToken ExecuteGet(string urlExtension)
        {
            var cookieContainer = new CookieContainer();
            using (var httpClientHandler = new HttpClientHandler() { CookieContainer = cookieContainer })
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                using (var client = new HttpClient(httpClientHandler))
                {
                    cookieContainer.Add(new Uri("https://" + _server + ":" + _port.ToString()), new Cookie("PVEAuthCookie", _ticket));
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("CSRFPreventionToken", _csrfpt);
                    var result = client.GetAsync(_baseurl + urlExtension).Result;
                    string strResult = result.Content.ReadAsStringAsync().Result;
                    var obj = JObject.Parse(strResult)["data"];
                    return obj;
                }
            }
        }
    }
}
