using Microsoft.Extensions.Hosting;

#region Builder

var appBuilder = Host.CreateApplicationBuilder();

#endregion

#region App

var app = appBuilder.Build();

await app.RunAsync();

#endregion