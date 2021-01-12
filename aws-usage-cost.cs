using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Configuration;
// using Microsoft.Azure.Services.AppAuthentication;
// using Microsoft.Azure.KeyVault;
// using Microsoft.Azure.KeyVault.Models;
// using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Amazon.CostExplorer;
using Amazon;
using Amazon.Organizations;
using System.Text;
using System.Collections.Generic;

namespace aws_azure
{
    public static class Aws_usage_cost
    {
        static  string aws_access_key_id = "";
        static  string aws_secret_key_id = "";
        [FunctionName("aws_usage_cost")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            aws_access_key_id = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            aws_secret_key_id = Environment.GetEnvironmentVariable("AWS_SECRET_KEY");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            
            var client = new AmazonCostExplorerClient(aws_access_key_id, aws_secret_key_id, RegionEndpoint.USEast1);

            var costRequest = new Amazon.CostExplorer.Model.GetCostAndUsageRequest();
            var start = new DateTime(2020, 11, 1, 0, 0, 0).AddMonths(-6).Date;
            var end = new DateTime(2020, 12, 1, 0, 0, 0);
            costRequest.TimePeriod = new Amazon.CostExplorer.Model.DateInterval()
            {
                Start = start.ToString("yyyy-MM-dd"),
                End = end.ToString("yyyy-MM-dd")
            };

            costRequest.Granularity = Granularity.MONTHLY;
            costRequest.Metrics.Add("UnblendedCost");
            List<Amazon.CostExplorer.Model.ResultByTime> resultByTimes = new List<Amazon.CostExplorer.Model.ResultByTime>();
            foreach (var account in await GetAccounts())
            {
                var expression = new Amazon.CostExplorer.Model.Expression
                {
                    Dimensions = new Amazon.CostExplorer.Model.DimensionValues
                    {
                        Key = "LINKED_ACCOUNT",
                        Values = new System.Collections.Generic.List<string>() { account.Id }
                    }
                };
                costRequest.Filter = expression;
                var resp = await client.GetCostAndUsageAsync(costRequest);
                 resultByTimes.AddRange(resp.ResultsByTime);

            }
            var jsonObject = JsonConvert.SerializeObject(resultByTimes);

            return new OkObjectResult(jsonObject);
        }

        private static async Task<List<Amazon.Organizations.Model.Account>> GetAccounts()
        {
            var client = new AmazonOrganizationsClient(aws_access_key_id, aws_secret_key_id, RegionEndpoint.USEast1);

           var accountList =  await client.ListAccountsAsync(new Amazon.Organizations.Model.ListAccountsRequest());
           return accountList.Accounts;
        }
    }
}
