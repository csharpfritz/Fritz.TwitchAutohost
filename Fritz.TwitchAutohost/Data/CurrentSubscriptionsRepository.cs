using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Fritz.TwitchAutohost.Data
{
  public class CurrentSubscriptionsRepository : BaseTableRepository<CurrentSubscription>
  {


		public CurrentSubscriptionsRepository(IConfiguration configuration) : base(configuration)
    {

    }

		protected override string TableName => "CurrentWebhookSubscriptions";


    public async Task<IEnumerable<CurrentSubscription>> GetExpiringSubscriptions()
    {

      var table = GetCloudTable(TableName);

      var partitionKey = DateTime.UtcNow.ToString("yyyyMMdd");
      var query = new TableQuery<CurrentSubscription>
      {
        FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey)
      };

      TableContinuationToken token = null;
      var outList = new List<CurrentSubscription>();
      while (true)
      {
        var results = await table.ExecuteQuerySegmentedAsync<CurrentSubscription>(query.Take(10), token);
        if (results.Results.Count == 0) break;

        outList.AddRange(results.Results);

        if (results.ContinuationToken != null)
        {
          token = results.ContinuationToken;
        }
        else
        {
          break;
        }

      }

      return outList;

    }


  }
}
