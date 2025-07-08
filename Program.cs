using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection.Metadata;

namespace D365FODataJob
{
    class Program
    {
        // 🔧 Config values
        static string tenantId = "xyz";
        static string clientId = "xyz";
        static string clientSecret = "xyz";
        static string d365foBaseUrl = "https://xyz.operations.sa.dynamics.com/";


        static async Task Main(string[] args)
        {
            string token = await AuthHelper.GetBearerToken(tenantId, clientId, clientSecret, d365foBaseUrl);

            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("❌ Authentication failed");
                return;
            }

            await JobHelper.EnqueueRecurringJob(token);
            await JobHelper.DequeueRecurringJob(token);
        }
    }

    static class AuthHelper
    {
        public static async Task<string> GetBearerToken(string tenantId, string clientId, string clientSecret, string resource)
        {
            using var client = new HttpClient();
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("resource", resource)
            });

            var url = $"https://login.microsoftonline.com/{tenantId}/oauth2/token";
            var response = await client.PostAsync(url, formData);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                dynamic json = JsonConvert.DeserializeObject(content);
                return json.access_token;
            }
            else
            {
                Console.WriteLine("❌ Token request failed:");
                Console.WriteLine(content);
                return null;
            }
        }
    }

    static class JobHelper
    {
        static string baseUrl = "https://xyz.operations.sa.dynamics.com";
        static string legalEntityId = "Customer groups";
        static string outputFile = @"C:\Temp\ExportedData.zip";
        static string inputfile = @"C:\Temp\Customer groups.csv";


        public static async Task EnqueueRecurringJob(string token)
        {
            var client = CreateHttpClient(token);
            string definitionGroupId = "xyz";


            //var payload = new
            //{
            //    entity = legalEntityId
            //};

            string inputfile = @"C:\Temp\Customer groups.csv";

            if (!File.Exists(inputfile))
            {
                Console.WriteLine("CSV file not found.");
                return;
            }

            byte[] fileBytes = await File.ReadAllBytesAsync(inputfile);

            var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            string thirdPartyApiUrl = $"/api/connector/enqueue/{definitionGroupId}?entity=Customer groups";

            HttpResponseMessage response = await client.PostAsync(thirdPartyApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("File sent as raw binary.");
            }
            else
            {
                Console.WriteLine($"Failed: {response.StatusCode}");
            }

        }

        public static async Task DequeueRecurringJob(string token)
        {
            var client = CreateHttpClient(token);
            string definitionGroupId = "xyzcd cus";

            //var payload = new
            //{
            //    definitionGroupId = definitionGroupId
            //};

            //var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await client.GetAsync($"/api/connector/dequeue/{definitionGroupId}");
            var result = await response.Content.ReadAsStringAsync();


            var json = JObject.Parse(result);
            string downloadUrl = json["DownloadLocation"]?.ToString();

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                Console.WriteLine("❌ No 'downloadUrl' found in response.");
                return;
            }

            //Console.WriteLine($"⬇ Download URL: {downloadUrl}");

            // ⬇ Step 3: Download the file with token
            using (var downloadClient = new HttpClient())
            {
                downloadClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var fileResponse = await downloadClient.GetAsync(downloadUrl);
                //Console.WriteLine(fileResponse);
                if (fileResponse.IsSuccessStatusCode)
                {
                    using (var fileStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                    {
                        await fileResponse.Content.CopyToAsync(fileStream);
                    }

                    Console.WriteLine($"✅ File downloaded and saved to {outputFile}");
                }
                else
                {
                    Console.WriteLine($"❌ File download failed: {fileResponse.StatusCode}");
                    string error = await fileResponse.Content.ReadAsStringAsync();
                    Console.WriteLine(error);
                }
            }

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✅ Dequeue triggered successfully");
                //Console.WriteLine(result);
            }
            else
            {
                Console.WriteLine("❌ Dequeue failed:");
                Console.WriteLine(result);
            }
        }

        private static HttpClient CreateHttpClient(string token)
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
