using Microsoft.Extensions.Configuration;

namespace Fritz.TwitchAutohost.Data
{
	public class CurrentHostConfigurationRepository : BaseTableRepository<CurrentHostConfiguration>
	{

		public CurrentHostConfigurationRepository(ServiceConfiguration configuration) : base(configuration)
		{

		}

		protected override string TableName { get; } = "CurrentHostConfig";

		public CurrentHostConfiguration GetForMyChannel() {
			return GetByRowKey(base._Configuration.MyChannelId);
		}

	}


}
