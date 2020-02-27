using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NetSharp.Deprecated;
using NetSharp.Utils;

namespace NetSharp.Packets
{
    /// <summary>
    /// Provides method of registering request packets and their relevant response packets, as well as mapping their ids.
    /// </summary>
    internal static class PacketRegistry
    {
        /// <summary>
        /// The start id for automatically generated packet type ids. Any custom packet type ids lower than this value
        /// that come from external assemblies will be incremented by this value, to ensure that there are no clashes.
        /// </summary>
        private const uint AutomaticPacketTypeIdStartPoint = 100;

        /// <summary>
        /// The lock object for synchronising access to the <see cref="currentAutomaticPacketTypeIdCounter"/> field.
        /// </summary>
        private static readonly object currentAutomaticPacketTypeIdCounterLockObject = new object();

        /// <summary>
        /// Maps a packet type id to its relevant packet type, and vice-versa.
        /// </summary>
        private static readonly BiDictionary<uint, Type> idToPacketTypeMap;

        /// <summary>
        /// The assembly that represents the library, where all of the builtin packets are defined.
        /// </summary>
        private static readonly Assembly LibraryAssembly = Assembly.GetAssembly(typeof(PacketRegistry));

        /// <summary>
        /// Maps a request packet to its relevant response packet, and vice-versa.
        /// </summary>
        private static readonly BiDictionary<Type, Type> requestToResponseMap;

        /// <summary>
        /// The current id for registered packets.
        /// </summary>
        private static uint currentAutomaticPacketTypeIdCounter = AutomaticPacketTypeIdStartPoint;

        /// <summary>
        /// Fetches the packet type id of the given packet type. If the packet type is declared outside of the library
        /// assembly, then its value is incremented by the <see cref="AutomaticPacketTypeIdStartPoint"/> value. This ensure that
        /// there are no clashes between the packet type ids of packets declared in the library and external packets.
        /// </summary>
        /// <param name="packetType">The packet type whose id should be fetched.</param>
        /// <returns>The id of the given packet type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetNewPacketTypeId(Type packetType)
        {
            uint packetTypeId;

            if (packetType.Assembly != LibraryAssembly)
            {
                lock (currentAutomaticPacketTypeIdCounterLockObject)
                {
                    packetTypeId = currentAutomaticPacketTypeIdCounter++;
                }
            }
            else
            {
                PacketTypeIdAttribute customPacketTypeIdAttribute =
                    (PacketTypeIdAttribute)packetType.GetCustomAttributes(typeof(PacketTypeIdAttribute)).First();

                packetTypeId = customPacketTypeIdAttribute.Id;
            }

            return packetTypeId;
        }

        /// <summary>
        /// Deregisters the given packet type from the registry.
        /// </summary>
        /// <param name="requestPacketType">The request packet type to deregister, if it is registered.</param>
        /// <param name="responsePacketType">The response packet associated with the request packet.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DeregisterPacketType(Type requestPacketType, Type? responsePacketType)
        {
            if (idToPacketTypeMap.ContainsValue(requestPacketType))
            {
                idToPacketTypeMap.TryClearKey(requestPacketType, out _);
            }

            // if the given response packet is null, then skip deregistering a response packet type
            if (responsePacketType == default) return;

            if (!idToPacketTypeMap.ContainsValue(responsePacketType))
            {
                idToPacketTypeMap.TryClearKey(responsePacketType, out _);
            }

            if (!requestToResponseMap.ContainsValue(requestPacketType))
            {
                requestToResponseMap.TryClearKey(requestPacketType, out _);
            }
        }

        /// <summary>
        /// Deregisters the given packet types from the registry.
        /// </summary>
        /// <param name="requestToResponsePacketTypeMap">The list of packet types to deregister, if they are registered.</param>
        internal static void DeregisterPacketTypes(Dictionary<Type, Type?> requestToResponsePacketTypeMap)
        {
            foreach ((Type requestPacketType, Type? responsePacketType) in requestToResponsePacketTypeMap)
            {
                DeregisterPacketType(requestPacketType, responsePacketType);
            }
        }

        /// <summary>
        /// Returns the packet type id associated with the given packet type.
        /// </summary>
        /// <param name="packetType">The packet type whose id to fetch.</param>
        /// <returns>The id of the packet type given.</returns>
        internal static uint GetPacketId(Type packetType) => idToPacketTypeMap[packetType];

        /// <summary>
        /// Returns the packet type id associated with the given packet type.
        /// </summary>
        /// <typeparam name="TPacket">The packet type whose id to fetch.</typeparam>
        /// <returns>The id of the packet type given.</returns>
        internal static uint GetPacketId<TPacket>() where TPacket : IPacket => idToPacketTypeMap[typeof(TPacket)];

        /// <summary>
        /// Returns the packet type associated with the given id.
        /// </summary>
        /// <param name="packetTypeId">The packet id whose mapped type to fetch.</param>
        /// <returns>The packet type mapped by the given id.</returns>
        internal static Type GetPacketType(uint packetTypeId) => idToPacketTypeMap[packetTypeId];

        /// <summary>
        /// Returns the type of request packet mapped by the given response packet type.
        /// </summary>
        /// <typeparam name="TResponse">The response packet type whose request packet type to fetch.</typeparam>
        /// <returns>The request packet type, <c>null</c> if no type is mapped.</returns>
        internal static Type GetRequestPacketType<TResponse>() where TResponse : IResponsePacket<IRequestPacket>
        {
            requestToResponseMap.TryGetKey(typeof(TResponse), out Type requestPacketType);

            return requestPacketType;
        }

