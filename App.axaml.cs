using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
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
//     instance is returned to every caller — critical for shared state like IMessenger.
//
//   • You can swap implementations for testing.  In a test host you would register
//     a mock IMessenger; everything else stays the same.
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
            // MainWindowViewModel does not participate in IMessenger, so it is still
            // constructed directly here.  If it ever needs messaging, register it in
            // ConfigureServices() and retrieve it via Ioc.Default instead.
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
    ///   IMessenger:
    ///     Must be a singleton.  Both PointControlViewModel (publisher) and
    ///     TriangleAlphaDataViewModel (subscriber) must receive THE SAME messenger
    ///     instance.  If they each got a different instance, published messages
    ///     would have no subscribers to route to.
    ///
    ///   PointControlViewModel / TriangleAlphaDataViewModel:
    ///     Must also be singletons because:
    ///       (a) Each corresponds to exactly one View in the window — there is no
    ///           scenario where you want two independent copies.
    ///       (b) TriangleAlphaDataViewModel registers with the messenger in its
    ///           constructor.  If it were Transient, every call to GetRequiredService
    ///           would create a new instance that registers again, and old instances
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

                // ── IMessenger ────────────────────────────────────────────────────
                // WeakReferenceMessenger.Default is the global process-wide messenger.
                // Registering it as a singleton means every consumer that asks for
                // IMessenger gets the same WeakReferenceMessenger.Default instance.
                //
                // WHY WeakReferenceMessenger over StrongReferenceMessenger:
                //   WeakReferenceMessenger stores recipients with weak references, so
                //   a ViewModel that goes out of scope can be garbage-collected even
                //   while still "registered."  StrongReferenceMessenger would keep it
                //   alive indefinitely — a memory leak in longer-lived applications.
                .AddSingleton<IMessenger>(WeakReferenceMessenger.Default)

                // ── PointControlViewModel ─────────────────────────────────────────
                // PUBLISHER: calls CommitPosition() → _messenger.Send(DotReleasedMessage)
                // Receives: IMessenger (injected from above registration).
                // The container injects IMessenger automatically because the constructor
                // declares it as a parameter.  This is constructor injection.
                .AddSingleton<PointControlViewModel>()

                // ── TriangleAlphaDataViewModel ────────────────────────────────────
                // SUBSCRIBER: registers for DotReleasedMessage in its constructor.
                // Receives: IMessenger (same singleton instance as PointControlViewModel).
                // The shared IMessenger is what makes the message route between them.
                .AddSingleton<TriangleAlphaDataViewModel>()

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
