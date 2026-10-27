using Ew4.Tool.Cli;

if (args.Length == 0)
{
    PrintHelp();
    return 1;
}

return args[0] switch
{
    "import-base" => ImportBaseCli.Run(args),
    "smoke-start" => SmokeStartCli.Run(args),
    "validate-mod" => ValidateModCli.Run(args),
    "import-apk-overlay" => ImportApkOverlayCli.Run(args),
    "scenario" => ScenarioCli.Run(args),
    "workspace" => WorkspaceCli.Run(args),
    "export-apk" => ExportApkCli.Run(args),
    _ => Unknown(args[0]),
};

static int Unknown(string command)
{
    Console.Error.WriteLine("Unknown command: " + command);
    PrintHelp();
    return 2;
}

static void PrintHelp()
{
    Console.WriteLine("""
        Ew4.Tool
          import-base --data <mods/import-seed> --dest <mods/base> [--btl <dir-with-btl>]
          smoke-start [--packs mods] [--scenario campaign1_01]
          validate-mod [--packs mods] [--pack base] [--level schema|reference|smoke]
          import-apk-overlay --apk <file.apk> --id <modId> --name <display> [--base mods/base] [--dest mods/<id>]
          scenario <subcommand>  (see: scenario btl-to-json --help via invalid subcommand)
          workspace <subcommand>  (list|create|delete|validate|export)
          export-apk --out <apk> [--mod <id>|--workspace|--ew4mod] …  (see export-apk --help)
        """);
}
