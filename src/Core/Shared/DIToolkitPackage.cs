using System.Threading;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using System.Linq;
using System.ComponentModel.Design;
using System.Collections.Generic;
using System.IO.Packaging;

namespace Community.VisualStudio.Toolkit.DependencyInjection
{
    public interface IToolkitServiceProviderContainer
    {
        IServiceProvider Get<TPackage>() where TPackage : AsyncPackage;
        IServiceProvider Get(Type packageType);
    }

    public class ToolkitServiceProviderContainer : IToolkitServiceProviderContainer
    {
        private static Dictionary<Type, IServiceProvider> _serviceProviders = new Dictionary<Type, IServiceProvider>();
        internal static void AddServiceProvider<TPackage>(IServiceProvider serviceProvider) where TPackage : AsyncPackage
        {
            _serviceProviders.Add(typeof(TPackage), serviceProvider);
        }

        public IServiceProvider Get<TPackage>() where TPackage : AsyncPackage
        {
            return this.Get(typeof(TPackage));
        }

        public IServiceProvider Get(Type packageType)
        {
            return _serviceProviders[packageType];
        }
    }

    /// <summary>
    /// Package that contains a DI service container.
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    [ProvideService(typeof(SToolkitServiceProviderContainer), IsAsyncQueryable = true)]
    public abstract class DIToolkitPackage<TPackage> : DIToolkitPackage
        where TPackage : AsyncPackage
    {
        /// <summary>
        /// Initializes the <see cref="AsyncPackage"/>
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            IServiceCollection services = this.CreateServiceCollection();
            services.AddSingleton<DIToolkitPackage>(this); // Add the 'DIToolkitPackage' to the services
            services.AddSingleton<AsyncPackage>(this); // Add the 'AsyncPackage' to the services
            services.AddSingleton(this.GetType(), this); // Add the exact package type to the services as well.
            services.AddSingleton((IMenuCommandService)await this.GetServiceAsync(typeof(IMenuCommandService)));
            services.AddSingleton(typeof(CommandWrapper<>));

            // Initialize any services that the implementor desires.
            InitializeServices(services);

            IServiceProvider serviceProvider = BuildServiceProvider(services);
            this.ServiceProvider = serviceProvider;
            ToolkitServiceProviderContainer.AddServiceProvider<TPackage>(this.ServiceProvider);

            // Add the IToolkitServiceProvider to the VS IServiceProvider
            AsyncServiceCreatorCallback serviceCreatorCallback = (sc, ct, t) =>
            {
                if(t == typeof(SToolkitServiceProviderContainer))
                {
                    return Task.FromResult((object)new ToolkitServiceProviderContainer());
                }
                return Task.FromResult<object?>(null);
                
            };

            AddService(typeof(SToolkitServiceProviderContainer), serviceCreatorCallback, true);

            // Register any commands that were added to the DI container
            // Create a CommandWrapper for each command that was added to the container
            var commands = services
                .Where(x => typeof(BaseDICommand).IsAssignableFrom(x.ImplementationType))
                .ToList();

            await JoinableTaskFactory.SwitchToMainThreadAsync();

            foreach (var command in commands)
            {
                var baseCommandTypeGeneric = typeof(CommandWrapper<>).MakeGenericType(command.ImplementationType);

                // Retrieveing the command wrapper from the container will register the command with the 'IMenuCommandService'
                _ = serviceProvider.GetRequiredService(baseCommandTypeGeneric);
            }

            
        }
    }

    /// <summary>
    /// Package that contains a DI service container.
    /// </summary>
    public abstract class DIToolkitPackage : ToolkitPackage
    {
        /// <summary>
        /// Custom ServiceProvider for the package.
        /// </summary>
        public IServiceProvider ServiceProvider { get; protected set; } = null!; // This property is initialized in `InitializeAsync`, so it's never actually null.

        /// <summary>
        /// Create the service collection.
        /// </summary>
        /// <returns></returns>
        protected abstract IServiceCollection CreateServiceCollection();

        /// <summary>
        /// Builds the service collection
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        protected abstract IServiceProvider BuildServiceProvider(IServiceCollection serviceCollection);

        /// <summary>
        /// Initialize the services in the DI container.
        /// </summary>
        /// <param name="services"></param>
        protected virtual void InitializeServices(IServiceCollection services)
        {
            // Nothing
        }
    }
}
