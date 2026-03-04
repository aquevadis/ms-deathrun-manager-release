using System;
using System.IO;
using DeathrunManager.Config;
using DeathrunManager.Interfaces;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Managers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using DeathrunManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Abstractions;

namespace DeathrunManager;

public class DeathrunManager : IModSharpModule, IDeathrunManager
{
    public string DisplayName         => $"DeathrunManager - Last Build Time: {Bridge.FileTime}";
    public string DisplayAuthor       => "AquaVadis";
    
    private readonly ServiceProvider  _serviceProvider;
    private static ISharedSystem      _sharedSystem = null!;
    public static ISharedSystem SharedSystem => _sharedSystem;
    
    public static IModSharpModuleInterface<IDeathrunManager>? Api;

#pragma warning disable CA2211

    public static string ModulePath                 = "";
    public static ILogger<DeathrunManager> Logger   = null!;
    public static InterfaceBridge Bridge            = null!;
    public static DeathrunManager Instance          = null!;
    
#pragma warning restore CA2211
    
    public DeathrunManager(ISharedSystem sharedSystem,
        string                   dllPath,
        string                   sharpPath,
        Version                  version,
        IConfiguration           coreConfiguration,
        bool                     hotReload)
    {
        ModulePath = dllPath;
        Bridge = new InterfaceBridge(dllPath, sharpPath, version, sharedSystem);
        Instance = this;
        Logger = sharedSystem.GetLoggerFactory().CreateLogger<DeathrunManager>();
        _sharedSystem = sharedSystem;
        
        var configuration = new ConfigurationBuilder()
                                .AddJsonFile(Path.Combine(dllPath, "base.json"), true, false)
                                .Build();
        
        var services = new ServiceCollection();

        services.AddSingleton(Bridge);
        services.AddSingleton(Bridge.ClientManager);
        services.AddSingleton(sharedSystem);
        services.AddSingleton(sharedSystem.GetConVarManager());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(sharedSystem.GetLoggerFactory());
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));
        
        //load base config
        DeathrunManagerConfig.LoadDeathrunManagerConfig();
        
        services.AddManagers();
        _serviceProvider = services.BuildServiceProvider();
    }

    #region IModule
    
    public bool Init()
    {
        Logger.LogInformation("[DeathrunManager] {colorMessage}", "Load DeathrunManager!");
        
        //load managers
        CallInit<IManager>();
        return true;
    }

    public void PostInit() { CallPostInit<IManager>(); }

    public void Shutdown()
    {
        CallShutdown<IManager>();

        _serviceProvider.ShutdownAllSharpExtensions();
        
        Logger.LogInformation("[DeathrunManager] {colorMessage}", "Unloaded DeathrunManager!");
    }

    public void OnAllModulesLoaded()
    {
        //expose shared interface
        Bridge.SharpModuleManager.RegisterSharpModuleInterface<IDeathrunManager>(Instance, IDeathrunManager.Identity, this);
        
        Api = Bridge.SharpModuleManager.GetOptionalSharpModuleInterface<IDeathrunManager>(IDeathrunManager.Identity);

        if (Api?.Instance is { } deathrunManagerApi)
        {
            // deathrunManagerApi.Events.OnSetRoundState += OnSetRoundStateHandler;
        }

        CallOnAllSharpModulesLoaded<IManager>();
    }

    public void OnLibraryConnected(string name) { }

    public void OnLibraryDisconnect(string name) { }
    
    #endregion
    
    #region Injected Instances' Caller methods
    
    private int CallInit<T>() where T : IBaseInterface
    {
        var init = 0;

        foreach (var service in _serviceProvider.GetServices<T>())
        {
            if (!service.Init())
            {
                Logger.LogError("Failed to Init {service}!", service.GetType().FullName);

                return -1;
            }

            init++;
        }

        return init;
    }

    private void CallPostInit<T>() where T : IBaseInterface
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            try
            {
                service.OnPostInit();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "An error occurred while calling PostInit in {m}", service.GetType().Name);
            }
        }
    }

    private void CallShutdown<T>() where T : IBaseInterface
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            try
            {
                service.Shutdown();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "An error occurred while calling Shutdown in {m}", service.GetType().Name);
            }
        }
    }

    private void CallOnAllSharpModulesLoaded<T>() where T : IBaseInterface
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            try
            {
                service.OnAllSharpModulesLoaded();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "An error occurred while calling OnAllSharpModulesLoaded in {m}", service.GetType().Name);
            }
        }
    }

    #endregion
}