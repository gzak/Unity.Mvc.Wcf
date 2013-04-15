using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.Unity;

namespace Unity.Mvc.Wcf
{
    /// <summary>
    /// Defines the interface for managing a pool of
    /// smart proxies of the specified WCF contract type.
    /// </summary>
    /// <typeparam name="TContract">The WCF contract type.</typeparam>
    public interface IProxyPool<TContract>
    {
        /// <summary>
        /// Requests a smart proxy from this manager.
        /// </summary>
        /// <returns>A smart proxy instance.</returns>
        TContract RequestProxy();

        /// <summary>
        /// Releases a smart proxy to this manager.
        /// </summary>
        /// <param name="proxy">The smart proxy instance to release.</param>
        bool ReleaseProxy(TContract proxy);
    }

    /// <summary>
    /// Represents an unlimited pool of smart proxies to the target service,
    /// meaning a new connection is established for each call to RequestProxy.
    /// </summary>
    /// <typeparam name="TContract">The type of the service contract.</typeparam>
    public sealed class UnlimitedProxyPool<TContract> : IProxyPool<TContract>, IDisposable
    {
        private ChannelFactory<TContract> fact;
        private HashSet<TContract> connections = new HashSet<TContract>();
        private ReaderWriterLockSlimWrapper locker = new ReaderWriterLockSlimWrapper();

        /// <summary>
        /// Initializes a new instance of the Unity.Mvc.Wcf.UnlimitedProxyPool&lt;TContract&gt;
        /// class which generates proxies according to the given endpoint configuration name
        /// found in the application's Web.config file.
        /// </summary>
        /// <param name="endpointConfigurationName">The name of the endpoint configuration defined in the application's Web.config file.</param>
        public UnlimitedProxyPool(string endpointConfigurationName)
        {
            fact = new ChannelFactory<TContract>(endpointConfigurationName);
        }

        /// <summary>
        /// Initializes a new instance of the Unity.Mvc.Wcf.UnlimitedProxyPool&lt;TContract&gt;
        /// class which generates proxies according to the given binding and endpoint address.
        /// </summary>
        /// <param name="binding">The connection binding to the remote service.</param>
        /// <param name="address">The endpoint address of the remote service.</param>
        public UnlimitedProxyPool(Binding  binding, EndpointAddress address)
        {
            fact = new ChannelFactory<TContract>(binding, address);
        }

        /// <summary>
        /// Requests a smart proxy from this manager.
        /// </summary>
        /// <returns>A smart proxy instance.</returns>
        public TContract RequestProxy()
        {
            var chan = fact.CreateChannel();
            using (locker.GetWriteLock())
                connections.Add(chan);
            return chan;
        }

        /// <summary>
        /// Releases a smart proxy to this manager.
        /// </summary>
        /// <param name="channel">The smart proxy instance to release.</param>
        public bool ReleaseProxy(TContract channel)
        {
            using (locker.GetUpgradeableReadLock())
            {
                if (connections.Contains(channel))
                {
                    (channel as IDisposable).Dispose();
                    using (locker.GetWriteLock())
                        connections.Remove(channel);
                    return true;
                }
                else
                    return false;
            }
        }

        /// <summary>
        /// Releases all resources used by the current instance of the
        /// Unity.Mvc.Wcf.UnlimitedProxyPool class.
        /// </summary>
        public void Dispose()
        {
            locker.Dispose();
            foreach (var chan in connections)
                (chan as IDisposable).Dispose();
            (fact as IDisposable).Dispose();
        }
    }

    /// <summary>
    /// Represents a limited manager for smart proxies to the target service,
    /// meaning a new connection is established for each call to RequestProxy
    /// up to the specified limit. Further calls block until an existing
    /// connection is released.
    /// </summary>
    /// <typeparam name="TContract">The type of the service contract.</typeparam>
    public sealed class LimitedProxyPool<TContract> : IProxyPool<TContract>, IDisposable
    {
        private UnlimitedProxyPool<TContract> pool;
        private Semaphore semaphore;

        private LimitedProxyPool(int limit, UnlimitedProxyPool<TContract> pool)
        {
            semaphore = new Semaphore(limit, limit);
            this.pool = pool;
        }

        /// <summary>
        /// Initializes a new instance of the Unity.Mvc.Wcf.LimitedProxyPool&lt;TContract&gt;
        /// class which generates a new proxy up to the specified "<paramref name="limit"/>"
        /// according to the given endpoint configuration name found in the application's
        /// Web.config file.
        /// </summary>
        /// <param name="limit">The maximum number of simultaneous connections this pool allows.</param>
        /// <param name="endpointConfigurationName">The name of the endpoint configuration defined in the application's Web.config file.</param>
        public LimitedProxyPool(int limit, string endpointConfigurationName)
            : this(limit, new UnlimitedProxyPool<TContract>(endpointConfigurationName))
        {
        }

