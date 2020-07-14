using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost.Messages
{
	public class TwitchWebhookSubscriptionPayload
	{

		[JsonProperty("hub.callback")]
		public string callback { get; set; }

		[JsonProperty("hub.mode")]
		public string mode { get; set; }

		[JsonProperty("hub.topic")]
		public string topic { get; set; }

		[JsonProperty("hub.lease_seconds")]
		public int lease_seconds { get; set; }

		[JsonProperty("hub.secret")]
		public string secret { get; set; }

	}
}
