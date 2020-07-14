using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Fritz.TwitchAutohost
{
	public static class Autohost
	{
		//[FunctionName("Function1")]
		//public static async Task<IActionResult> Run(
		//    [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
		//    ILogger log)
		//{
		//    log.LogInformation("C# HTTP trigger function processed a request.");

		//    string name = req.Query["name"];

		//    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
		//    dynamic data = JsonConvert.DeserializeObject(requestBody);
		//    name = name ?? data?.name;

		//    string responseMessage = string.IsNullOrEmpty(name)
		//        ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
		//        : $"Hello, {name}. This HTTP triggered function executed successfully.";

		//    return new OkObjectResult(responseMessage);
		//}

		private readonly static Dictionary<HostingState, Action> _HostingActions = new Dictionary<HostingState, Action>
		{
			{ HostingState.Nothing, IdentifyChannelToHost },
			{ HostingState.Broadcasting, () => { }  },
			{ HostingState.Hosting, ConsiderHostingChange }
		};

		//[FunctionName(nameof(MonitorCurrentState))]
		//public static async Task MonitorCurrentState([TimerTrigger("* * * * *")] TimerInfo myTimer)
		//{

		//	var hostingState = IdentifyHostingState(TwitchAuthTokens.Instance.UserName);

		//	return;
			

		//}

		private static HostingState IdentifyHostingState(string channelName)
		{

			// TODO: Query this endpoint to determine current hosting
			// "https://tmi.twitch.tv/hosts?include_logins=1&host=51684790"
			// TODO: Convert ChannelName to user_id

			// TODO: Get hosting for MULTIPLE channels GET https://api.twitch.tv/helix/streams
			return HostingState.Nothing;

		}

		private static void ConsiderHostingChange()
		{
			throw new NotImplementedException();
		}

		private static void IdentifyChannelToHost()
		{
			throw new NotImplementedException();
		}



	}
}
