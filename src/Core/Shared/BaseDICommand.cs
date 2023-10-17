using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Community.VisualStudio.Toolkit.DependencyInjection.Core
{
    /// <summary>
    /// Base class used for commands instantiated by the DI container.
    /// </summary>
    public abstract class BaseDICommand : BaseCommand
    {
        /// <summary>
        /// Constructor for the BaseDICommand
        /// </summary>
        /// <param name="package"></param>
        public BaseDICommand(DIToolkitPackage package)
        {
            this.Package = package;
            var commandWrapperType = typeof(CommandWrapper<>).MakeGenericType(this.GetType());
            var commandWrapper = package.ServiceProvider.GetRequiredService(commandWrapperType);
            var commandPropertyInfo = commandWrapperType.GetProperty(nameof(BaseCommand.Command));
            this.Command = (OleMenuCommand)commandPropertyInfo.GetValue(commandWrapper, null);
        }
    }

    public interface IToolWindowProvider
    {
        Type PaneType { get; }

        string GetTitle(int toolWindowId);

        Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken);
    }

    internal static class DIToolkitReflectionHelpers
    {
        public static bool IsDIToolkitPackage(Type type)
        {
            var baseType = type.BaseType;
            if (baseType == null)
            {
                return false;
            }
            if (baseType.IsGenericType)
            {
                var genericTypeDefinition = baseType.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(DIToolkitPackage<>))
                {
                    return true;
                }
            }
            return IsDIToolkitPackage(baseType);
        }

    }

    public abstract class BaseDIToolWindowRegistration<T, TToolWindowProvider> : BaseToolWindow<T> where T : BaseToolWindow<T>, new() where TToolWindowProvider : IToolWindowProvider
    {
        private readonly TToolWindowProvider toolWindowProvider;

        public BaseDIToolWindowRegistration()
        {
            static Type GetToolkitPackageType()
            {
                var stackTrace = new StackTrace();
                for (var i = 0; i < stackTrace.FrameCount; i++)
                {
                    var stackFrame = stackTrace.GetFrame(i);
                    var method = stackFrame.GetMethod();
                    var declaringType = method.DeclaringType;
                    var package = DIToolkitReflectionHelpers.IsDIToolkitPackage(declaringType);
                    if (package)
                    {
                        return declaringType;
                    }
                }
                return null;
            }
            var toolkitPackageType = GetToolkitPackageType();

#pragma warning disable VSTHRD104 // Offer async methods
            var serviceProvider = ThreadHelper.JoinableTaskFactory.Run(async () => {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var toolkitServiceProviderX = await VS.GetRequiredServiceAsync<SToolkitServiceProviderContainer, IToolkitServiceProviderContainer>();
                return toolkitServiceProviderX.Get(toolkitPackageType);
            });
#pragma warning restore VSTHRD104 // Offer async methods
            toolWindowProvider = (TToolWindowProvider)serviceProvider.GetRequiredService(typeof(TToolWindowProvider));
        }


        public override Type PaneType => toolWindowProvider.PaneType;

        public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            return toolWindowProvider.CreateAsync(toolWindowId, cancellationToken);
        }

        public override string GetTitle(int toolWindowId)
        {
            return toolWindowProvider.GetTitle(toolWindowId);
        }
    }

}
