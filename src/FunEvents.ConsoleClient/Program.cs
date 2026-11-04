using FunEvents.ConsoleClient;

if (!ClientOptions.TryParse(args, out _, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}

return 0;