        /// <summary>
        /// Initializes a new instance of the Unity.Mvc.Wcf.LimitedProxyPool&lt;TContract&gt;
        /// class which generates a new proxy up to the specified "<paramref name="limit"/>"
        /// according to the given binding and endpoint address.
        /// </summary>
        /// <param name="limit">The maximum number of simultaneous connections this pool allows.</param>
        /// <param name="binding">The connection binding to the remote service.</param>
        /// <param name="address">The endpoint address of the remote service.</param>
        public LimitedProxyPool(int limit, Binding binding, EndpointAddress address)
            : this(limit, new UnlimitedProxyPool<TContract>(binding, address))
        {
        }

        /// <summary>
        /// Requests a smart proxy from this manager.
        /// </summary>
        /// <returns>A smart proxy instance.</returns>
        public TContract RequestProxy()
        {
            semaphore.WaitOne();
            try
            {
                return pool.RequestProxy();
            }
            catch
            {
                semaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// Releases a smart proxy to this manager.
        /// </summary>
        /// <param name="channel">The smart proxy instance to release.</param>
        public bool ReleaseProxy(TContract channel)
        {
            if (pool.ReleaseProxy(channel))
            {
                semaphore.Release(); // release only when successful.
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Releases all resources used by the current instance of the
        /// Unity.Mvc.Wcf.LimitedProxyPool class.
        /// </summary>
        public void Dispose()
        {
            semaphore.Dispose();
            pool.Dispose();
        }
    }

    /// <summary>
    /// Preestablishes connections up to the specified count, and reuses
    /// them per injection. Injections past the specified count block until
    /// an existing connection is released.
    /// </summary>
    /// <typeparam name="TContract">The type of the service contract.</typeparam>
    public sealed class PersistentClientPool<TContract> : IProxyPool<TContract>, IDisposable
    {
        private Semaphore semaphore;
        private ChannelFactory<TContract> factory;
        private HashSet<TContract> availableChannels;
        private HashSet<TContract> allChannels;

        private PersistentClientPool(int count, ChannelFactory<TContract> fact)
        {
            try
            {
                factory = fact;
                semaphore = new Semaphore(count, count);
                availableChannels = new HashSet<TContract>();
                allChannels = new HashSet<TContract>();
                for (int i = 0; i < count; i++)
                {
                    var channel = fact.CreateChannel();
                    availableChannels.Add(channel);
                    allChannels.Add(channel);
                }
            }
            catch (Exception e)
            {
                throw new WcfConnectionException("Could not establish connection to WCF service.", e);
            }
        }

        /// <summary>
        /// Initializes a new instance of the Unity.Mvc.Wcf&lt;TContract&gt;
        /// class which reuses one of the existing "<paramref name="count"/>"
        /// proxy instances generated according to the given endpoint
        /// configuration name (or the full name of the TContract type if
        /// null) found in the application's Web.config file.
        /// </summary>
        /// <param name="count">The maximum number of connections this pool allows.</param>
        /// <param name="endpointConfigurationName">The name of the endpoint configuration defined in the application's Web.config file.</param>
        public PersistentClientPool(int count, string endpointConfigurationName)
            : this(count, new ChannelFactory<TContract>(endpointConfigurationName))
        {
        }

        /// <summary>
        /// Initializes a new instance of the Unity.Mvc.Wcf&lt;TContract&gt;
        /// class which reuses one of the existing "<paramref name="count"/>"
        /// proxy instances generated according to the given binding and
        /// endpoint address.
        /// </summary>
        /// <param name="count">The maximum number of connections this pool allows.</param>
        /// <param name="binding">The connection binding to the remote service.</param>
        /// <param name="address">The endpoint address of the remote service.</param>
        public PersistentClientPool(int count, Binding binding, EndpointAddress address)
            : this(count, new ChannelFactory<TContract>(binding, address))
        {
        }

        /// <summary>
        /// Requests a smart proxy from this manager.
        /// </summary>
        /// <returns>A smart proxy instance.</returns>
        public TContract RequestProxy()
        {
            semaphore.WaitOne();
            try
            {
                // empty queue blocks until something is in the queue,
                // no need to check for empty queue.
                lock (availableChannels)
                {
                    var first = availableChannels.First();
                    availableChannels.Remove(first);
                    return first;
                }
            }
            catch
            {
                semaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// Releases a smart proxy to this manager.
        /// </summary>
        /// <param name="channel">The smart proxy instance to release.</param>
        public bool ReleaseProxy(TContract channel)
        {
            if (allChannels.Contains(channel) && !availableChannels.Contains(channel))
            {
                lock (availableChannels)
                    availableChannels.Add(channel);
                semaphore.Release(); // again, only release on success rather than in finally
                return true;
            }
            else
                return false;
        }

        public void Dispose()
        {
            // only ever called once, no need for locking
            semaphore.Dispose();
            foreach (var channel in allChannels)
                (channel as IDisposable).Dispose();
        }
    }
}