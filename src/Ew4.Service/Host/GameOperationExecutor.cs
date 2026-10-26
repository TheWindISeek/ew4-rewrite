using Ew4.Interface.Host;
using Ew4.Interface.Operations;

using Ew4.Service.Plans;

namespace Ew4.Service.Host;

public sealed class GameOperationExecutor : IGameOperations
{
    private readonly IRuntimeHost _runtime;
    private readonly PlanRunner _plans;
    private readonly PlayStaticAssetService _staticAssets;

    public GameOperationExecutor(
        IRuntimeHost runtime,
        PlanRunner plans,
        PlayStaticAssetService staticAssets)
    {
        _runtime = runtime;
        _plans = plans;
        _staticAssets = staticAssets;
    }

    public string? ActiveContentRevision => _runtime.ActiveSnapshot?.Revision;

    public OperationResponse Invoke(OperationDescriptor descriptor, OperationPayload payload)
    {
        try
        {
            return descriptor is StaticHttpAssetDescriptor staticAsset
                ? _staticAssets.Serve(staticAsset.RequestPath)
                : _plans.Run(descriptor, payload);
        }
        catch (Exception error)
        {
            OperationResponse? client = OperationInvokeFaults.TryClientError(error);
            if (client != null)
            {
                return client;
            }

            throw;
        }
    }
}
