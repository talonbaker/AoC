using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using SquareClickerPointer.EventBuses;
using SquareClickerPointer.ViewModels;
using SquareClickerPointer.Views;
using System.Linq;

namespace SquareClickerPointer;

// ═══════════════════════════════════════════════════════════════════════════════
//  App  —  application entry point and dependency injection composition root
// ═══════════════════════════════════════════════════════════════════════════════
//
//  COMPOSITION ROOT
//  ────────────────
//  In a DI-based application there is exactly one place where you wire all the
//  dependencies together: the "Composition Root".  In this app that is the
//  ConfigureServices() call below.
//
//  WHY one central location (not scattered new() calls)?
//
//   • Every dependency relationship is visible in one file.  If PointControlViewModel
//     gains a new constructor parameter, you add one line here and nothing else changes.
//
//   • The container enforces lifetime rules.  Registering as Singleton means the same
//     instance is returned to every caller — critical for shared state like the event
//     buses below (publisher and subscriber must share the same bus instance).
//
//   • You can swap implementations for testing.  In a test host you would register
//     a test-only event bus instance; everything else stays the same.
//
//  ORDER OF OPERATIONS (important — do not reorder)
//  ─────────────────────────────────────────────────
//    1. ConfigureServices()  — registers all services in Ioc.Default.
//    2. new MainWindow()     — constructor calls Ioc.Default.GetRequiredService<...>()
//                             to get ViewModels.  Services must be registered first.
//
//  If you call new MainWindow() before ConfigureServices(), GetRequiredService throws
//  InvalidOperationException because the container has not been built yet.

public partial class App : Application
{
    public override void Initialize()
    {
        // Loads App.axaml (resources, styles, ViewLocator template).
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ── Step 1: Build the DI container BEFORE creating any Views ─────────
            //
            // Views resolve their ViewModels from Ioc.Default in their constructors.
            // The container must be configured before any View is instantiated.
            ConfigureServices();

            // Avoid duplicate validations from both Avalonia and CommunityToolkit.
            // See: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // ── Step 2: Create the main window ───────────────────────────────────
            //
            // MainWindowViewModel does not participate in any event bus, so it is
            // still constructed directly here.  If it ever needs cross-VM events,
            // register it in ConfigureServices() and retrieve it via Ioc.Default.
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Registers all application services and ViewModels with the IoC container.
    ///
    /// LIFETIME CHOICES EXPLAINED
    /// ──────────────────────────
    ///
    /// AddSingleton — one instance shared by everyone who asks for it.
    ///
    ///   Event buses (DotPositionEventBus, ItemColorEventBus, …):
    ///     Must be singletons.  A publisher and its subscribers must receive THE SAME
    ///     bus instance — if they each got a different bus, raised events would have
    ///     no subscribers to invoke.
    ///
    ///   PointControlViewModel / TriangleAlphaDataViewModel:
    ///     Must also be singletons because:
    ///       (a) Each corresponds to exactly one View in the window — there is no
    ///           scenario where you want two independent copies.
    ///       (b) TriangleAlphaDataViewModel subscribes to an event bus in its
    ///           constructor.  If it were Transient, every call to GetRequiredService
    ///           would create a new instance that subscribes again, and old instances
    ///           (still alive in the View) would accumulate stale subscriptions.
    ///
    /// AddTransient — a new instance per request (use for stateless services).
    ///   Not used here, but would be appropriate for, e.g., a formatting helper
    ///   with no state that multiple ViewModels can each own privately.
    ///
    /// AddScoped — one instance per "scope" (request in web apps).
    ///   Not applicable in a desktop application.
    /// </summary>
    private static void ConfigureServices()
    {
        Ioc.Default.ConfigureServices(
            new ServiceCollection()

                // ── Event buses ──────────────────────────────────────────────────
                // Each bus carries one specific event type using the standard .NET
                // EventHandler<TEventArgs> pattern.  Registering as singletons means
                // every consumer (publisher or subscriber) receives the same instance,
                // which is what allows raised events to reach their handlers.
                .AddSingleton<DotPositionEventBus>()
                .AddSingleton<ItemColorEventBus>()
                .AddSingleton<ContainerEventBus>()
                .AddSingleton<ItemLockEventBus>()
                .AddSingleton<ItemShapeEventBus>()

                // ── PointControlViewModel ─────────────────────────────────────────
                // PUBLISHER: calls CommitPosition() → DotPositionEventBus.Publish(...)
                // Receives: DotPositionEventBus (the singleton registered above).
                .AddSingleton<PointControlViewModel>()

                // ── TriangleAlphaDataViewModel ────────────────────────────────────
                // SUBSCRIBER: subscribes to DotPositionEventBus.DotReleased in its
                // constructor and unsubscribes in Dispose.
                .AddSingleton<TriangleAlphaDataViewModel>()

                // ── ItemListViewModel ─────────────────────────────────────────────
                // TRANSIENT: each ExpandableContainerViewModel owns one private list.
                // Transient means "create a new instance for every caller."
                // When DI constructs an ExpandableContainerViewModel, it automatically
                // creates a fresh ItemListViewModel and injects it via the constructor.
                // Container 1's list never shares state with Container 2's list.
                .AddTransient<ItemListViewModel>()

                // ── ExpandableContainerViewModel ──────────────────────────────────
                // TRANSIENT: there are multiple containers in the window (Container 1,
                // 2, 3…), each with its own title, items, and expanded/collapsed state.
                //
                // DI automatically injects:
                //   ContainerEventBus   → the shared singleton (for accordion pub/sub)
                //   ItemColorEventBus   → singleton (forwarded to ListItemViewModels)
                //   ItemLockEventBus    → singleton (forwarded to ListItemViewModels)
                //   ItemShapeEventBus   → singleton (forwarded to ListItemViewModels)
                //   ItemListViewModel   → a fresh transient instance (per-container list)
                .AddTransient<ExpandableContainerViewModel>()

                .BuildServiceProvider()
        );
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var pluginsToRemove = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>()
            .ToArray();

        foreach (var plugin in pluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
