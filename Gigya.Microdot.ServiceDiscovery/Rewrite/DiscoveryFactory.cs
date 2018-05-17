﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <inheritdoc />
    public class DiscoveryFactory : IDiscoveryFactory
    {
        private Func<DeploymentIdentifier, INodeSource, ReachabilityCheck, ILoadBalancer> CreateLoadBalancer { get; }
        private Func<DeploymentIdentifier, INodeSource[]> CreateNodeSources { get; }
        private Func<IConsulServiceListMonitor[]> CreateNodeSources { get; }
        private Func<DiscoveryConfig> GetConfig { get; }

        /// <inheritdoc />
        public DiscoveryFactory(Func<DiscoveryConfig> getConfig, Func<DeploymentIdentifier, INodeSource, ReachabilityCheck, ILoadBalancer> createLoadBalancer, Func<DeploymentIdentifier, INodeSource[]> createNodeSources)
        {
            GetConfig = getConfig;
            CreateNodeSources = createNodeSources;
            CreateLoadBalancer = createLoadBalancer;
        }

        /// <inheritdoc />
        public async Task<ILoadBalancer> TryCreateLoadBalancer(DeploymentIdentifier deploymentIdentifier, ReachabilityCheck reachabilityCheck)
        {
            INodeSource nodeSource = await TryCreateNodeSource(deploymentIdentifier).ConfigureAwait(false);
            if (nodeSource == null)
                return null;
            else
                return CreateLoadBalancer(deploymentIdentifier, nodeSource, reachabilityCheck);
        }

        /// <inheritdoc />
        public async Task<INodeSource> TryCreateNodeSource(DeploymentIdentifier deploymentIdentifier)
        {
            var nodeSource = CreateNodeSource(deploymentIdentifier);
            await nodeSource.Init().ConfigureAwait(false);
            bool specificEnvironmentNotSupported = !nodeSource.SupportsMultipleEnvironments && deploymentIdentifier.IsEnvironmentSpecific;// if nodeSource not supports multiple environments, only the last fallback environment will get a valid nodeSource

            if (nodeSource.WasUndeployed || specificEnvironmentNotSupported)
            {
                nodeSource.Shutdown();
                return null;
            }

            return nodeSource;        
        }

        private INodeSource CreateNodeSource(DeploymentIdentifier deploymentIdentifier)
        {
            var serviceConfig = GetConfig().Services[deploymentIdentifier.ServiceName];
            // Why create all sources and pick one? Can't we create just the needed one?
            // what if the source changes at runtime?
            var source = CreateNodeSources(deploymentIdentifier).FirstOrDefault(f => f.Type.Equals(serviceConfig.Source, StringComparison.InvariantCultureIgnoreCase));

            if (source == null)
                throw new ConfigurationException($"Discovery Source '{serviceConfig.Source}' is not supported.");

            return source;
        }
    }
}