using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PrivateLinkDataTest.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private IConfiguration _configuration;

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public Result ReadFromRW;
        public Result ReadFromRO;
        public Result ReadFromRI;
        public Result WriteToRW;
        public Result ReadCosmos;
        public Result WriteCosmos;

        public async Task OnGetAsync()
        {
            ReadFromRW = await TestSQLRead("SQLRW");
            ReadFromRO = await TestSQLRead("SQLRO");
            ReadFromRI = await TestSQLRead("SQLRI");
            WriteToRW = await TestSQLWrite();
            ReadCosmos = await TestCosmosRead();
            WriteCosmos = await TestCosmosWrite();
        }

        private async Task<Result> TestSQLRead(string connectionStringName)
        {
            try
            {
                string accessToken = await (new AzureServiceTokenProvider()).GetAccessTokenAsync("https://database.windows.net/");

                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString(connectionStringName)))
                {
                    conn.AccessToken = accessToken;
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("SELECT TOP 1  @@SERVERNAME, Id FROM Items", conn))
                    {
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            reader.Read();
                            return new Result()
                            {
                                serverName = reader.GetString(0),
                                success = true
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new Result()
                {
                    serverName = ex.Message + ": " + ex.StackTrace,
                    success = false
                };
            }
        }

        private async Task<Result> TestSQLWrite()
        {
            try
            {
                string accessToken = await (new AzureServiceTokenProvider()).GetAccessTokenAsync("https://database.windows.net/");

                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("SQLRW")))
                {
                    conn.AccessToken = accessToken;
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("INSERT INTO NewItems VALUES (Newid());SELECT @@SERVERNAME", conn))
                    {
                        string serverName = (await cmd.ExecuteScalarAsync()) as string;
                        return new Result()
                        {
                            serverName = serverName,
                            success = true
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new Result()
                {
                    serverName = ex.Message + ": " + ex.StackTrace,
                    success = false
                };
            }
        }

        private async Task<Result> TestCosmosRead()
        {
            try
            {
                var cosmosClient = new CosmosClientBuilder(_configuration.GetConnectionString("Cosmos")).
                                       WithApplicationRegion(Regions.SouthCentralUS).
                                       WithThrottlingRetryOptions(TimeSpan.FromSeconds(5), 5).Build();

                var db = cosmosClient.GetDatabase("test");
                var container = db.GetContainer("items");

                ItemResponse<Doc> readItemResponse = await container.ReadItemAsync<Doc>("5ae9ec8c-897b-4cd3-9529-14f47ff27ff3", new PartitionKey("5ae9ec8c-897b-4cd3-9529-14f47ff27ff3"));

                return new Result()
                {
                    success = readItemResponse.StatusCode == System.Net.HttpStatusCode.OK,
                    serverName = "N/A"
                };
            }
            catch (Exception ex)
            {
                return new Result()
                {
                    serverName = ex.Message + ": " + ex.StackTrace,
                    success = false
                };
            }
        }

        private async Task<Result> TestCosmosWrite()
        {
            try
            {
                var cosmosClient = new CosmosClientBuilder(_configuration.GetConnectionString("Cosmos")).
                                       WithApplicationRegion(Regions.SouthCentralUS).
                                       WithThrottlingRetryOptions(TimeSpan.FromSeconds(5), 5).Build();

                var db = cosmosClient.GetDatabase("test");
                var container = db.GetContainer("items");

                Doc newDoc = new Doc() { id = Guid.NewGuid().ToString("d") };

                ItemResponse<Doc> readItemResponse = await container.CreateItemAsync<Doc>(newDoc, new PartitionKey(newDoc.id));

                return new Result()
                {
                    success = readItemResponse.StatusCode == System.Net.HttpStatusCode.Created,
                    serverName = "N/A"
                };
            }
            catch (Exception ex)
            {
                return new Result()
                {
                    serverName = ex.Message + ": " + ex.StackTrace,
                    success = false
                };
            }
        }
    }


    public class Result
    {
        public bool success { get; set; }
        public string errorMessage { get; set; }
        public string serverName { get; set; }
    }

    public class Doc
    {
        public string id { get; set; }
    }
}
