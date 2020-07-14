using Fritz.TwitchAutohost.Data;
using Fritz.TwitchAutohost.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Fritz.TwitchAutohost
{
	public class WebHookManagement : BaseFunction
	{

		private const string STORAGE_CONNECTIONSTRING_NAME = "TwitchAutohostStorage";
		private readonly CurrentSubscriptionsRepository _Repository;

		public WebHookManagement(IConfiguration configuration, IHttpClientFactory httpClientFactory, CurrentSubscriptionsRepository repository) : base(configuration, httpClientFactory)
		{
			_Repository = repository;
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
					await _Repository.AddSubscription(newSub);

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

		private async Task HandlePayload(HttpRequest req, ILogger log, string channelId)
		{

			var msg = await (new StreamReader(req.Body).ReadToEndAsync());
			var payload = JsonConvert.DeserializeObject<TwitchStreamChangeMessage>(msg);

			if (payload.data.Length == 0) // end of stream
			{
			 // TODO: delete active record for this channel
			} else {
				// TODO: Add / update active record for this channel
			}

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
