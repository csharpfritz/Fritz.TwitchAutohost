using Microsoft.Extensions.Configuration;

namespace Fritz.TwitchAutohost.Data
{
	public class CurrentHostConfigurationRepository : BaseTableRepository<CurrentHostConfiguration>
	{

		public CurrentHostConfigurationRepository(IConfiguration configuration) : base(configuration)
		{

		}

		protected override string TableName { get; } = "CurrentHostConfig";

	}


}
