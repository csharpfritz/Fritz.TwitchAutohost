using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fritz.TwitchAutohost.Data
{
  public class CurrentSubscriptionsRepository : BaseTableRepository<CurrentSubscription>
  {


		public CurrentSubscriptionsRepository(ServiceConfiguration configuration) : base(configuration)
    {

    }

		protected override string TableName => "CurrentWebhookSubscriptions";


    public async Task<IEnumerable<CurrentSubscription>> GetExpiringSubscriptions()
    {

      var partitionKey = DateTime.UtcNow.ToString("yyyyMMdd");
      return await base.GetAllForPartition(partitionKey);

    }

    public CurrentSubscription GetSubscriptionForChannel(string channelName) {

			var table = GetCloudTable(TableName);

			var query = new TableQuery<CurrentSubscription>
			{
				FilterString = TableQuery.GenerateFilterCondition("ChannelName", QueryComparisons.Equal, channelName)
			};

			var results = table.ExecuteQuery<CurrentSubscription>(query);
			return results.FirstOrDefault();

		}


	}
}
