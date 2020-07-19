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

      var partitionKey = DateTime.UtcNow.ToString("yyyyMMdd");
      return await base.GetAllForPartition(partitionKey);

    }


  }
}
