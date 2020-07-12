using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost
{
	public class ChannelRepository
	{

		private List<string> _Channels = new List<string> {
			"csharpfritz",
			"microsoftdeveloper",
			"baldbeardedbuilder",
			"1kevgriff",
			"coderushed"
		};

		public List<string> GetOtherChannels() {
			return _Channels;
		}


	}
}
