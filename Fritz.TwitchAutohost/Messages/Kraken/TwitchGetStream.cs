﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost.Messages.Kraken
{
	public class TwitchGetStream
	{
		public Stream stream { get; set; }

		public class Stream
		{
			public long _id { get; set; }
			public string game { get; set; }
			public string broadcast_platform { get; set; }
			public string community_id { get; set; }
			public object[] community_ids { get; set; }
			public int viewers { get; set; }
			public int video_height { get; set; }
			public int average_fps { get; set; }
			public int delay { get; set; }
			public DateTime created_at { get; set; }
			public bool is_playlist { get; set; }
			public string stream_type { get; set; }
			public Preview preview { get; set; }
			public Channel channel { get; set; }
		}

		public class Preview
		{
			public string small { get; set; }
			public string medium { get; set; }
			public string large { get; set; }
			public string template { get; set; }
		}

		public class Channel
		{
			public bool mature { get; set; }
			public string status { get; set; }
			public string broadcaster_language { get; set; }
			public string broadcaster_software { get; set; }
			public string display_name { get; set; }
			public string game { get; set; }
			public string language { get; set; }
			public int _id { get; set; }
			public string name { get; set; }
			public DateTime created_at { get; set; }
			public DateTime updated_at { get; set; }
			public bool partner { get; set; }
			public string logo { get; set; }
			public string video_banner { get; set; }
			public string profile_banner { get; set; }
			public string profile_banner_background_color { get; set; }
			public string url { get; set; }
			public int views { get; set; }
			public int followers { get; set; }
			public string broadcaster_type { get; set; }
			public string description { get; set; }
			public bool private_video { get; set; }
			public bool privacy_options_enabled { get; set; }
		}


	}
}