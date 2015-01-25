using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using Microsoft.Practices.Unity;
using ISyncCollection = System.Collections.ICollection;

namespace Unity.Mvc.Wcf
{
    /// <summary>
    /// Provides a static method to generate a smart proxy for a given
    /// WCF contract type.
    /// </summary>
    internal static class ProxyGen
    {
        // the in-memory assembly which will store all the generated smart proxies
        private static readonly AssemblyBuilder _dynamicAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("Unity.Mvc.Wcf._Dynamic_"), AssemblyBuilderAccess.Run);

        // the in-memory module which will store all the generated smart proxies
        private static readonly ModuleBuilder _dynamicModule = _dynamicAssembly.DefineDynamicModule("Unity.Mvc.Wcf._Dynamic_.dll");

        // for adding [CompilerGenerated] to the generated proxy's private fields
        private static readonly CustomAttributeBuilder _compGen = new CustomAttributeBuilder(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes), new object[] { });

        // applies [InjectionConstructor] to the generated proxy's sole constructor
        private static readonly CustomAttributeBuilder _resolveConstructor = new CustomAttributeBuilder(typeof(InjectionConstructorAttribute).GetConstructor(Type.EmptyTypes), new object[] { });

        // keeps track of the number of times a specific WCF contract name has been seen
        private static Dictionary<string, int> _names = new Dictionary<string, int>();

        // cache of generated proxies for a given WCF contract type
        private static Dictionary<Type, Type> _dynamic = new Dictionary<Type, Type>();

        // method attributes
        private const MethodAttributes methAttr = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;
        private const MethodAttributes propAttr = methAttr | MethodAttributes.SpecialName;

        /// <summary>
        /// Generates a smart proxy for the given WCF contract type.
        /// </summary>
        /// <param name="tContract">The type of WCF contract for which to generate a smart proxy.</param>
        /// <returns>A smart proxy of the given type.</returns>
        internal static Type GenerateProxy(Type tContract)
        {
            lock ((_dynamic as ISyncCollection).SyncRoot)
            {
                Type proxy;
                if (_dynamic.TryGetValue(tContract, out proxy))
                    return proxy; // cache previously generated proxies
                else if (tContract.IsInterface && !tContract.IsGenericTypeDefinition)
                {
                    var interfaces = InterfaceAggregator.GetAllInterfaces(tContract).ToList();
                    if (AllHaveServiceContractAttributes(interfaces))
                    {
                        var meths = GetAllMethods(interfaces).ToList();
                        // in case a different contract has the same name as another registered contract
                        // e.g.: Namespace1.IService and Namespace2.IService... same names, different contracts
                        int nameCount;
                        _names.TryGetValue(tContract.Name, out nameCount);
                        var name = string.Format("Unity.Mvc.Wcf._Dynamic_.{0}_Proxy_{1}", tContract.Name, nameCount);
                        _names[tContract.Name] = nameCount + 1;

                        // new type which implements the contract and IDisposable
                        var builder = _dynamicModule.DefineType(name, TypeAttributes.Class | TypeAttributes.AutoLayout | TypeAttributes.Public | TypeAttributes.Sealed);
                        builder.AddInterfaceImplementation(tContract);
                        builder.AddInterfaceImplementation(typeof(IDisposable));

                        // to store the WCF-generated proxy (the real proxy)
                        var client = builder.DefineField("client", tContract, FieldAttributes.Private);
                        client.SetCustomAttribute(_compGen);

                        // to store the client proxy pool manager
                        var pool = builder.DefineField("pool", typeof(IProxyPool<>).MakeGenericType(new Type[] { tContract }), FieldAttributes.Private);
                        pool.SetCustomAttribute(_compGen);

                        // implement contract methods by simply wrapping calls to the real proxy
                        foreach (var meth in meths)
                            MethodBuilderHelper(builder, client, meth);

                        // implement contract properties by simply wrapping calls to the real proxy
                        foreach (var prop in GetAllProperties(interfaces))
                            PropertyBuilderHelper(builder, client, prop);

                        // implement Dispose method to call the connection pool manager to release the connection
                        DisposeHelper(builder, client, pool);

                        // generate a constructor which initializes the real proxy by requesting a connection from the connection pool manager
                        ConstructorHelper(builder, client, pool);

                        // cache and return
                        return _dynamic[tContract] = builder.CreateType();
                    }
                }

                throw new InvalidOperationException(string.Format("{0} is not a valid or supported WCF service contract interface.", tContract.FullName));
            }
        }

        /// <summary>
        /// Generates a constructor which, given a proxy pool manager, automatically
        /// requests a smart proxy from the manager by calling RequestProxy&lt;TClient&gt;()
        /// and stores it in the "client" field, and also stores the pool manager itself in
        /// the "pool" field.
        /// </summary>
        /// <param name="builder">The type builder for which to generate the constructor.</param>
        /// <param name="client">The WCF contract field.</param>
        /// <param name="pool">The proxy pool manager field.</param>
        private static void ConstructorHelper(TypeBuilder builder, FieldBuilder client, FieldBuilder pool)
        {
            var cons = builder.DefineConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig, CallingConventions.HasThis, new Type[] { pool.FieldType });
            cons.SetCustomAttribute(_resolveConstructor);
            cons.DefineParameter(1, ParameterAttributes.None, "pool"); // Unity 2.1 requires the param to be named, may be omitted with Unity 3+
            var body = cons.GetILGenerator();
            body.LoadThis();
            body.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            body.LoadThis();
            body.LoadArg(0);
            body.Emit(OpCodes.Stfld, pool);
            body.LoadThis();
            body.LoadArg(0);
            body.Emit(OpCodes.Callvirt, pool.FieldType.GetMethod("RequestProxy", BindingFlags.Instance | BindingFlags.Public));
            body.Emit(OpCodes.Stfld, client);
            body.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Generates a Dispose method which simply calls the ReleaseProxy() method
        /// of the "pool" field, passing in the "client" field to release.
        /// </summary>
        /// <param name="builder">The type builder for which to generate the Dispose method.</param>
        /// <param name="client">The WCF contract field.</param>
        /// <param name="pool">The proxy pool manager field.</param>
        private static void DisposeHelper(TypeBuilder builder, FieldBuilder client, FieldBuilder pool)
        {
            var disposeBuilder = builder.DefineMethod("Dispose", methAttr, typeof(void), Type.EmptyTypes);
            var body = disposeBuilder.GetILGenerator();
            body.LoadThis();
            body.Emit(OpCodes.Ldfld, pool);
            body.LoadThis();
            body.Emit(OpCodes.Ldfld, client);
            body.Emit(OpCodes.Callvirt, pool.FieldType.GetMethod("ReleaseProxy", BindingFlags.Instance | BindingFlags.Public));
            body.Emit(OpCodes.Pop);
            body.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Generates and implements an interface property of the WCF contract
        /// by simply delegating the call to the corresponding property of the
        /// underlying "client".
        /// </summary>
        /// <param name="builder">The type builder for which to generate the implementing propery.</param>
        /// <param name="client">The WCF contract field.</param>
        /// <param name="prop">The property of the WCF contract interface to implement.</param>
        private static void PropertyBuilderHelper(TypeBuilder builder, FieldBuilder client, PropertyInfo prop)
        {
            var pi = builder.DefineProperty(prop.Name, prop.Attributes, prop.PropertyType, Type.EmptyTypes);
            if (prop.CanRead)
            {
#if NET40
                var getMeth = prop.GetGetMethod();
                var getter = builder.DefineMethod(getMeth.Name, propAttr, prop.PropertyType, Type.EmptyTypes);
#elif NET45
                var getter = builder.DefineMethod(prop.GetMethod.Name, propAttr, prop.PropertyType, Type.EmptyTypes);
#endif
                getter.SetCustomAttribute(_compGen);
                var body = getter.GetILGenerator();
                body.LoadThis();
                body.Emit(OpCodes.Ldfld, client);
#if NET40
                body.Emit(OpCodes.Callvirt, getMeth);
#elif NET45
                body.Emit(OpCodes.Callvirt, prop.GetMethod);
#endif
                body.Emit(OpCodes.Ret);
                pi.SetGetMethod(getter);
            }

            if (prop.CanWrite)
            {
#if NET40
                var setMeth = prop.GetSetMethod();
                var setter = builder.DefineMethod(setMeth.Name, propAttr, typeof(void), new[] { prop.PropertyType });
#elif NET45
                var setter = builder.DefineMethod(prop.SetMethod.Name, propAttr, typeof(void), new[] { prop.PropertyType });
#endif
                setter.SetCustomAttribute(_compGen);
                var body = setter.GetILGenerator();
                body.LoadThis();
                body.Emit(OpCodes.Ldfld, client);
                body.LoadArg(0);
#if NET40
                body.Emit(OpCodes.Callvirt, setMeth);
#elif NET45
                body.Emit(OpCodes.Callvirt, prop.SetMethod);
#endif
                body.Emit(OpCodes.Ret);
                pi.SetSetMethod(setter);
            }
        }

        /// <summary>
        /// Generates and implements an interface method of the WCF contract
        /// by simply delegating the call to the corresponding method of the
        /// underlying "client".
        /// </summary>
        /// <param name="builder">The type builder for which to generate the implementing method.</param>
        /// <param name="client">The WCF contract field.</param>
        /// <param name="meth">The method of the WCF contract interface to implement.</param>
        private static void MethodBuilderHelper(TypeBuilder builder, FieldBuilder client, MethodInfo meth)
        {
            var mi = builder.DefineMethod(meth.Name, methAttr);
            mi.SetReturnType(meth.ReturnType);
            var param = meth.GetParameters();
            mi.SetParameters(param.Select(p => p.ParameterType).ToArray());
            for (int i = 0; i < param.Length; i++)
                mi.DefineParameter(i, param[i].Attributes, param[i].Name);

            var body = mi.GetILGenerator();
            body.LoadThis();
            body.Emit(OpCodes.Ldfld, client);
            for (int i = 0; i < param.Length; i++)
                body.LoadArg(i);
            body.Emit(OpCodes.Callvirt, meth);
            body.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Get all methods from all interfaces.
        /// </summary>
        /// <param name="interfaces">Interface types from which to obtain methods.</param>
        private static IEnumerable<MethodInfo> GetAllMethods(IEnumerable<Type> interfaces)
        {
            // no special names to avoid property getters/setters
            return interfaces.SelectMany(i => i.GetMethods()).Where(m => !m.IsSpecialName);
        }

        /// <summary>
        /// Get all properties from all interfaces.
        /// </summary>
        /// <param name="interfaces">Interface types from which to obtain methods.</param>
        private static IEnumerable<PropertyInfo> GetAllProperties(IEnumerable<Type> interfaces)
        {
            return interfaces.SelectMany(i => i.GetProperties());
        }

        /// <summary>
        /// Ensure all interfaces have a ServiceContract attribute.
        /// </summary>
        /// <param name="interfaces">Interface types to check.</param>
        private static bool AllHaveServiceContractAttributes(IEnumerable<Type> interfaces)
        {
#if NET40
            return interfaces.All(i => i.GetCustomAttributes(typeof(ServiceContractAttribute), false).Any());
#elif NET45
            return interfaces.All(i => i.GetCustomAttributes<ServiceContractAttribute>(false).Any());
#endif
        }
    }
}