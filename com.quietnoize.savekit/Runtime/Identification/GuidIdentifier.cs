namespace QuietNoize.SaveKit.Identification
{
    using UnityEngine;
    using System;

    /// <summary>
    /// Generates globally unique identifiers (GUIDs) for the associated GameObject.
    /// </summary>
    [AddComponentMenu("SaveKit/Guid Identifier")]
    public sealed class GuidIdentifier : IdentifierBase<byte[]>
    {
        [SerializeField] private bool m_isUniqueInstance = false;

        public override string Id =>
            IsIdAssigned ? new Guid(m_id).ToString("D") : "GUID_IS_NOT_ASSIGNED";

        protected override bool IsIdAssigned =>
            m_id != null &&
            m_id.Length == 16 &&
            new Guid(m_id) != Guid.Empty;

        protected override bool CanGenerateId => CanGenerateIdCheck();

        protected override byte[] GenerateNewId()
        {
            var guid = new Guid();

            do
            {
                guid = Guid.NewGuid();
            }
            while(s_pool.ContainsKey(FormatGuid(guid)));

            return guid.ToByteArray();
        }

        protected override byte[] GetTypedId(string idString)
        {
            return Guid.Parse(idString).ToByteArray();
        }

        private string FormatGuid(Guid guid)
        {
            return guid.ToString("D");
        }

        protected override void ResetId()
        {
            m_id = Guid.Empty.ToByteArray();
        }

        private bool CanGenerateIdCheck()
        {
#if UNITY_EDITOR
            if (IsInPrefabAssetOrStage() && !m_isUniqueInstance)
                return false;
#endif

            return true;
        }
    }
}
