namespace QuietNoize.SaveKit.Internal.Serialization
{
    using UnityEngine.Scripting.APIUpdating;
    using System;
    using QuietNoize.SaveKit.Serialization;
    using UnityEngine;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Serialization")]
    [Serializable]
    internal sealed class TransformSerializerDefinition
        : ComponentSerializerDefinition<Transform>
    {
        protected override MemberSerializerDefinition[] GetMembers()
        {
            return new[]
            {
                new MemberSerializerDefinition(
                    nameof(Transform.localPosition),
                    typeof(Vector3)
                ),

                new MemberSerializerDefinition(
                    nameof(Transform.localRotation),
                    typeof(Quaternion)
                ),

                new MemberSerializerDefinition(
                    nameof(Transform.localScale),
                    typeof(Vector3)
                )
            };
        }
    }
}
