using MessagePack;
using MessagePack.Resolvers;
using UnityEngine;

namespace Assets.Scripts
{
    class InitialSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterResolvers()
        {
            // NOTE: Currently, CompositeResolver doesn't work on Unity IL2CPP build. Use StaticCompositeResolver instead of it.
            StaticCompositeResolver.Instance.Register(
                MathResolver.Instance,
                BuiltinResolver.Instance,
                PrimitiveObjectResolver.Instance
            );

            MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions
                .WithResolver(StaticCompositeResolver.Instance);
        }
    }
}
