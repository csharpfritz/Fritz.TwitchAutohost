namespace Fritz.TwitchAutohost
{
	/// <summary>
	/// Current state of the channel inspected -- is it hosting, broadcasting, or nothing
	/// </summary>
	public enum HostingState
	{
		Nothing = 0,
		Hosting = 1,
		Broadcasting = 2
	}
}
