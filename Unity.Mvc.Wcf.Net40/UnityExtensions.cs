using System;
using Microsoft.Practices.Unity;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Unity.Mvc.Wcf
{
    public static class UnityExtensions
    {
        /// <summary>
        /// Registers the given proxy pool manager with Unity for the given WCF contract.
        /// </summary>
        /// <typeparam name="TContract">The type of WCF contract for which to associate the manager.</typeparam>
        /// <param name="container">The Unity container with which to register the manager.</param>
        /// <param name="pool">The proxy pool manager associated with the WCF contract.</param>
        public static IUnityContainer RegisterWcfClientFor<TContract>(this IUnityContainer container, IProxyPool<TContract> pool)
        {
            return container.RegisterWcfClientFor(pool, null);
        }

        /// <summary>
        /// Registers the given proxy pool manager with Unity for the given WCF contract
        /// under a specific Unity name.
        /// </summary>
        /// <typeparam name="TContract">The type of WCF contract for which to associate the manager.</typeparam>
        /// <param name="container">The Unity container with which to register the manager.</param>
        /// <param name="pool">The proxy pool manager associated with the WCF contract.</param>
        /// <param name="name">The name under which to register the manager with Unity.</param>
        public static IUnityContainer RegisterWcfClientFor<TContract>(this IUnityContainer container, IProxyPool<TContract> pool, string name)
        {
            if (container == null) throw new ArgumentNullException("container");
            if (pool == null) throw new ArgumentNullException("pool");

            var t = typeof(TContract);
            return container
                .RegisterInstance(name, pool)
                .RegisterType(t, ProxyGen.GenerateProxy(t), name, new HierarchicalLifetimeManager());
        }

        /// <summary>
        /// Registers an UnlimitedProxyPool manager with Unity for the given WCF contract,
        /// using the given name of the endpoing configuration entry found in the application's
        /// Web.config file.
        /// </summary>
        /// <typeparam name="TContract">The type of WCF contract for which to associate the manager.</typeparam>
        /// <param name="container">The Unity container with which to register the manager.</param>
        /// <param name="endpointConfigurationName">The name of the endpoint configuration
        /// entry to use, defined in the application's Web.config file.</param>
        public static IUnityContainer RegisterWcfClientFor<TContract>(this IUnityContainer container, string endpointConfigurationName)
        {
            return container.RegisterWcfClientFor<TContract>(endpointConfigurationName, null);
        }

        /// <summary>
        /// Registers an UnlimitedProxyPool manager with Unity for the given WCF contract
        /// under the specified Unity name, using the given name of the endpoing configuration
        /// entry found in the application's Web.config file.
        /// </summary>
        /// <typeparam name="TContract">The type of WCF contract for which to associate the manager.</typeparam>
        /// <param name="container">The Unity container with which to register the manager.</param>
        /// <param name="endpointConfigurationName">The name of the endpoint configuration
        /// entry to use, defined in the application's Web.config file.</param>
        /// <param name="name">The name under which to register the manager with Unity.</param>
        public static IUnityContainer RegisterWcfClientFor<TContract>(this IUnityContainer container, string endpointConfigurationName, string name)
        {
            return container.RegisterWcfClientFor(new UnlimitedProxyPool<TContract>(endpointConfigurationName), name);
        }

        /// <summary>
        /// Registers an UnlimitedProxyPool manager with Unity for the given WCF contract
        /// under the specified Unity name, using the given binding and endpoint address.
        /// </summary>
        /// <typeparam name="TContract">The type of WCF contract for which to associate the manager.</typeparam>
        /// <param name="container">The Unity container with which to register the manager.</param>
        /// <param name="binding">The connection binding to the remote service.</param>
        /// <param name="address">The endpoint address of the remote service.</param>
        public static IUnityContainer RegisterWcfClientFor<TContract>(this IUnityContainer container, Binding binding, EndpointAddress address)
        {
            return container.RegisterWcfClientFor<TContract>(binding, address, null);
        }

        /// <summary>
        /// Registers an UnlimitedProxyPool manager with Unity for the given WCF contract
        /// under the specified Unity name, using the given binding and endpoint address.
        /// </summary>
        /// <typeparam name="TContract">The type of WCF contract for which to associate the manager.</typeparam>
        /// <param name="container">The Unity container with which to register the manager.</param>
        /// <param name="binding">The connection binding to the remote service.</param>
        /// <param name="address">The endpoint address of the remote service.</param>
        /// <param name="name">The name under which to register the manager with Unity.</param>
        public static IUnityContainer RegisterWcfClientFor<TContract>(this IUnityContainer container, Binding binding, EndpointAddress address, string name)
        {
            return container.RegisterWcfClientFor(new UnlimitedProxyPool<TContract>(binding, address), name);
        }
    }
}