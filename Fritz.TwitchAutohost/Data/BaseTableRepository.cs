using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fritz.TwitchAutohost.Data
{

	public abstract class BaseTableRepository<T> where T : TableEntity, new()
	{

		protected ServiceConfiguration _Configuration { get; }

		protected BaseTableRepository(ServiceConfiguration configuration)
		{
			_Configuration = configuration;
		}

		protected virtual string TableName { get { return typeof(T).Name; }  }

		protected CloudTable GetCloudTable(string tableName)
		{

			var account = CloudStorageAccount.Parse(_Configuration.StorageConnectionString);
			var client = account.CreateCloudTableClient(new TableClientConfiguration());
			client.GetTableReference(tableName).CreateIfNotExists();
			return client.GetTableReference(tableName);


		}

		public virtual Task AddOrUpdate(T obj)
		{

			if (obj is ISetKeys keyObj) keyObj.SetKeys();

			var table = GetCloudTable(TableName);

			return table.ExecuteAsync(TableOperation.InsertOrReplace(obj));

		}

		public async Task<T> Get(string partitionKey, string rowKey)
		{

			var table = GetCloudTable(TableName);
			var getOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);

			return (await table.ExecuteAsync(getOperation)).Result as T;

		}

		public T GetByRowKey(string rowKey)
		{

			var table = GetCloudTable(TableName);
			var query = new TableQuery<T>
			{
				FilterString = TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, rowKey)
			};

			return table.ExecuteQuery(query).FirstOrDefault();

		}

			public async Task<IEnumerable<T>> GetAllForPartition(string partitionKey) {

			var table = GetCloudTable(TableName);

			var query = new TableQuery<T>
			{
				FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey)
			};

			TableContinuationToken token = null;
			var outList = new List<T>();
			while (true)
			{
				var results = await table.ExecuteQuerySegmentedAsync<T>(query.Take(10), token);
				if (results.Results.Count == 0) break;

				outList.AddRange(results.Results);

				if (results.ContinuationToken != null)
				{
					token = results.ContinuationToken;
				}
				else
				{
					break;
				}

			}

			return outList;

		}


		public Task Remove(T sub)
		{

			if (sub == null) return Task.CompletedTask;

			var table = GetCloudTable(TableName);

			return table.ExecuteAsync(TableOperation.Delete(sub));

		}


	}

}
