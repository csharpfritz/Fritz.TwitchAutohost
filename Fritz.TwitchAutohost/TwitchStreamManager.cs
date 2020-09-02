using Fritz.TwitchAutohost.Data;
using Fritz.TwitchAutohost.Messages;
using Fritz.TwitchAutohost.Messages.Kraken;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;

namespace Fritz.TwitchAutohost
{
	public class TwitchStreamManager : BaseFunction
	{

		private readonly string _MyChannelId;
		private readonly ILogger _Logger;

		public TwitchStreamManager(ServiceConfiguration configuration, ILogger logger, IHttpClientFactory httpClientFactory) : base(configuration, httpClientFactory)
		{
			_MyChannelId = configuration.MyChannelId;
			ChannelHostingActivated = configuration.HostChannelsActive;
			DryRun = configuration.DryRun;
			_Logger = logger;
		}

		internal static bool ChannelHostingActivated = false;
		internal static bool DryRun = true;

		public async Task HandleStreamUpdateWebHookPayload(HttpRequest req, ILogger log, string channelId)
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
				var configRepo = new CurrentHostConfigurationRepository(Configuration);

				if (channelId == _MyChannelId) {

					// Force the channel category to Science and Technology
					await SetChannelCategory();

					// My channel started an original stream -> log we are currently broadcasting
					_Logger.LogInformation($"We just started a stream on {Configuration.MyChannelName}");
					await configRepo.AddOrUpdate(new CurrentHostConfiguration {
						ManagedChannelId = _MyChannelId,
						HostedChannelId = _MyChannelId,
						HostedChannelName = ""
					});

				} else if (configRepo.GetForMyChannel() == null) {

					// We are not currently hosting
					_Logger.LogInformation($"We are not currently hosting - let's look for another channel to host");
					await HostTheNextStream(channelId);

				}

			}

		}

		/// <summary>
		/// Set the Channel category for our channel to Science & Technology
		/// </summary>
		/// <returns></returns>
		private async Task SetChannelCategory()
		{

			var api = new TwitchAPI();
			api.Settings.ClientId = Configuration.ClientId;
			api.Settings.AccessToken = TwitchAuthTokens.Instance.AccessToken;

			_Logger.LogInformation("Setting category to Science & Technology");
			await api.V5.Channels.UpdateChannelAsync(Configuration.MyChannelId, game: "Science & Technology");

		}

		internal async Task HostTheNextStream(string channelIdThatJustEnded)
		{

			if (!ChannelHostingActivated)
			{
				_Logger.LogInformation($"Channel hosting is not activated - will not look for a channel to host");
				return;
			}

			var repo = new CurrentHostConfigurationRepository(Configuration);
			var channelWeAreCurrentlyHosting = repo.GetByRowKey(_MyChannelId);

			
			if (channelIdThatJustEnded == _MyChannelId ||		// Our show just ended...
				(channelWeAreCurrentlyHosting == null) ||			// We are not currently hosting anyone
				(channelWeAreCurrentlyHosting != null && channelIdThatJustEnded == channelWeAreCurrentlyHosting.HostedChannelId))
			{

				_Logger.LogInformation($"Our stream OR a stream we were hosting ended... looking for another channel");

				var channels = await new ActiveChannelRepository(Configuration).GetAllActiveChannels();
				var autohostFilter = new AutohostFilter(Configuration);

				// TODO: Get information about this Twitch channel and re-inspect JUST this channel
				var valid = false;
				var channelsToAvoid = new List<string>();
				ActiveChannel nextChannel = null;
				while (!valid)
				{
					nextChannel = autohostFilter.DecideOnChannelToHost(channels, channelsToAvoid);
					if (nextChannel == null) break; // Exit now if no channel found

					var channelInformation = await GetInformationForChannel(nextChannel.UserName);
					valid = channelInformation.Category == ActiveChannel.OFFLINE ? false : autohostFilter.IsValid(channelInformation);
					if (!valid) channelsToAvoid.Add(nextChannel.UserName);
				}

				if (nextChannel == null || nextChannel.Category == ActiveChannel.OFFLINE)
				{

					_Logger.LogInformation($"Cannot identify another channel to host");

					// Clear the current host config as we are not hosting again
					await repo.Remove(repo.GetForMyChannel());
					await LogHostAction(channelWeAreCurrentlyHosting, new ActiveChannel { 
						Category = "",
						Tags= new string[] { },
						Title="- OFFLINE -",
						UserName = "< OFFLINE >"
					});
					return;

				}
				// HOST THE CHANNEL --- SEND COMMAND TO TWITCH TO DO THE THING

				_Logger.LogInformation($"Starting host for: {nextChannel.UserName} with title '{nextChannel.Title}'");
				if (!DryRun) await SendHostCommandToTwitch(repo, channelWeAreCurrentlyHosting, nextChannel);

				await LogHostAction(channelWeAreCurrentlyHosting, nextChannel);

				await repo.AddOrUpdate(new CurrentHostConfiguration()
				{
					HostedChannelId = nextChannel.ChannelId,
					HostedChannelName = nextChannel.UserName,
					ManagedChannelId = _MyChannelId
				});
			}

		}

		private async Task SendHostCommandToTwitch(CurrentHostConfigurationRepository repo, CurrentHostConfiguration channelWeAreCurrentlyHosting, ActiveChannel nextChannel)
		{
			var twitch = new TwitchClient();
			twitch.Initialize(new ConnectionCredentials(TwitchAuthTokens.Instance.UserName, TwitchAuthTokens.Instance.AccessToken));
			twitch.Connect();
			twitch.JoinChannel(TwitchAuthTokens.Instance.UserName);

			_Logger.LogInformation($"Sending host command for channel {nextChannel.UserName}");

			twitch.Host(TwitchAuthTokens.Instance.UserName, nextChannel.UserName);
			if (channelWeAreCurrentlyHosting != null) await repo.Remove(channelWeAreCurrentlyHosting);
		}

		private async Task LogHostAction(CurrentHostConfiguration channelWeAreCurrentlyHosting, ActiveChannel nextChannel)
		{
			var logRepo = new HostLogRepository(Configuration);
			await logRepo.AddOrUpdate(new HostLog
			{
				Category = nextChannel.Category,
				FromChannelName = channelWeAreCurrentlyHosting?.HostedChannelName ?? "",
				Tags = string.Join('|', nextChannel.Tags),
				Title = nextChannel.Title,
				ToChannelName = nextChannel.UserName
			});
		}

		/// <summary>
		/// Inspect the channel of the Twitch user submitted and immediately log their Active channel status
		/// </summary>
		/// <param name="userName"></param>
		/// <returns></returns>
		internal async Task InspectChannelAndLogStatus(string userName) {

			var status = await GetInformationForChannel(userName);
			if (status.Category == ActiveChannel.OFFLINE) return;

			var repo = new ActiveChannelRepository(Configuration);
			await repo.AddOrUpdate(status);

		}

		internal async Task<ActiveChannel> GetInformationForChannel(string userName)
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

			if (payload.data.Length == 0) return new OfflineChannel();
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
				Title = twitchStream.title,
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

			if (tag_ids == null) return new string[] { };

			var client = GetHttpClient("https://api.twitch.tv/helix/tags/streams", authHeader: true);
			var result = await client.GetStringAsync($"?tag_id={string.Join("&tag_id=", tag_ids)}");

			var tagPayload = JsonConvert.DeserializeObject<TwitchGetTagsPayload>(result);

			return tagPayload.data.Select(d => d.localization_names.EN_US).ToArray();

		}


	}
}
