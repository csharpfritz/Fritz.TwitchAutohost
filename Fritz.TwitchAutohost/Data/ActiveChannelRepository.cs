using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using System;
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
			await base.Remove(obj);

		}

	}

}
