using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;

namespace Fritz.TwitchAutohost.Data
{
	public class HostLog : TableEntity, ISetKeys
	{

		public string FromChannelName { get; set; } = "<nohost>";

		public string ToChannelName { get; set; }

		public string Title { get; set; }

		public string Category { get; set; }

		public string Tags { get; set; }

		public void SetKeys() {

			var utcNow = DateTime.UtcNow;
			this.PartitionKey = $"{FromChannelName}_{utcNow.ToString("yyyyMM")}";
			this.RowKey = $"{FromChannelName}_{utcNow.ToString("yyyyMMdd-HHmm")}";

		}


	}

	public class HostLogRepository : BaseTableRepository<HostLog>
	{
		public HostLogRepository(ServiceConfiguration configuration) : base(configuration) { }

	}

}
