using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost.Messages
{
	public class TwitchGetTagsPayload
	{
		public TagData[] data { get; set; }
		public Pagination pagination { get; set; }

		public class Pagination
		{
			public string cursor { get; set; }
		}

		public class TagData
		{
			public string tag_id { get; set; }
			public bool is_auto { get; set; }
			public Localization_Names localization_names { get; set; }
			public Localization_Descriptions localization_descriptions { get; set; }
		}

		public class Localization_Names
		{
			[JsonProperty(PropertyName = "en-us")]
			public string EN_US { get; set; }
		}

		public class Localization_Descriptions
		{

			[JsonProperty(PropertyName = "en-us")]
			public string EN_US { get; set; }
		}

	}

}