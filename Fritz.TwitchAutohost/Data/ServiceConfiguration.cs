using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost.Data
{
	public class ServiceConfiguration
	{

		public ServiceConfiguration(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		public string MyChannelId { get { return Configuration["ChannelId"]; } }

		public string MyChannelName => Configuration["TwitchUserName"];

		public string StorageConnectionString { get { return Configuration["TwitchAutohostStorage"]; } }

		public bool HostChannelsActive
		{
			get
			{
				return !string.IsNullOrEmpty(Configuration["HostChannelsActive"]) && bool.Parse(Configuration["HostChannelsActive"]);
			}
		}

		public bool DryRun => string.IsNullOrEmpty(Configuration["DryRun"]) || bool.Parse(Configuration["DryRun"]);

		public Uri EndpointBaseUrl => new Uri(Configuration["EndpointBaseUrl"]);

		public string ClientId => Configuration["ClientId"];
	}
}
