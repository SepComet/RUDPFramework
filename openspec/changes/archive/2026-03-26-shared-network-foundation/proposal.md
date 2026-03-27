## Why

The project's transport and message pipeline are now strong enough to serve both client and server, but the current runtime assembly still mixes reusable networking code with Unity-specific hosting concerns such as `NetworkManager` and main-thread pumping. If the client and server continue to evolve separately, transport, protocol handling, and dispatch behavior will drift and the same bugs will be fixed twice.

## What Changes

- Extract the reusable transport, session, protocol-envelope, and message-routing core into a shared client/server networking foundation.
- Introduce a host-side dispatcher abstraction so `MessageManager` depends on an injected dispatch strategy rather than a hard-coded Unity main-thread implementation.
- Keep Unity-specific hosting, frame-loop pumping, and gameplay/UI handlers in the client host layer while enabling a non-Unity server host to use the same networking core.
- Add tests and documentation that prove the same shared networking layer can run under both client-style and server-style hosting paths.

## Capabilities

### New Capabilities
- `shared-network-foundation`: Defines the shared transport/message infrastructure that both client and server hosts use without depending on Unity-specific runtime classes.

### Modified Capabilities
- `network-main-thread-dispatch`: Refine the threading requirement so Unity main-thread dispatch is a host-specific strategy layered on top of a host-injected dispatcher abstraction, not the only message execution model.

## Impact

- Affected code: `Assets/Scripts/Network/NetworkTransport/`, `Assets/Scripts/Network/NetworkApplication/`, `Assets/Scripts/NetworkManager.cs`, and new host/dispatcher abstraction code.
- Affected architecture: shared networking core becomes independent from Unity `MonoBehaviour` hosting; client and server each provide their own runtime host and dispatch policy.
- Dependencies: no new external packages expected, but assembly boundaries and tests will need to be reorganized to support shared code reuse.
