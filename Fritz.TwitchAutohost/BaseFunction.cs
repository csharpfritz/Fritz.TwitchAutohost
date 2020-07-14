using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Fritz.TwitchAutohost
{
	public abstract class BaseFunction
	{

		public const string TWITCH_SECRET = "This!5MySecr3t";

		private readonly IHttpClientFactory _HttpClientFactory;

		protected BaseFunction(IConfiguration configuration, IHttpClientFactory httpClientFactory)
		{
			Configuration = configuration;
			_HttpClientFactory = httpClientFactory;
		}

		protected IConfiguration Configuration { get; }

		protected HttpClient GetHttpClient(string baseAddress, string clientId = "", bool includeJson = true, bool authHeader = false)
		{

			if (clientId == "") clientId = TwitchAuthTokens.Instance.ClientId;

			var client = _HttpClientFactory.CreateClient();
			client.BaseAddress = new Uri(baseAddress);

			if (includeJson)
			{
				client.DefaultRequestHeaders.Add("Accept", @"application/json");
			}
			client.DefaultRequestHeaders.Add("Accept", @"application/vnd.twitchtv.v5+json");
			client.DefaultRequestHeaders.Add("Client-ID", clientId);

			if (authHeader) {
				client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TwitchAuthTokens.Instance.AccessToken}");
			}

			return client;

		}

    protected async Task<bool> VerifyPayloadSecret(HttpRequest req, ILogger log)
    {

      var signature = req.Headers["X-Hub-Signature"].ToString();

      log.LogInformation($"Twitch Signature sent: {signature}");

      var ourHashCalculation = string.Empty;
      if (req.Body.CanSeek)
      {
        using (var reader = new StreamReader(req.Body, Encoding.UTF8))
        {
          req.Body.Position = 0;
          var bodyContent = await reader.ReadToEndAsync();
          ourHashCalculation = CreateHmacHash(bodyContent, TWITCH_SECRET);
        }
      }

      log.LogInformation($"Our calculated signature: {ourHashCalculation}");
      return true;

    }

    protected static string CreateHmacHash(string data, string key)
    {

      var keybytes = UTF8Encoding.UTF8.GetBytes(key);
      var dataBytes = UTF8Encoding.UTF8.GetBytes(data);

      var hmac = new HMACSHA256(keybytes);
      var hmacBytes = hmac.ComputeHash(dataBytes);

      return Convert.ToBase64String(hmacBytes);

    }

		/// <summary>
		/// Get the numeric id for a channel based on a username
		/// </summary>
		/// <param name="userName"></param>
		/// <returns></returns>
		protected async Task<string> GetChannelIdForUserName(string userName)
		{


			var client = GetHttpClient("https://api.twitch.tv/helix/", authHeader: true);

			string body = string.Empty;
			try
			{
				var msg = await client.GetAsync($"users?login={userName}");
				msg.EnsureSuccessStatusCode();
				body = await msg.Content.ReadAsStringAsync();
			}
			catch (HttpRequestException e)
			{
				throw;
			}

			var obj = JObject.Parse(body);
			return obj["data"][0]["id"].ToString();

		}
  }
}
