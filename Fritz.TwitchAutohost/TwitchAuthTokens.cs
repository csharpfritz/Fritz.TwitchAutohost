using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost
{
	public class TwitchAuthTokens
	{

		public static readonly TwitchAuthTokens Instance = new TwitchAuthTokens();

		private TwitchAuthTokens() {

			UserName = Environment.GetEnvironmentVariable("TwitchUserName");
			OAuthToken = Environment.GetEnvironmentVariable("OAuthToken");
			AccessToken = Environment.GetEnvironmentVariable("AccessToken");
			RefreshToken = Environment.GetEnvironmentVariable("RefreshToken");
			ClientId = Environment.GetEnvironmentVariable("ClientId");
		}

		public string UserName { get; set; }

		public string OAuthToken { get; set; }

		public string AccessToken { get; set; }

		public string RefreshToken { get; set; }

		public string ClientId { get; set; }

	}
}
