using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fritz.TwitchAutohost.Data
{
	public class ActiveChannelRepository : BaseTableRepository<ActiveChannel>
	{

		public ActiveChannelRepository(IConfiguration configuration) : base(configuration)
		{

		}

		protected override string TableName { get { return "ActiveChannels"; } }

		internal async Task RemoveByChannelId(string channelId)
		{

			var obj = await base.Get("A", channelId);
			if (obj == null) return; // Exit because we have no record of this channel
			await base.Remove(obj);

		}

		public async Task<IEnumerable<ActiveChannel>> GetAllActiveChannels() {


			var testList = new List<ActiveChannel> { 
				new ActiveChannel {
					UserName="csharpfritz",
					ChannelId = "96909659",
					Category = "Science & Technology",
					Mature = false,
					Tags = new string[] { "Programming", "AMA", "Web Programming" }
				},
				new ActiveChannel {
					UserName="drlupo",
					ChannelId="29829912",
					Category="Fortnite",
					Mature=false,
					Tags = new string[] { }
				},
				new ActiveChannel {
					UserName = "fiercekittenz",
					ChannelId = "63208102",
					Category = "Science & Technology",
					Mature = true,
					Tags = new string[]  { "Programming", "AMA" }
				}
			};
			return testList;

			//return await GetAllForPartition("A");

		}

	}

}
