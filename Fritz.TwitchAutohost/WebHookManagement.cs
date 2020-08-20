using Fritz.TwitchAutohost.Data;
using Fritz.TwitchAutohost.Messages;
using Fritz.TwitchAutohost.Messages.Kraken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;

namespace Fritz.TwitchAutohost
{
	public class WebHookManagement : BaseFunction
	{

		private const string STORAGE_CONNECTIONSTRING_NAME = "TwitchAutohostStorage";
		private readonly CurrentSubscriptionsRepository _Repository;
		private readonly TwitchStreamManager _StreamManager;

		public WebHookManagement(IConfiguration configuration, IHttpClientFactory httpClientFactory, CurrentSubscriptionsRepository repository, TwitchStreamManager streamManager) : base(configuration, httpClientFactory)
		{
			_Repository = repository;
			_StreamManager = streamManager;
		}

		[FunctionName(nameof(Subscribe))]
		public async Task Subscribe([QueueTrigger("twitch-webhook-subscription", Connection = STORAGE_CONNECTIONSTRING_NAME)] string twitchUserName,
																				ILogger logger)
		{

			var twitchEndPoint = "https://api.twitch.tv/helix/webhooks/hub";
			var leaseInSeconds = 864000; // = 10 days

			var channelId = long.TryParse(twitchUserName, out var _) ? twitchUserName : await GetChannelIdForUserName(twitchUserName);
			var callbackUrl = new Uri(Configuration["EndpointBaseUrl"]);

			var payload = new TwitchWebhookSubscriptionPayload
			{
				callback = new Uri(callbackUrl, $"?channelId={channelId}&username={twitchUserName}").ToString(),
				mode = "subscribe",
				topic = $"https://api.twitch.tv/helix/streams?user_id={channelId}",
				lease_seconds = leaseInSeconds,
				secret = TWITCH_SECRET
			};

			logger.LogDebug($"Posting with callback url: {payload.callback}");
			var stringPayload = JsonConvert.SerializeObject(payload);
			logger.Log(LogLevel.Information, $"Subscribing to Twitch with payload: {stringPayload}");

			using (var client = GetHttpClient(twitchEndPoint, authHeader: true))
			{

				var responseMessage = await client.PostAsync("", new StringContent(stringPayload, Encoding.UTF8, @"application/json"));
				if (responseMessage.IsSuccessStatusCode)
				{

					// Use the Table output binding to save a new record to Azure table storage
					var newSub = new CurrentSubscription
					{
						ChannelId = channelId,
						ChannelName = twitchUserName,
						ExpirationDateTimeUtc = DateTime.UtcNow.AddSeconds(leaseInSeconds).AddDays(-1)
					};
					await _Repository.AddOrUpdate(newSub);
					await _StreamManager.InspectChannelAndLogStatus(twitchUserName);

				}

				// Error: Log information about the error and gracefully fail
				var responseBody = await responseMessage.Content.ReadAsStringAsync();
				logger.Log(LogLevel.Error, $"Error response body: {responseBody}");

			}

		}

		[FunctionName(nameof(ReceiveStreamUpdate))]
		public async Task<HttpResponseMessage> ReceiveStreamUpdate(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
							ILogger log)
						//	,[ServiceBus("EndOfStream", Connection = "ServiceBusConnectionString", EntityType = EntityType.Topic)]
						//out CompletedStream completedStream)
		{

			var channelId = req.Query["channelId"].ToString();
			log.LogMetric("Query", 1, new Dictionary<string, object> { { "TwitchChannelId", channelId } });
			log.LogInformation($"ChannelId: {channelId}");

			// Handle the verification on the WebHook subscription
			var challengeContent = VerifySubscription(req, log);
			if (challengeContent != null) {
				return challengeContent;
			}

			if (!string.IsNullOrEmpty(req.Query["userid"].ToString())) channelId = GetChannelIdForUserName(req.Query["userid"].ToString()).GetAwaiter().GetResult();

			if (!(VerifyPayloadSecret(req, log).GetAwaiter().GetResult()))
			{
				log.LogError($"Invalid signature on request for ChannelId {channelId}");
				return null;
			}
			else
			{
				log.LogTrace($"Valid signature for ChannelId {channelId}");
			}

			await _StreamManager.HandlePayload(req, log, channelId);

			return new HttpResponseMessage(HttpStatusCode.OK);

		}

		[FunctionName(nameof(ScheduledResubscribe))]
		public async Task ScheduledResubscribe([TimerTrigger(CrontabConfigs.EVERY_SECOND_HOUR_AND_SECOND_MINUTE, RunOnStartup = true)] TimerInfo timer,
			[Queue("twitch-webhook-subscription", Connection = STORAGE_CONNECTIONSTRING_NAME)] CloudQueue queue,
			ILogger logger)
		{

			var repo = new CurrentSubscriptionsRepository(Configuration);

			var currentSubscriptions = await repo.GetExpiringSubscriptions();
			foreach (var item in currentSubscriptions)
			{
				await queue.AddMessageAsync(new CloudQueueMessage(item.ChannelName));
				await repo.Remove(item);
			}

		}

		[FunctionName(nameof(TagRefresh))]
		public async Task TagRefresh([TimerTrigger(CrontabConfigs.EVERY_THIRTY_MINUTES, RunOnStartup =true) ] TimerInfo timerInfo,
			ILogger logger)
		{

			var repo = new ActiveChannelRepository(Configuration);
			var currentChannels = await repo.GetAllActiveChannels();
			var taskList = new List<Task<ActiveChannel>>();

			foreach (var channel in currentChannels)
			{
				taskList.Add(_StreamManager.GetInformationForChannel(channel.UserName));
			}

			Task.WaitAll(taskList.ToArray(), (int)TimeSpan.FromMinutes(5).TotalMilliseconds);

			foreach (var channelUpdate in taskList)
			{

				var newInfo = channelUpdate.Result;
				if (newInfo.Category == ActiveChannel.OFFLINE) continue;

				await repo.AddOrUpdate(newInfo);

			}

		}


		[FunctionName("Test")]
		public async Task<HttpResponseMessage> Test([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req) {

			await _StreamManager.HostTheNextStream("123391659");
			return new HttpResponseMessage(HttpStatusCode.OK);

			//var tagIds = new string[] { "6ea6bca4-4712-4ab9-a906-e3336a9d8039",
			//					"6f86127d-6051-4a38-94bb-f7b475dde109",
			//					"cea7bc0c-75a5-4446-8743-6db031b71550",
			//					"c23ce252-cf78-4b98-8c11-8769801aaf3a",
			//					"a59f1e4e-257b-4bd0-90c7-189c3efbf917",
			//					"67259b26-ff83-444e-9d3c-faab390df16f" };

			//var tagNames = await _StreamManager.ConvertFromTwitchTagIds(tagIds);

			//return new HttpResponseMessage(HttpStatusCode.OK) {
			//	Content = new StringContent(string.Join(',', tagNames))
			//};

		}

		private HttpResponseMessage VerifySubscription(HttpRequest req, ILogger log)
		{

			var challenge = req.Query["hub.challenge"].ToString();
			if (string.IsNullOrEmpty(challenge)) return null;

			var channelId = req.Query["channelId"].ToString();
			log.LogInformation($"Successfully subscribed to channel {channelId}");

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(challenge)
			};

		}

	}
}
