using CommonIR.Errors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CommonIR.Generators.Binary.DXIL
{
    public class DXILCompiler
    {
        public string CompilerPath { get; }

        public DXILCompiler(string? compilerPath = null)
        {
            this.CompilerPath = compilerPath ?? Locate() ?? throw ErrorHandler.Create("Could not find the DirectX Shader Compiler (dxc.exe). Install the Windows SDK, add dxc.exe to PATH, or set the DXC_PATH environment variable.");

            if (!File.Exists(this.CompilerPath))
            {
                throw ErrorHandler.Create($"The DirectX Shader Compiler was not found at '{this.CompilerPath}'.");
            }
        }

        public static string? Locate()
        {
            string? fromEnvironment = Environment.GetEnvironmentVariable("DXC_PATH");

            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return Directory.Exists(fromEnvironment) ? Path.Combine(fromEnvironment, "dxc.exe") : fromEnvironment;
            }

            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(directory.Trim(), "dxc.exe");

                if (File.Exists(candidate) && File.Exists(Path.Combine(directory.Trim(), "dxil.dll")))
                {
                    return candidate;
                }
            }

            foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                string kits = Path.Combine(root, "Windows Kits", "10", "bin");

                if (!Directory.Exists(kits))
                {
                    continue;
                }

                string? newest = Directory.GetDirectories(kits)
                    .Select(d => (Path: d, Version: Version.TryParse(Path.GetFileName(d), out Version? v) ? v : null))
                    .Where(d => d.Version != null && File.Exists(Path.Combine(d.Path, "x64", "dxc.exe")))
                    .OrderByDescending(d => d.Version)
                    .Select(d => Path.Combine(d.Path, "x64", "dxc.exe"))
                    .FirstOrDefault();

                if (newest != null)
                {
                    return newest;
                }
            }

            return null;
        }

        public byte[] Compile(string source, string entryPoint, string profile, string name)
        {
            string directory = Path.Combine(Path.GetTempPath(), $"CommonIR-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            try
            {
                string sourcePath = Path.Combine(directory, $"{name}.hlsl");
                string outputPath = Path.Combine(directory, $"{name}.dxil");

                File.WriteAllText(sourcePath, source);

                ProcessStartInfo startInfo = new ProcessStartInfo(this.CompilerPath)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(this.CompilerPath)!,
                };

                foreach (string argument in new[] { "-nologo", "-T", profile, "-E", entryPoint, "-O3", "-Qstrip_debug", "-Qstrip_reflect", "-Fo", outputPath, sourcePath })
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using Process process = Process.Start(startInfo) ?? throw ErrorHandler.Create($"Could not start '{this.CompilerPath}'.");

                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                process.WaitForExit();

                if (process.ExitCode != 0 || !File.Exists(outputPath))
                {
                    throw ErrorHandler.Create($"The DirectX Shader Compiler failed to compile '{name}':{Environment.NewLine}{errors.Result}{output.Result}{Environment.NewLine}{source}");
                }

                return File.ReadAllBytes(outputPath);
            }
            finally
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
