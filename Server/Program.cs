using ElementWar.Server;

// ============================================================
// ElementWar PVP 权威服务器（.NET UDP，参考 CalabiYau 架构）
// 运行：cd Server && dotnet run
// 可选参数：--port 7777 --tickrate 60（默认 60Hz；GameWorldSettings.ServerTickRate 必须与之一致）
// ============================================================

var options = new UdpGameServerOptions
{
    ListenPort = ParseArg("--port", 7777),
    TickRate = ParseArg("--tickrate", 60),
};
options.WorldSettings.EnableBots = ParseFlag("--bots");   // 默认关：单人纯测试不被 bot 秒杀；`--bots` 开训练模式
options.WorldSettings.ServerTickRate = options.TickRate;

if (ParseFlag("--self-test"))
{
    if (!GameRulesFileLoader.TryLoadDefault(out var selfTestRules, out string selfTestHash, out string selfTestError))
        throw new InvalidOperationException($"规则校验失败：{selfTestError}");
    Console.WriteLine($"[Rules] loaded ruleset={selfTestRules.rulesetId} hash={selfTestHash}");
    SelfTests.Run();
    return;
}

if (!GameRulesFileLoader.TryLoadDefault(out var rules, out string rulesHash, out string rulesError))
{
    Console.Error.WriteLine($"[ElementWarServer] 规则校验失败，拒绝启动：{rulesError}");
    Environment.ExitCode = 2;
    return;
}
options.WorldSettings.ApplyRules(rules.pvp_1v1);
options.WorldSettings.RulesetId = rules.rulesetId;
options.WorldSettings.RulesContentHash = rulesHash;

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

static bool ParseFlag(string key)
{
    string[] args = Environment.GetCommandLineArgs();
    return Array.IndexOf(args, key) >= 0;
}
