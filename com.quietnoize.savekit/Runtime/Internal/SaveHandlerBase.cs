namespace QuietNoize.SaveKit.Internal
{
    using UnityEngine.Scripting.APIUpdating;
    using QuietNoize.SaveKit.Internal.Data;
    using UnityEngine;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Components")]
    public abstract class SaveHandlerBase : MonoBehaviour
    {
        internal abstract int Order { get; }

        internal abstract bool TryGetSnapshot(out GameObjectSnapshot snapshot);
    }
}
