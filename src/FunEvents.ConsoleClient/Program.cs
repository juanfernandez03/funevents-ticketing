using FunEvents.ConsoleClient;

if (args is ["-h"] or ["--help"])
{
    Console.WriteLine(ClientOptions.Usage);
    return 0;
}

if (!ClientOptions.TryParse(args, out _, out var error))
{
    Console.Error.WriteLine(error);
    Console.WriteLine();
    Console.WriteLine(ClientOptions.Usage);
    return 2;
}

return 0;
