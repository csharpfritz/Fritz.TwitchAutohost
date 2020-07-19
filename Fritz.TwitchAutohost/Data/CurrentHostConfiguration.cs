using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost.Data
{
	public class CurrentHostConfiguration : TableEntity
	{

		public string ManagedChannelId {
			get { return base.PartitionKey; }
			set { base.PartitionKey = value; }
		}

		public string HostedChannelId
		{
			get { return base.RowKey; }
			set { base.RowKey = value; }
		}

		public string HostedChannelName { get; set; }

	}


}
