using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Fritz.TwitchAutohost.Startup))]

namespace Fritz.TwitchAutohost
{


	public class Startup : FunctionsStartup
	{

		public override void Configure(IFunctionsHostBuilder builder)
		{

			builder.Services.AddHttpClient();

		}
	}

}
