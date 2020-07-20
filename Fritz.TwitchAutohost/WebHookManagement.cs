using Fritz.TwitchAutohost.Data;
using Fritz.TwitchAutohost.Messages;
using Fritz.TwitchAutohost.Messages.Kraken;
using Microsoft.AspNetCore.Http;
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
		private readonly string _MyChannelId;

		public WebHookManagement(IConfiguration configuration, IHttpClientFactory httpClientFactory, CurrentSubscriptionsRepository repository) : base(configuration, httpClientFactory)
		{
			_Repository = repository;
			_MyChannelId = configuration["ChannelId"];

		}

		[FunctionName("Subscribe")]
		public async Task Subscribe([QueueTrigger("twitch-webhook-subscription", Connection = STORAGE_CONNECTIONSTRING_NAME)] string msg,
																				ILogger logger)
		{

			var twitchEndPoint = "https://api.twitch.tv/helix/webhooks/hub";
			var leaseInSeconds = 864000; // = 10 days

			var channelId = long.TryParse(msg, out var _) ? msg : await GetChannelIdForUserName(msg);
			var callbackUrl = new Uri(Configuration["EndpointBaseUrl"]);

			var payload = new TwitchWebhookSubscriptionPayload
			{
				callback = new Uri(callbackUrl, $"?channelId={channelId}&username={msg}").ToString(),
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
						ChannelName = msg,
						ExpirationDateTimeUtc = DateTime.UtcNow.AddSeconds(leaseInSeconds).AddDays(-1)
					};
					await _Repository.AddOrUpdate(newSub);

				}

				// Error: Log information about the error and gracefully fail
				var responseBody = await responseMessage.Content.ReadAsStringAsync();
				logger.Log(LogLevel.Error, $"Error response body: {responseBody}");

			}

		}

		[FunctionName("ReceiveStreamUpdate")]
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

			await HandlePayload(req, log, channelId);

			return new HttpResponseMessage(HttpStatusCode.OK);

		}

		[FunctionName("ScheduledResubscribe")]
		public async Task ScheduledResubscribe([TimerTrigger("0 2 */2 * * *", RunOnStartup = true)] TimerInfo timer,
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

		[FunctionName("Test")]
		public async Task<HttpResponseMessage> Test([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req) {

			var msg = JsonConvert.DeserializeObject<TwitchStreamChangeMessage>(@"{""data"":[{""game_id"":"""",""id"":""2250511473"",""language"":""en"",""started_at"":""2020-07-19T20:28:41Z"",""tag_ids"":[""6ea6bca4-4712-4ab9-a906-e3336a9d8039""],""thumbnail_url"":""https://static-cdn.jtvnw.net/previews-ttv/live_user_thefritzbot-{width}x{height}.jpg"",""title"":""This is a test - part 5"",""type"":""live"",""user_id"":""210391141"",""user_name"":""thefritzbot"",""viewer_count"":1}]}");
			var result = await ConvertFromPayload(msg);


			return new HttpResponseMessage(HttpStatusCode.OK) {
				Content = new StringContent("Now hosting...")
			};

		}

		private async Task HandlePayload(HttpRequest req, ILogger log, string channelId)
		{

			req.Body.Position = 0;
			TwitchStreamChangeMessage payload = null;
			using (var reader = new StreamReader(req.Body))
			{
				var msg = await reader.ReadToEndAsync();
				log.LogInformation($"Payload received on stream change: {msg}");
				payload = JsonConvert.DeserializeObject<TwitchStreamChangeMessage>(msg);
			}
			var repo = new ActiveChannelRepository(Configuration);

			if (payload.data.Length == 0) // end of stream
			{
				await repo.RemoveByChannelId(channelId);
				await HostTheNextStream(channelId);
			} else {
				await repo.AddOrUpdate(await ConvertFromPayload(payload));
			}

		}

		private async Task HostTheNextStream(string channelId)
		{

			var repo = new CurrentHostConfigurationRepository(Configuration);
			var channelWeAreCurrentlyHosting = (await repo.GetAllForPartition(_MyChannelId)).FirstOrDefault();

			if (channelId == _MyChannelId || (channelWeAreCurrentlyHosting != null && channelId == channelWeAreCurrentlyHosting.HostedChannelId))
			{
				var channels = await new ActiveChannelRepository(Configuration).GetAllActiveChannels();
				var nextChannel = new AutohostFilter(Configuration).DecideOnChannelToHost(channels);
				if (nextChannel == null) return;
				
				// HOST THE CHANNEL --- SEND COMMAND TO TWITCH TO DO THE THING
				var twitch = new TwitchClient();
				twitch.Initialize(new ConnectionCredentials(TwitchAuthTokens.Instance.UserName, TwitchAuthTokens.Instance.AccessToken));
				twitch.Connect();
				twitch.JoinChannel(TwitchAuthTokens.Instance.UserName);
				twitch.Host(TwitchAuthTokens.Instance.UserName, nextChannel.UserName);
				if (channelWeAreCurrentlyHosting != null) await repo.Remove(channelWeAreCurrentlyHosting);
				await repo.AddOrUpdate(new CurrentHostConfiguration() {
					HostedChannelId = nextChannel.ChannelId,
					HostedChannelName = nextChannel.UserName,
					ManagedChannelId = _MyChannelId
				});
			} 

		}

		private async Task<ActiveChannel> ConvertFromPayload(TwitchStreamChangeMessage payload)
		{

			var twitchStream = payload.data[0];
			var ac = new ActiveChannel
			{
				Category = await ConvertFromTwitchCategoryId(twitchStream.game_id), // TODO: Lookup and convert from Twitch's lookup table
				ChannelId = twitchStream.user_id,
				Tags = await ConvertFromTwitchTagIds(twitchStream.tag_ids),
				UserName = twitchStream.user_name
			};
			ac.Mature = await GetMatureFlag(twitchStream.user_id);

			return ac;
		}

		private async Task<string> ConvertFromTwitchCategoryId(string categoryId)
		{

			var client = GetHttpClient("https://api.twitch.tv/helix/games", authHeader: true);
			var result = await client.GetStringAsync($"?id={categoryId}");

			var categoryPayload = JsonConvert.DeserializeObject<TwitchSearchCategoryPayload>(result);
			return categoryPayload.data.Any() ? categoryPayload.data[0].name : "";

		}

		private async Task<bool> GetMatureFlag(string user_id)
		{
			// FETCH mature flag from https://api.twitch.tv/kraken/streams/<channel ID>
			var client = GetHttpClient("https://api.twitch.tv/kraken/streams/", authHeader: true);
			var result = await client.GetStringAsync(user_id);

			return JsonConvert.DeserializeObject<TwitchGetStream>(result).stream.channel.mature;
		}

		private async Task<string[]> ConvertFromTwitchTagIds(string[] tag_ids)
		{

			// TODO: Convert with a query using this syntax:  https://dev.twitch.tv/docs/api/reference#get-all-stream-tags
			var client = GetHttpClient("https://api.twitch.tv/helix/tags/streams", authHeader: true);
			var result = await client.GetStringAsync($"?tag_id={string.Join('&', tag_ids)}");

			var tagPayload = JsonConvert.DeserializeObject<TwitchGetTagsPayload>(result);

			return tagPayload.data.Select(d => d.localization_names.EN_US).ToArray();

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
