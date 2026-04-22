using System.IO;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;

namespace _build;

class Build : NukeBuild
{
    const string InstallerAppVersion = "1.0.0";

    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration (Debug ou Release).")]
    readonly Configuration Configuration = Configuration.Debug;

    [Parameter("Pasta do Revit em Addins (ex.: 2025). O manifesto é colocado em …\\Addins\\{ano}\\.")]
    readonly int RevitAddInYear = 2025;

    [Parameter("Caminho completo para ISCC.exe (Inno Setup). Se vazio, tenta as pastas por defeito.")]
    readonly string? InnoSetupCompilerPath;

    AbsolutePath SourceProject => RootDirectory / "ClassLibrary2" / "AutoDocumentation.csproj";
    AbsolutePath StagingDirectory => RootDirectory / "artifacts" / "staging";
    AbsolutePath InstallerOutputDirectory => RootDirectory / "artifacts" / "installer";
    AbsolutePath InnoScript => RootDirectory / "install" / "AutoDocumentation.iss";

    Target Clean => _ => _
        .Executes(() =>
        {
            (RootDirectory / "artifacts").CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore(s => s.SetProjectFile(SourceProject));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(SourceProject)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target PublishAddIn => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            StagingDirectory.CreateOrCleanDirectory();
            DotNetTasks.DotNetPublish(s => s
                .SetProject(SourceProject)
                .SetConfiguration(Configuration.Release)
                .SetFramework("net8.0-windows")
                .SetOutput(StagingDirectory)
                .AddProperty("DebugType", "none")
                .AddProperty("DebugSymbols", "false"));
        });

    Target Installer => _ => _
        .DependsOn(PublishAddIn)
        .Executes(() =>
        {
            InstallerOutputDirectory.CreateDirectory();

            var iscc = ResolveInnoSetupCompiler();
            if (string.IsNullOrWhiteSpace(iscc) || !File.Exists(iscc))
                throw new Exception(
                    "Inno Setup 6 não encontrado. Instale a partir de https://jrsoftware.org/isdl.php " +
                    "ou defina InnoSetupCompilerPath apontando para ISCC.exe.");

            if (!InnoScript.FileExists())
                throw new Exception($"Ficheiro de script em falta: {InnoScript}");

            var stagingForInno = StagingDirectory.ToString().Replace('\\', '/').TrimEnd('/');
            var outForInno = InstallerOutputDirectory.ToString().Replace('\\', '/').TrimEnd('/');

            var args =
                $"/DMyAppVersion={InstallerAppVersion} /DRevitYear={RevitAddInYear} /DStagingDir={stagingForInno} /DOutputDir={outForInno} \"{InnoScript}\"";

            ProcessTasks.StartProcess(iscc, args, logInvocation: true);

            var readmeSrc = RootDirectory / "install" / "README-DISTRIBUICAO.md";
            if (readmeSrc.FileExists())
            {
                var readmeDest = InstallerOutputDirectory / "README.md";
                File.Copy(readmeSrc, readmeDest, overwrite: true);
            }
        });

    string? ResolveInnoSetupCompiler()
    {
        if (!string.IsNullOrWhiteSpace(InnoSetupCompilerPath) && File.Exists(InnoSetupCompilerPath))
            return InnoSetupCompilerPath;

        var candidates = new[]
        {
            @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            @"C:\Program Files\Inno Setup 6\ISCC.exe"
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
