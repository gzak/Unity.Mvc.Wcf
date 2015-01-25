using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Mvc.Wcf
{
    internal static class InterfaceAggregator
    {
        /// <summary>
        /// Gets a collection containing the given interface and all its inherited interfaces, without duplicates.
        /// </summary>
        /// <param name="interfaceType">The interface to process.</param>
        /// <returns>A collection of interfaces.</returns>
        public static IEnumerable<Type> GetAllInterfaces(Type interfaceType)
        {
            return GetAllInterfacesInternal(interfaceType).Distinct();
        }

        /// <summary>
        /// Recursively gets a collection containing the given interface and all its inherited interfaces.
        /// </summary>
        /// <param name="interfaceType">The interface to recursively process.</param>
        /// <returns>A collection of interfaces.</returns>
        private static IEnumerable<Type> GetAllInterfacesInternal(Type interfaceType)
        {
            yield return interfaceType;
            foreach (var i in interfaceType.GetInterfaces().SelectMany(p => GetAllInterfacesInternal(p)))
                yield return i;
        }
    }
}