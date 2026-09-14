// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace EvolveOS_ShellEnhancer.Utilities
{
    public static class IpcServerManager
    {
        private const string PipeName = "EvolveOS_ShellPipe";
        private static CancellationTokenSource? _cts;

        public static event Action<string, string>? CommandReceived;

        public static void StartListening()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ServerLoop(_cts.Token));
        }

        public static void StopListening()
        {
            _cts?.Cancel();
        }

        private static async Task ServerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipeServer = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipeServer.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(pipeServer);
                    string? message = await reader.ReadLineAsync();

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        ProcessMessage(message);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"IPC Server Error: {ex.Message}");
                }
            }
        }

        private static void ProcessMessage(string message)
        {
            var parts = message.Split(':', 2);
            if (parts.Length == 2)
            {
                CommandReceived?.Invoke(parts[0], parts[1]);
            }
        }
    }
}