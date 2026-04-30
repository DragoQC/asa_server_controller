Sudo required error. you forgot some things in the new IP table and other things that was just added. we need toa djust the sudo requirements before pushing that
fail: Microsoft.Extensions.Hosting.Internal.Host[9]
      BackgroundService failed
      System.InvalidOperationException: sudo: a password is required
         at asa_server_controller.Services.SudoService.RunProcessAsync(String fileName, IReadOnlyList`1 arguments, CancellationToken cancellationToken, Boolean throwOnNonZero) in /home/drago/Git/ASA_Server_Manager_Control/asa_server_controller/Services/SudoService.cs:line 232
         at asa_server_controller.Services.SudoService.ApplyGamePortForwardingRulesAsync(IReadOnlyList`1 rules, CancellationToken cancellationToken) in /home/drago/Git/ASA_Server_Manager_Control/asa_server_controller/Services/SudoService.cs:line 102
         at asa_server_controller.Services.GamePortForwardingService.SynchronizeCoreAsync(CancellationToken cancellationToken) in /home/drago/Git/ASA_Server_Manager_Control/asa_server_controller/Services/GamePortForwardingService.cs:line 98
         at asa_server_controller.Services.GamePortForwardingService.ExecuteAsync(CancellationToken stoppingToken) in /home/drago/Git/ASA_Server_Manager_Control/asa_server_controller/Services/GamePortForwardingService.cs:line 20
         at Microsoft.Extensions.Hosting.Internal.Host.TryExecuteBackgroundServiceAsync(BackgroundService backgroundService)
crit: Microsoft.Extensions.Hosting.Internal.Host[10]
      The HostOptions.BackgroundServiceExceptionBehavior is configured to StopHost. A BackgroundService has thrown an unhandled exception, and the IHost instance is stopping. To avoid this behavior, configure this to Ignore; however the BackgroundService will not be restarted.
      System.InvalidOperationException: sudo: a password is required
         at asa_server_controller.Services.SudoService.RunProcessAsync(String fileName, IReadOnlyList`1 arguments, CancellationToken cancellationToken, Boolean throwOnNonZero) in /home/drago/Git/ASA_Server_Manager_Control/asa_server_controller/Services/SudoService.cs:line 232
         at asa_server_controller.Services.SudoService.ApplyGamePortForwardingRulesAsync(IReadOnlyList`1 rules, CancellationToken cancellationToken) in /home/drago/Git/ASA_Server_Manager_Control/asa_server_controller/Services/SudoService.cs:line 102
         at asa_server_controller.Services.GamePortForwardingService.SynchronizeCoreAsync(CancellationToken cancellationToken) in /home/drago/Git/ASA_Server_Manager_Control/asa_server_controller/Services/GamePortForwardingService.cs:line 98
         at asa_server_controller.Services.GamePortForwardingService.ExecuteAsync(CancellationToken stoppingToken) in /home/drago/Git/ASA_Server_Manager_Control/asa_server_controller/Services/GamePortForwardingService.cs:line 20
         at Microsoft.Extensions.Hosting.Internal.Host.TryExecuteBackgroundServiceAsync(BackgroundService backgroundService)
info: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
fail: Microsoft.Extensions.Hosting.Internal.Host[11]
      Hosting failed to start
      System.Threading.Tasks.TaskCanceledException: A task was canceled.
         at Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerImpl.BindAsync(CancellationToken cancellationToken)
         at Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerImpl.StartAsync[TContext](IHttpApplication`1 application, CancellationToken cancellationToken)
         at Microsoft.AspNetCore.Hosting.GenericWebHostService.StartAsync(CancellationToken cancellationToken)
         at Microsoft.Extensions.Hosting.Internal.Host.<StartAsync>b__14_1(IHostedService service, CancellationToken token)
         at Microsoft.Extensions.Hosting.Internal.Host.ForeachService[T](IEnumerable`1 services, CancellationToken token, Boolean concurrent, Boolean abortOnFirstException, List`1 exceptions, Func`3 operation)
Unhandled exception. System.Threading.Tasks.TaskCanceledException: A task was canceled.
   at Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerImpl.BindAsync(CancellationToken cancellationToken)
   at Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerImpl.StartAsync[TContext](IHttpApplication`1 application, CancellationToken cancellationToken)
   at Microsoft.AspNetCore.Hosting.GenericWebHostService.StartAsync(CancellationToken cancellationToken)
   at Microsoft.Extensions.Hosting.Internal.Host.<StartAsync>b__14_1(IHostedService service, CancellationToken token)
   at Microsoft.Extensions.Hosting.Internal.Host.ForeachService[T](IEnumerable`1 services, CancellationToken token, Boolean concurrent, Boolean abortOnFirstException, List`1 exceptions, Func`3 operation)
   at Microsoft.Extensions.Hosting.Internal.Host.StartAsync(CancellationToken cancellationToken)
   at Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions.RunAsync(IHost host, CancellationToken token)
   at Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions.RunAsync(IHost host, CancellationToken token)
   at Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions.Run(IHost host)
   at Program.<Main>$(String[] args) in /home/drago/Git/ASA_Server_Manager_Control/asa_server_controller/Program.cs:line 128
   at Program.<Main>(String[] args)