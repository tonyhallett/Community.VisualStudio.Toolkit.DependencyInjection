﻿using System;
using System.Linq;
using System.Reflection;
using Community.VisualStudio.Toolkit.DependencyInjection.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Community.VisualStudio.Toolkit
{
    /// <summary>
    /// Provides extensions for the Community.VisualStudio.Toolkit.DependencyInjection package.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Registers all commands in the given assemblies. The commands MUST inherit from <see cref="BaseDICommand"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serviceLifetime">Generally you should register them with a lifetime of <see cref="ServiceLifetime.Singleton"/>. You can also register them with a lifetime of <see cref="ServiceLifetime.Scoped"/></param>
        /// <param name="assemblies">Assemblies to scan for commands that inherit from <see cref="BaseDICommand"/>. If none are provided, then the calling assembly is scanned.</param>
        /// <returns></returns>
        public static IServiceCollection RegisterCommands(this IServiceCollection services, ServiceLifetime serviceLifetime, params Assembly[] assemblies)
        {
            if (!(assemblies?.Any() ?? false))
                assemblies = new Assembly[] { Assembly.GetCallingAssembly() };

            foreach (var assembly in assemblies)
            {
                var commandTypes = assembly.GetTypes()
                    .Where(x => typeof(BaseDICommand).IsAssignableFrom(x));

                foreach (var commandType in commandTypes)
                    services.Add(new ServiceDescriptor(commandType, commandType, serviceLifetime));
            }

            return services;
        }

        private static readonly Type _registrationType = typeof(BaseDIToolWindowRegistration<,>);
        private static Type? GetToolWindowProviderType(Type derivedType)
        {
            if (derivedType == null) return null;

            var baseType = derivedType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType)
                {
                    var genericTypeDefinition = baseType.GetGenericTypeDefinition();
                    if (genericTypeDefinition == _registrationType)
                    {
                        return baseType.GenericTypeArguments[1];
                    }
                }
                baseType = baseType.BaseType;
            }
            return null;
        }

        public static IServiceCollection RegisterToolWindows(this IServiceCollection services, ServiceLifetime serviceLifetime, params Assembly[] assemblies)
        {
            if (!(assemblies?.Any() ?? false))
                assemblies = new Assembly[] { Assembly.GetCallingAssembly() };
            foreach (var assembly in assemblies)
            {
                var toolWindowProviderTypes = assembly.GetTypes().Select(t => GetToolWindowProviderType(t)).Where(t => t != null);


                foreach (var toolWindowProviderType in toolWindowProviderTypes)
                    services.Add(new ServiceDescriptor(toolWindowProviderType, toolWindowProviderType, serviceLifetime));
            }
            return services;

        }
    }
}
