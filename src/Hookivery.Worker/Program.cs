using Hookivery.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<HeartbeatWorker>();

var host = builder.Build();
await host.RunAsync();
