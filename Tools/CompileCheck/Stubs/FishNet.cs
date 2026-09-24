// Reference stubs for FishNet 4.7.3 (com.firstgeargames.fishnet, pinned in Packages/manifest.json):
// the parts the game and its editor tools use. Signatures only, copied from the package source.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Broadcast
{
    public interface IBroadcast { }
}

namespace FishNet.Connection
{
    public partial class NetworkConnection
    {
        public int ClientId = -1;
        public bool IsActive => ClientId >= 0;
        public bool IsValid => ClientId >= 0;
        public bool IsHost => false;
        public bool IsLocalClient => false;
        public bool IsAuthenticated { get; private set; }
        public string GetAddress() => string.Empty;
        public void Disconnect(bool immediately) { }
    }
}

namespace FishNet.Transporting
{
    public enum Channel : byte { Reliable = 0, Unreliable = 1 }

    [Flags]
    public enum LocalConnectionState : int { Stopped = 1 << 0, Stopping = 1 << 1, Starting = 1 << 2, Started = 1 << 3 }
    public enum RemoteConnectionState : byte { Stopped = 0, Started = 2 }

    public struct RemoteConnectionStateArgs
    {
        public int TransportIndex;
        public RemoteConnectionState ConnectionState;
        public int ConnectionId;
        public RemoteConnectionStateArgs(RemoteConnectionState connectionState, int connectionId, int transportIndex) { ConnectionState = connectionState; ConnectionId = connectionId; TransportIndex = transportIndex; }
    }

    public struct ServerConnectionStateArgs
    {
        public int TransportIndex;
        public LocalConnectionState ConnectionState;
        public ServerConnectionStateArgs(LocalConnectionState connectionState, int transportIndex) { ConnectionState = connectionState; TransportIndex = transportIndex; }
    }

    public struct ClientConnectionStateArgs
    {
        public LocalConnectionState ConnectionState;
        public int TransportIndex;
        public ClientConnectionStateArgs(LocalConnectionState connectionState, int transportIndex) { ConnectionState = connectionState; TransportIndex = transportIndex; }
    }

    public abstract class Transport : MonoBehaviour
    {
        public virtual int GetMaximumClients() => 0;
        public virtual void SetMaximumClients(int value) { }
        public virtual void SetClientAddress(string address) { }
        public virtual string GetClientAddress() => string.Empty;
        public virtual void SetPort(ushort port) { }
        public virtual ushort GetPort() => 0;
    }
}

namespace FishNet.Transporting.Tugboat
{
    public class Tugboat : Transport
    {
        public override int GetMaximumClients() => 0;
        public override void SetMaximumClients(int value) { }
        public override void SetClientAddress(string address) { }
        public override string GetClientAddress() => string.Empty;
        public override void SetPort(ushort port) { }
        public override ushort GetPort() => 0;
    }
}

namespace FishNet.Object
{
    using FishNet.Connection;
    using FishNet.Managing;

    public partial class NetworkObject : MonoBehaviour
    {
        public bool IsGlobal { get; private set; }
        public void SetIsGlobal(bool value) { }
        public int ObjectId { get; private set; } = ushort.MaxValue;
        public bool GetIsNetworked() => true;
        public void SetIsNetworked(bool value) { }
        public bool IsOwner => false;
        public NetworkConnection Owner => null;
        public int OwnerId => -1;
        public bool IsSpawned => false;
        public NetworkManager NetworkManager => null;
    }

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        public NetworkObject NetworkObject => null;
        public NetworkManager NetworkManager => null;
        public bool IsSpawned => false;
        public bool IsClientInitialized => false;
        public bool IsServerInitialized => false;
        public bool IsHostInitialized => false;
        public bool IsOwner => false;
        public bool IsController => false;
        public NetworkConnection Owner => null;
        public int OwnerId => -1;
        public int ObjectId => -1;
        public virtual void OnStartNetwork() { }
        public virtual void OnStopNetwork() { }
        public virtual void OnStartServer() { }
        public virtual void OnStopServer() { }
        public virtual void OnStartClient() { }
        public virtual void OnStopClient() { }
        public virtual void OnOwnershipClient(NetworkConnection prevOwner) { }
        protected virtual void Reset() { }
        protected virtual void OnValidate() { }
    }
}

namespace FishNet.Component.Transforming
{
    public sealed class NetworkTransform : FishNet.Object.NetworkBehaviour
    {
        public enum ComponentConfigurationType { Disabled = 0, CharacterController = 1, Rigidbody = 2, Rigidbody2D = 3 }
        public void SetSynchronizePosition(bool value) { }
        public void SetSynchronizeRotation(bool value) { }
        public void SetSynchronizeScale(bool value) { }
        public void SetSendToOwner(bool value) { }
        public void SetInterpolation(ushort value) { }
        public void SetExtrapolation(ushort value) { }
    }
}

namespace FishNet.Managing.Object
{
    using FishNet.Object;

    public abstract class PrefabObjects : ScriptableObject
    {
        public abstract void Clear();
        public abstract int GetObjectCount();
        public abstract NetworkObject GetObject(bool asServer, int id);
        public abstract void RemoveNull();
        public abstract void AddObject(NetworkObject networkObject, bool checkForDuplicates = false, bool initializeAdded = true);
        public abstract void AddObjects(List<NetworkObject> networkObjects, bool checkForDuplicates = false, bool initializeAdded = true);
        public abstract void AddObjects(NetworkObject[] networkObjects, bool checkForDuplicates = false, bool initializeAdded = true);
        public abstract void InitializePrefabRange(int startIndex);
    }

