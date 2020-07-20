using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Fritz.TwitchAutohost.Data
{
	public class ActiveChannel : TableEntity
	{

		public const string OFFLINE = "<offline>";

		public ActiveChannel()
		{
			this.PartitionKey = "A";
		}

		public string ChannelId { 
			get { return base.RowKey; }
			set { base.RowKey = value; }
		}

		public string UserName { get; set; }

		/// <summary>
		/// Is this broadcast intended for Mature audiences only?
		/// </summary>
		public bool Mature { get; set; }

		public string Category { get; set; }

		public string[] Tags { get; set; }

		public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
		{
			var results = base.WriteEntity(operationContext);
			results.Add("tags", EntityProperty.GeneratePropertyForString(string.Join('|', Tags)));
			return results;
		}

		public override void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
		{
			base.ReadEntity(properties, operationContext);
			Tags = properties["tags"].StringValue.Split('|');
		}

	}

	public class OfflineChannel : ActiveChannel {

		public OfflineChannel()
		{
			Category = OFFLINE;
		}

	}

}
