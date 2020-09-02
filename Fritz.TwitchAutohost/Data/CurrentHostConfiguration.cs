using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost.Data
{
	public class CurrentHostConfiguration : TableEntity
	{

		public CurrentHostConfiguration()
		{
			PartitionKey = "A";
		}

		public string ManagedChannelId {
			get { return base.RowKey; }
			set { base.RowKey = value; }
		}

		public string HostedChannelId { get; set; }

		public string HostedChannelName { get; set; }

	}


}
