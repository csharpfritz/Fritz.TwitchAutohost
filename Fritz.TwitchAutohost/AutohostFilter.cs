using Fritz.TwitchAutohost.Data;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fritz.TwitchAutohost
{

	public class AutohostFilter
	{

		private readonly IConfiguration _Configuration;

		public AutohostFilter(IConfiguration configuration)
		{
			_Configuration = configuration;
		}

		/// <summary>
		/// Inspect the submitted list of active channels, apply our filters and return a preferred channel to host
		/// </summary>
		/// <param name="activeChannels"></param>
		/// <returns></returns>
		public ActiveChannel DecideOnChannelToHost(IEnumerable<ActiveChannel> activeChannels, IEnumerable<string> channelsToAvoid = null) {

			if (string.IsNullOrEmpty(_Configuration["HostChannels"])) return null;

			channelsToAvoid = channelsToAvoid ?? new string[] { };

			/// Start with:  Science & Technology category, Mature=false			
			return activeChannels.Where(ChannelCriteria)
				.Where(a => !channelsToAvoid.Contains(a.UserName))
				.OrderBy(a => Guid.NewGuid())
				.FirstOrDefault();


		}

		public Func<ActiveChannel, bool> ChannelCriteria => 
			new Func<ActiveChannel, bool>
				(a => a.Category == "Science & Technology" && !a.Mature);


		internal bool IsValid(ActiveChannel channelInformation)
		{
			return ChannelCriteria(channelInformation);
		}

	}


}
