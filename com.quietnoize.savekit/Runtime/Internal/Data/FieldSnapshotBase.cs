namespace QuietNoize.SaveKit.Internal.Data
{
    using UnityEngine.Scripting.APIUpdating;
    using System;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Data")]
    [Serializable]
    internal abstract class MemberSnapshotBase
    {
        internal abstract string Name { get; }
        internal abstract string FieldType { get; }
        internal abstract object Value { get; }
    }
}
