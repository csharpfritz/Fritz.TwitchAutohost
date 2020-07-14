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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Fritz.TwitchAutohost
{
	public class WebHookManagement : BaseFunction
	{

		private const string STORAGE_CONNECTIONSTRING_NAME = "TwitchAutohostStorage";

		public WebHookManagement(IConfiguration configuration, IHttpClientFactory httpClientFactory) : base(configuration, httpClientFactory)
		{
		}

		[FunctionName("Subscribe")]
		[return: Table("CurrentWebhookSubscriptions", Connection = STORAGE_CONNECTIONSTRING_NAME)]
		public async Task<CurrentSubscription> Subscribe([QueueTrigger("twitch-webhook-subscription", Connection = STORAGE_CONNECTIONSTRING_NAME)] string msg,
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
					return new CurrentSubscription
					{
						ChannelId = channelId,
						ChannelName = msg,
						ExpirationDateTimeUtc = DateTime.UtcNow.AddSeconds(leaseInSeconds).AddDays(-1)
					};

				}

				// Error: Log information about the error and gracefully fail
				var responseBody = await responseMessage.Content.ReadAsStringAsync();
				logger.Log(LogLevel.Error, $"Error response body: {responseBody}");
				return null;

			}

		}

		[FunctionName("ReceiveEndOfStream")]
		public HttpResponseMessage EndOfStream(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
							ILogger log)
						//	,[ServiceBus("EndOfStream", Connection = "ServiceBusConnectionString", EntityType = EntityType.Topic)]
						//out CompletedStream completedStream)
		{

			var channelId = req.Query["channelId"].ToString();
			log.LogMetric("Query", 1, new Dictionary<string, object> { { "TwitchChannelId", channelId } });
			log.LogInformation($"ChannelId: {channelId}");

			// Handle the verification on the WebHook subscription
			var challenge = req.Query["hub.challenge"].ToString();
			if (!string.IsNullOrEmpty(challenge))
			{

				log.LogInformation($"Successfully subscribed to channel {channelId}");

				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(challenge)
				};
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

			//var videoId = GetLastVideoForChannel(channelId).GetAwaiter().GetResult();
			//log.LogInformation($"Found last video with id: {videoId}");

			//completedStream.ChannelName = base.GetUserNameForChannelId(channelId).GetAwaiter().GetResult();
			//completedStream.ChannelId = channelId;
			//completedStream.VideoId = videoId;

			return new HttpResponseMessage(HttpStatusCode.OK);

		}
	}
}
