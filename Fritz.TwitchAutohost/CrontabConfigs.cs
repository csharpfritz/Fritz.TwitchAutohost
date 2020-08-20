using System;
using System.Collections.Generic;
using System.Text;

namespace Fritz.TwitchAutohost
{

	/// <summary>
	/// A collection of Azure Timer-Trigger appropriate CronTab strings 
	/// </summary>
	internal static class CrontabConfigs
	{

		/// <summary>
		/// Run at 0:02 on every second hour
		/// </summary>
		public const string EVERY_SECOND_HOUR_AND_SECOND_MINUTE = "0 2 */2 * * *";

		/// <summary>
		/// Run every thirty minutes on the 28th and 58th minute
		/// </summary>
		public const string EVERY_THIRTY_MINUTES = "0 28,58 * * * *";

	}
}
