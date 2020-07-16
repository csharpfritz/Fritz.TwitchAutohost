using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost.Data
{
	public class ActiveChannel : TableEntity
	{

		public ActiveChannel()
		{
			this.PartitionKey = "A";
		}

		public string ChannelId { 
			get { return base.RowKey; }
			set { base.RowKey = value; }
		}

		public string UserName { get; set; }

		public bool Mature { get; set; }

		public string Category { get; set; }

		public string[] Tags { get; set; }

	}
}