    public class SinglePrefabObjects : PrefabObjects
    {
        public IReadOnlyList<NetworkObject> Prefabs => null;
        public override void Clear() { }
        public override int GetObjectCount() => 0;
        public override NetworkObject GetObject(bool asServer, int id) => null;
        public override void RemoveNull() { }
        public override void AddObject(NetworkObject networkObject, bool checkForDuplicates = false, bool initializeAdded = true) { }
        public override void AddObjects(List<NetworkObject> networkObjects, bool checkForDuplicates = false, bool initializeAdded = true) { }
        public override void AddObjects(NetworkObject[] networkObjects, bool checkForDuplicates = false, bool initializeAdded = true) { }
        public override void InitializePrefabRange(int startIndex) { }
    }

    public class DefaultPrefabObjects : SinglePrefabObjects { }
}

namespace FishNet.Managing.Transporting
{
    public sealed partial class TransportManager : MonoBehaviour
    {
        public FishNet.Transporting.Transport Transport;
    }
}

namespace FishNet.Managing.Timing
{
    public sealed partial class TimeManager : MonoBehaviour
    {
        public long RoundTripTime { get; private set; }
    }
}

namespace FishNet.Managing.Scened
{
    using FishNet.Connection;
    using FishNet.Object;

    public sealed class SceneManager : MonoBehaviour
    {
        public event Action<NetworkConnection, bool> OnClientLoadedStartScenes;
        public void AddOwnerToDefaultScene(NetworkObject nob) { }
    }
}

namespace FishNet.Authenticating
{
    using FishNet.Connection;
    using FishNet.Managing;

    public abstract class Authenticator : MonoBehaviour
    {
        public bool Initialized { get; private set; }
        protected NetworkManager NetworkManager { get; private set; }
        public abstract event Action<NetworkConnection, bool> OnAuthenticationResult;
        public virtual void InitializeOnce(NetworkManager networkManager) { }
        public virtual void OnRemoteConnection(NetworkConnection connection) { }
    }
}

namespace FishNet.Managing.Server
{
    using FishNet.Authenticating;
    using FishNet.Broadcast;
    using FishNet.Connection;
    using FishNet.Object;
    using FishNet.Transporting;

    public sealed partial class ServerManager : MonoBehaviour
    {
        public Authenticator GetAuthenticator() => null;
        public void SetAuthenticator(Authenticator value) { }
        public void RegisterBroadcast<T>(Action<NetworkConnection, T, Channel> handler, bool requireAuthentication = true) where T : struct, IBroadcast { }
        public void UnregisterBroadcast<T>(Action<NetworkConnection, T, Channel> handler) where T : struct, IBroadcast { }
        public void Broadcast<T>(NetworkConnection connection, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast { }
        public void Broadcast<T>(T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast { }
        public event Action<ServerConnectionStateArgs> OnServerConnectionState;
        public event Action<NetworkConnection, RemoteConnectionStateArgs> OnRemoteConnectionState;
        public bool Started { get; private set; }
        public Dictionary<int, NetworkConnection> Clients = new Dictionary<int, NetworkConnection>();
        public bool GetStartOnHeadless() => false;
        public void SetStartOnHeadless(bool value) { }
        public bool StartConnection() => false;
        public bool StartConnection(ushort port) => false;
        public bool StopConnection(bool sendDisconnectMessage) => false;
        public void Spawn(GameObject go, NetworkConnection ownerConnection = null, UnityEngine.SceneManagement.Scene scene = default) { }
        public void Spawn(NetworkObject nob, NetworkConnection ownerConnection = null, UnityEngine.SceneManagement.Scene scene = default) { }
    }
}

namespace FishNet.Managing.Client
{
    using FishNet.Broadcast;
    using FishNet.Connection;
    using FishNet.Transporting;

    public sealed partial class ClientManager : MonoBehaviour
    {
        public void RegisterBroadcast<T>(Action<T, Channel> handler) where T : struct, IBroadcast { }
        public void UnregisterBroadcast<T>(Action<T, Channel> handler) where T : struct, IBroadcast { }
        public void Broadcast<T>(T message, Channel channel = Channel.Reliable) where T : struct, IBroadcast { }
        public event Action OnAuthenticated;
        public event Action<ClientConnectionStateArgs> OnClientConnectionState;
        public event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        public bool Started { get; private set; }
        public NetworkConnection Connection;
        public bool StartConnection() => false;
        public bool StartConnection(string address) => false;
        public bool StartConnection(string address, ushort port) => false;
        public bool StopConnection() => false;
    }
}

namespace FishNet.Managing
{
    using FishNet.Managing.Client;
    using FishNet.Managing.Object;
    using FishNet.Managing.Scened;
    using FishNet.Managing.Server;
    using FishNet.Managing.Timing;
    using FishNet.Managing.Transporting;
    using FishNet.Object;

    public sealed partial class NetworkManager : MonoBehaviour
    {
        public static IReadOnlyList<NetworkManager> Instances => null;
        public bool Initialized { get; private set; }
        public ServerManager ServerManager { get; private set; }
        public ClientManager ClientManager { get; private set; }
        public TransportManager TransportManager { get; private set; }
        public TimeManager TimeManager { get; private set; }
        public SceneManager SceneManager { get; private set; }
        public PrefabObjects SpawnablePrefabs { get; set; }
        public bool IsServerStarted => false;
        public bool IsClientStarted => false;
        public NetworkObject GetPooledInstantiated(NetworkObject prefab, bool asServer) => null;
        public NetworkObject GetPooledInstantiated(NetworkObject prefab, Transform parent, bool asServer) => null;
        public NetworkObject GetPooledInstantiated(NetworkObject prefab, Vector3 position, Quaternion rotation, bool asServer) => null;
    }
}
