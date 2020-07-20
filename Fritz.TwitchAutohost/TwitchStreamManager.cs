using Fritz.TwitchAutohost.Data;
using Fritz.TwitchAutohost.Messages;
using Fritz.TwitchAutohost.Messages.Kraken;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;

namespace Fritz.TwitchAutohost
{
	public class TwitchStreamManager : BaseFunction
	{

		private readonly string _MyChannelId;

		public TwitchStreamManager(IConfiguration configuration, IHttpClientFactory httpClientFactory) : base(configuration, httpClientFactory)
		{
			_MyChannelId = configuration["ChannelId"];
		}

		public async Task HandlePayload(HttpRequest req, ILogger log, string channelId)
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
			}
			else
			{
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
				var autohostFilter = new AutohostFilter(Configuration);

				// TODO: Get information about this Twitch channel and re-inspect JUST this channel
				var valid = false;
				var channelsToAvoid = new List<string>();
				ActiveChannel nextChannel = null;
				while (!valid)
				{
					nextChannel = autohostFilter.DecideOnChannelToHost(channels, channelsToAvoid);
					var channelInformation = await GetInformationForChannel(nextChannel.UserName);
					valid = channelInformation.Category == ActiveChannel.OFFLINE ? false : autohostFilter.IsValid(channelInformation);
					if (!valid) channelsToAvoid.Add(nextChannel.UserName);
				}

				if (nextChannel == null) return;
				// HOST THE CHANNEL --- SEND COMMAND TO TWITCH TO DO THE THING
				var twitch = new TwitchClient();
				twitch.Initialize(new ConnectionCredentials(TwitchAuthTokens.Instance.UserName, TwitchAuthTokens.Instance.AccessToken));
				twitch.Connect();
				twitch.JoinChannel(TwitchAuthTokens.Instance.UserName);
				twitch.Host(TwitchAuthTokens.Instance.UserName, nextChannel.UserName);
				if (channelWeAreCurrentlyHosting != null) await repo.Remove(channelWeAreCurrentlyHosting);
				await repo.AddOrUpdate(new CurrentHostConfiguration()
				{
					HostedChannelId = nextChannel.ChannelId,
					HostedChannelName = nextChannel.UserName,
					ManagedChannelId = _MyChannelId
				});
			}

		}

		private async Task<ActiveChannel> GetInformationForChannel(string userName)
		{

			var client = GetHttpClient("https://api.twitch.tv/helix/streams", authHeader: true);
			var response = await client.GetAsync($"?user_login={userName}");
			try
			{
				response.EnsureSuccessStatusCode();
			} catch {
				return new OfflineChannel();
			}

			var content = await response.Content.ReadAsStringAsync();
			var payload = JsonConvert.DeserializeObject<TwitchStreamChangeMessage>(content);
			return await ConvertFromPayload(payload);

		}

		public async Task<ActiveChannel> ConvertFromPayload(TwitchStreamChangeMessage payload)
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

		public async Task<string> ConvertFromTwitchCategoryId(string categoryId)
		{

			var client = GetHttpClient("https://api.twitch.tv/helix/games", authHeader: true);
			var result = await client.GetStringAsync($"?id={categoryId}");

			var categoryPayload = JsonConvert.DeserializeObject<TwitchSearchCategoryPayload>(result);
			return categoryPayload.data.Any() ? categoryPayload.data[0].name : "";

		}

		public async Task<bool> GetMatureFlag(string user_id)
		{
			// FETCH mature flag from https://api.twitch.tv/kraken/streams/<channel ID>
			var client = GetHttpClient("https://api.twitch.tv/kraken/streams/", authHeader: true);
			var result = await client.GetStringAsync(user_id);

			return JsonConvert.DeserializeObject<TwitchGetStream>(result).stream.channel.mature;
		}

		public async Task<string[]> ConvertFromTwitchTagIds(string[] tag_ids)
		{

			// TODO: Convert with a query using this syntax:  https://dev.twitch.tv/docs/api/reference#get-all-stream-tags
			var client = GetHttpClient("https://api.twitch.tv/helix/tags/streams", authHeader: true);
			var result = await client.GetStringAsync($"?tag_id={string.Join("&tag_id=", tag_ids)}");

			var tagPayload = JsonConvert.DeserializeObject<TwitchGetTagsPayload>(result);

			return tagPayload.data.Select(d => d.localization_names.EN_US).ToArray();

		}


	}
}