        /// <summary>
        /// Returns the type of request packet mapped by the given response packet type.
        /// </summary>
        /// <param name="responsePacketType">The response packet type whose request packet type to fetch.</param>
        /// <returns>The request packet type, <c>null</c> if no type is mapped.</returns>
        internal static Type GetRequestPacketType(Type responsePacketType)
        {
            requestToResponseMap.TryGetKey(responsePacketType, out Type requestPacketType);

            return requestPacketType;
        }

        /// <summary>
        /// Returns the type of response packet mapped by the given request packet type.
        /// </summary>
        /// <typeparam name="TRequest">The request packet type whose response packet type to fetch.</typeparam>
        /// <returns>The response packet type, <c>null</c> if no type is mapped.</returns>
        internal static Type? GetResponsePacketType<TRequest>() where TRequest : IRequestPacket
        {
            return requestToResponseMap.TryGetValue(typeof(TRequest), out Type responsePacketType) ? responsePacketType : default;
        }

        /// <summary>
        /// Returns the type of response packet mapped by the given request packet type.
        /// </summary>
        /// <param name="requestPacketType">The request packet type whose response packet type to fetch.</param>
        /// <returns>The response packet type, <c>null</c> if no type is mapped.</returns>
        internal static Type? GetResponsePacketType(Type requestPacketType)
        {
            return requestToResponseMap.TryGetValue(requestPacketType, out Type responsePacketType) ? responsePacketType : default;
        }

        /// <summary>
        /// Rebuilds the packet registry, by registering every <see cref="IPacket"/> inheritor in the given assemblies.
        /// </summary>
        /// <param name="packetSourceAssemblies">
        /// The assemblies from which the packet types to register are sourced.
        /// </param>
        internal static void RegisterPacketSourceAssemblies(params Assembly[] packetSourceAssemblies)
        {
            foreach (Assembly assembly in packetSourceAssemblies)
            {
                RegisterPacketSourceAssembly(assembly);
            }
        }

        /// <summary>
        /// Registers all the <see cref="IPacket"/> implementors in the given assembly.
        /// </summary>
        /// <param name="packetSourceAssembly">The assembly whose packet types to register.</param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RegisterPacketSourceAssembly(Assembly packetSourceAssembly)
        {
            Dictionary<Type, Type?> requestToResponseTypeMap = new Dictionary<Type, Type?>();

            foreach (Type type in packetSourceAssembly.DefinedTypes)
            {
                foreach (Type interfaceType in type.GetInterfaces())
                {
                    if (!typeof(IPacket).IsAssignableFrom(interfaceType) || interfaceType == typeof(IPacket))
                    {
                        continue;
                    }

                    if (interfaceType == typeof(IRequestPacket))
                    {
                        requestToResponseTypeMap[type] = default;
                    }
                    else //if (interfaceType == typeof(IResponsePacket<>))
                    {
                        Type handledRequestType = interfaceType.GetGenericArguments()[0];

                        requestToResponseTypeMap[handledRequestType] = type;
                    }
                }
            }

            RegisterPacketTypes(requestToResponseTypeMap);
        }

        /// <summary>
        /// Registers the given packet type to the registry.
        /// </summary>
        /// <param name="requestPacketType">The request packet type to register, if it is not registered.</param>
        /// <param name="responsePacketType">The response packet associated with the request packet.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RegisterPacketType(Type requestPacketType, Type? responsePacketType)
        {
            if (!idToPacketTypeMap.ContainsValue(requestPacketType))
            {
                uint requestPacketTypeId = GetNewPacketTypeId(requestPacketType);

                idToPacketTypeMap.TrySetValue(requestPacketTypeId, requestPacketType);
            }

            // if the given response packet is null, then skip registering a response packet type
            if (responsePacketType == default) return;

            if (!idToPacketTypeMap.ContainsValue(responsePacketType))
            {
                uint responsePacketTypeId = GetNewPacketTypeId(responsePacketType);

                idToPacketTypeMap.TrySetValue(responsePacketTypeId, responsePacketType);
            }

            if (!requestToResponseMap.ContainsValue(requestPacketType))
            {
                requestToResponseMap.TrySetValue(requestPacketType, responsePacketType);
            }
        }

        /// <summary>
        /// Registers the given packet types to the registry.
        /// </summary>
        /// <param name="requestToResponsePacketTypeMap">
        /// The dictionary mapping the request packet types to register, to their relevant response packet types.
        /// The response packet type can be null; then the request packet type is treated as a 'simple' packet.
        /// </param>
        internal static void RegisterPacketTypes(Dictionary<Type, Type?> requestToResponsePacketTypeMap)
        {
            foreach ((Type requestPacketType, Type? responsePacketType) in requestToResponsePacketTypeMap)
            {
                RegisterPacketType(requestPacketType, responsePacketType);
            }
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="PacketRegistry"/> class.
        /// </summary>
        static PacketRegistry()
        {
            idToPacketTypeMap = new BiDictionary<uint, Type>();

            requestToResponseMap = new BiDictionary<Type, Type>();

            RegisterPacketSourceAssembly(LibraryAssembly);
        }
    }
}