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
		public ActiveChannel DecideOnChannelToHost(IEnumerable<ActiveChannel> activeChannels) {

			if (string.IsNullOrEmpty(_Configuration["HostChannels"])) return null;

			/// Start with:  Science & Technology category, Mature=false			
			return activeChannels.Where(a => a.Category == "Science & Technology")
				.Where(a => !a.Mature)
				.OrderBy(a => Guid.NewGuid())
				.FirstOrDefault();


		}

	
	}


}
