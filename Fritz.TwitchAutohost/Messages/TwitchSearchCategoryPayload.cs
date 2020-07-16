using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost.Messages
{
	public class TwitchSearchCategoryPayload
	{
		public CategoryResult[] data { get; set; }
		public Pagination pagination { get; set; }

		public class Pagination
		{
			public string cursor { get; set; }
		}

		public class CategoryResult
		{
			public string id { get; set; }
			public string name { get; set; }
			public string box_art_url { get; set; }
		}

	}

}
