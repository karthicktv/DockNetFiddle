using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DockNetFiddle.Models;
using System.IO;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace DockNetFiddle.Services
{
    public class ProgramExecutor : IProgramExecutor
    {
        private IHostingEnvironment env;
        public ProgramExecutor(IHostingEnvironment env)
        {
            this.env = env;
        }

        public async Task<string> Execute(ProgramSpecification program)
        {
            var zipFileName = Guid.NewGuid().ToString() + ".zip";
            var zipFilePath = Path.Combine("/requests", zipFileName);
            var expectedOutputFile = Path.Combine("/outputs", zipFileName + ".output");
            try
            {
                DropToZip(program, zipFilePath);
                await WaitForOutputFile(expectedOutputFile);
                Thread.Sleep(10);
                return File.ReadAllText(expectedOutputFile);
            }
            finally
            {
                if (File.Exists(zipFilePath))
                    File.Delete(zipFilePath);
                if (File.Exists(expectedOutputFile))
                    File.Delete(expectedOutputFile);
            }
        }

        private void DropToZip(ProgramSpecification program, string zipFilePath)
        {
            using (var zipFile = File.Create(zipFilePath))
            using (var zipStream = new System.IO.Compression.ZipArchive(zipFile, System.IO.Compression.ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zipStream.CreateEntry("Program.cs").Open()))
                {
                    writer.Write(program.Program);
                }
                if (!String.IsNullOrWhiteSpace(program.ProjectJSON))
                {
                    using (var writer = new StreamWriter(zipStream.CreateEntry("project.json").Open()))
                    {
                        writer.Write(program.ProjectJSON);
                    }
                }
            }
        }

        private Task WaitForOutputFile(string expectedOutputFile)
        {
            if (File.Exists(expectedOutputFile))
                return Task.FromResult(true);

            var tcs = new TaskCompletionSource<bool>();
            var ct = new CancellationTokenSource(10000);
            ct.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

            FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(expectedOutputFile));
            FileSystemEventHandler createdHandler = null;
            createdHandler = (s, e) =>
            {
                if (e.Name == Path.GetFileName(expectedOutputFile))
                {
                    tcs.TrySetResult(true);
                    watcher.Created -= createdHandler;
                    watcher.Dispose();
                }
            };
            watcher.Created += createdHandler;
            watcher.EnableRaisingEvents = true;

            return tcs.Task;
        }
    }
}
