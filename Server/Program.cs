using ElementWar.Server;

// ============================================================
// ElementWar PVP 权威服务器（.NET UDP，参考 CalabiYau 架构）
// 运行：cd Server && dotnet run
// 可选参数：--port 7777 --tickrate 30
// ============================================================

var options = new UdpGameServerOptions
{
    ListenPort = ParseArg("--port", 7777),
    TickRate = ParseArg("--tickrate", 30),
};

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

using var server = new UdpGameServer(options);
try
{
    await server.RunAsync(shutdown.Token);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    Console.WriteLine("UDP server stopped.");
}

static int ParseArg(string key, int fallback)
{
    string[] args = Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(args[i + 1], out int v))
            return v;
    }
    return fallback;
}
