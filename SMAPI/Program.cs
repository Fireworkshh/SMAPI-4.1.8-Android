using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Framework;
using StardewModdingAPI.Toolkit.Serialization.Models;
using StardewModdingAPI.Toolkit.Utilities;
using StardewValley;

namespace StardewModdingAPI;

/// <summary>The main entry point for SMAPI, responsible for hooking into and launching the game.</summary>
public class Program
{
    /*********
    ** Fields
    *********/
    /// <summary>The absolute path to search for SMAPI's internal DLLs.</summary>
    public static readonly string DllSearchPath = EarlyConstants.InternalFilesPath;

    /// <summary>The assembly paths in the search folders indexed by assembly name.</summary>
    private static Dictionary<string, string>? AssemblyPathsByName;


    /*********
    ** Public methods
    *********/
    /// <summary>The main entry point which hooks into and launches the game.</summary>
    /// <param name="args">The command-line arguments.</param>
    public static void Main()
    {

    
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; // per StardewValley.Program.Main
       // Console.Title = $"SMAPI {EarlyConstants.RawApiVersion}";

        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += Program.CurrentDomain_AssemblyResolve;
            AppDomain.CurrentDomain.AssemblyResolve += Program.HandleAssemblyResolve;
            //   Program.AssertGamePresent();
            // Program.AssertGameVersion();
            //  Program.AssertSmapiVersions();
            //     Program.AssertDepsJson();
            Program.Start();
        }
        catch (BadImageFormatException ex) when (ex.FileName == EarlyConstants.GameAssemblyName)
        {
            Console.WriteLine($"SMAPI failed to initialize because your game's {ex.FileName}.exe seems to be invalid.\nThis may be a pirated version which modified the executable in an incompatible way; if so, you can try a different download or buy a legitimate version.\n\nTechnical details:\n{ex}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SMAPI failed to initialize: {ex}");
            Program.PressAnyKeyToExit(true);
        }
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Method called when assembly resolution fails, which may return a manually resolved assembly.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    ///
    private static Assembly HandleAssemblyResolve(object sender, ResolveEventArgs args)
    {
        string assemblyName = new AssemblyName(args.Name).Name;

        string rootDirectory = Path.Combine("/storage/emulated/0/Android/data/app.SMAPIStardew/files/dotnet/shared/Microsoft.NETCore.App/8.0.11");

        try
        {
            var assemblyPaths = Directory.EnumerateFiles(rootDirectory, "*.dll", SearchOption.AllDirectories)
                .Where(path => Path.GetFileNameWithoutExtension(path) == assemblyName).ToList();

            if (assemblyPaths.Any())
            {
                return Assembly.LoadFrom(assemblyPaths.First());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载程序集时出错: {ex.Message}，程序集名: {assemblyName}");
            throw new Exception($"加载程序集时出错: {ex.Message}", ex);
        }

        return null;
    }

    private static Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs e)
    {
        // Cache assembly paths by name
        if (Program.AssemblyPathsByName == null)
        {
            Program.AssemblyPathsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Include current executing assembly's directory
            string currentAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (currentAssemblyPath != null)
            {
                // Add the directory of the executing assembly to the search paths
                Program.AssemblyPathsByName["CurrentAssembly"] = currentAssemblyPath;
            }

            // Search through predefined directories
            foreach (string searchPath in new[] { EarlyConstants.GamePath, Program.DllSearchPath  })
            {
                foreach (string dllPath in Directory.EnumerateFiles(searchPath, "*.dll"))
                {
                    try
                    {
                        string? curName = AssemblyName.GetAssemblyName(dllPath).Name;
                        if (curName != null)
                        {
                            Program.AssemblyPathsByName[curName] = dllPath;
                        }
                    }
                    catch
                    {
                        // Ignore invalid DLLs
                    }
                }
            }
        }

        // Try to resolve the assembly
        try
        {
            string? searchName = new AssemblyName(e.Name).Name;
            if (searchName != null)
            {
                // Check if the assembly is in the cache and load it
                if (Program.AssemblyPathsByName.TryGetValue(searchName, out string? assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resolving assembly: {ex}");
            return null;
        }
    }


    /// <summary>Assert that the game is available.</summary>
    /// <remarks>This must be checked *before* any references to <see cref="Constants"/>, and this method should not reference <see cref="Constants"/> itself to avoid errors in Mono or when the game isn't present.</remarks>
    private static void AssertGamePresent()
    {
        try
        {
            _ = Type.GetType($"StardewValley.Game1, {EarlyConstants.GameAssemblyName}", throwOnError: true);
        }
        catch (Exception ex)
        {
            // file doesn't exist
            if (!File.Exists(Path.Combine(EarlyConstants.GamePath, $"{EarlyConstants.GameAssemblyName}.exe")))
                Program.PrintErrorAndExit("Oops! SMAPI can't find the game. Make sure you're running StardewModdingAPI.exe in your game folder.");

            // can't load file
            Program.PrintErrorAndExit(
                message: "Oops! SMAPI couldn't load the game executable. The technical details below may have more info.",
                technicalMessage: $"Technical details: {ex}"
            );
        }
    }

    /// <summary>Assert that the game version is within <see cref="Constants.MinimumGameVersion"/>, <see cref="Constants.MinimumGameBuild"/>, and <see cref="Constants.MaximumGameVersion"/>.</summary>
    private static void AssertGameVersion()
    {
        // min version
        int? minBuild = Constants.MinimumGameBuild;
        if (Constants.GameVersion.IsOlderThan(Constants.MinimumGameVersion) || (minBuild.HasValue && Game1.versionBuildNumber < minBuild))
        {
            ISemanticVersion? suggestedApiVersion = Constants.GetCompatibleApiVersion(Constants.GameVersion);

            string errorPhrase = minBuild.HasValue && Constants.GameVersion.CompareTo(Constants.MinimumGameVersion) == 0
                ? $"You're running Stardew Valley {Constants.GameVersion} build {Game1.versionBuildNumber}, but the oldest supported version is build {minBuild}."
                : $"You're running Stardew Valley {Constants.GameVersion}, but the oldest supported version is {Constants.MinimumGameVersion}.";

            Program.PrintErrorAndExit(suggestedApiVersion != null
                ? $"Oops! {errorPhrase} You can install SMAPI {suggestedApiVersion} instead to fix this error, or update your game to the latest version."
                : $"Oops! {errorPhrase} Please update your game before using SMAPI."
            );
        }

        // max version
        if (Constants.MaximumGameVersion != null && Constants.GameVersion.IsNewerThan(Constants.MaximumGameVersion))
            Program.PrintErrorAndExit($"Oops! You're running Stardew Valley {Constants.GameVersion}, but this version of SMAPI is only compatible up to Stardew Valley {Constants.MaximumGameVersion}. Please check for a newer version of SMAPI: https://smapi.io.");
    }

    /// <summary>Assert that the versions of all SMAPI components are correct.</summary>
    /// <remarks>Players sometimes have mismatched versions (particularly when installed through Vortex), which can cause some very confusing bugs without this check.</remarks>
    private static void AssertSmapiVersions()
    {
        // get SMAPI version without prerelease suffix (since we can't get that from the assembly versions)
        ISemanticVersion smapiVersion = new SemanticVersion(Constants.ApiVersion.MajorVersion, Constants.ApiVersion.MinorVersion, Constants.ApiVersion.PatchVersion);

        // compare with assembly versions
        foreach (var type in new[] { typeof(IManifest), typeof(Manifest) })
        {
            AssemblyName assemblyName = type.Assembly.GetName();
            ISemanticVersion assemblyVersion = new SemanticVersion(assemblyName.Version!);
            if (!assemblyVersion.Equals(smapiVersion))
                Program.PrintErrorAndExit($"Oops! The 'smapi-internal/{assemblyName.Name}.dll' file is version {assemblyVersion} instead of the required {Constants.ApiVersion}. SMAPI doesn't seem to be installed correctly.");
        }
    }

    /// <summary>Assert that SMAPI's <c>StardewModdingAPI.deps.json</c> matches <c>Stardew Valley.deps.json</c>, fixing it if necessary.</summary>
    /// <remarks>This is needed to resolve native DLLs like libSkiaSharp.</remarks>
    private static void AssertDepsJson()
    {
        string sourcePath = Path.Combine(Constants.GamePath, "Stardew Valley.deps.json");
        string targetPath = Path.Combine(Constants.GamePath, "StardewModdingAPI.deps.json");

        if (!File.Exists(targetPath) || FileUtilities.GetFileHash(sourcePath) != FileUtilities.GetFileHash(targetPath))
        {
            File.Copy(sourcePath, targetPath, overwrite: true);

            Console.WriteLine("A new game version was installed, so SMAPI needs to update its settings.");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Please exit this window and re-launch SMAPI to play.");
            Console.ResetColor();

            Thread.Sleep(2500);
            //666 Environment.Exit(0);
        }
    }
   


    /// <summary>Initialize SMAPI and launch the game.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <remarks>This method is separate from <see cref="Main"/> because that can't contain any references to assemblies loaded by <see cref="CurrentDomain_AssemblyResolve"/> (e.g. via <see cref="Constants"/>), or Mono will incorrectly show an assembly resolution error before assembly resolution is set up.</remarks>
    public static void Start()
    {



    //  string      modsPath =  Constants.DefaultModsPath;
        

        // load SMAPI
    //   using SCore core = new(modsPath, false, false);
     //   core.RunInteractively();
    }

    /// <summary>Write an error directly to the console and exit.</summary>
    /// <param name="message">The error message to display.</param>
    /// <param name="technicalMessage">An additional message to log with technical details.</param>
    private static void PrintErrorAndExit(string message, string? technicalMessage = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();

        if (technicalMessage != null)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(technicalMessage);
            Console.ResetColor();
            Console.WriteLine();
        }

        Program.PressAnyKeyToExit(showMessage: true);
    }

    /// <summary>Show a 'press any key to exit' message, and exit when they press a key.</summary>
    /// <param name="showMessage">Whether to print a 'press any key to exit' message to the console.</param>
    private static void PressAnyKeyToExit(bool showMessage)
    {
        if (showMessage)
            Console.WriteLine("Game has ended. Press any key to exit.");
        Thread.Sleep(100);
      
    }
}
