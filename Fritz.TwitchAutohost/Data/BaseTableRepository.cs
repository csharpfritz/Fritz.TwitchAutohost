using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Fritz.TwitchAutohost.Data
{

	public abstract class BaseTableRepository<T> where T : TableEntity
	{
		
		protected IConfiguration _Configuration { get; }

		protected BaseTableRepository(IConfiguration configuration)
		{
			_Configuration = configuration;
		}

		protected abstract string TableName { get; }

		protected CloudTable GetCloudTable(string tableName)
		{

			var account = CloudStorageAccount.Parse(_Configuration["TwitchChatStorage"]);
			var client = account.CreateCloudTableClient(new TableClientConfiguration());
			return client.GetTableReference(tableName);


		}

		public Task AddOrUpdate(T obj)
		{

			var table = GetCloudTable(TableName);

			return table.ExecuteAsync(TableOperation.InsertOrReplace(obj));

		}

		public async Task<T> Get(string partitionKey, string rowKey) {

			var table = GetCloudTable(TableName);
			var getOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);

			return (await table.ExecuteAsync(getOperation)).Result as T;
			
		}

		public Task Remove(T sub)
		{

			var table = GetCloudTable(TableName);

			return table.ExecuteAsync(TableOperation.Delete(sub));

		}




	}

}
