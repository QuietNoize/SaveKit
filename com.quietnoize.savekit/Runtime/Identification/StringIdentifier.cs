namespace QuietNoize.SaveKit.Identification
{
    using UnityEngine;

    /// <summary>
    /// Generates identifiers based on the name of the associated GameObject.
    /// </summary>
    [AddComponentMenu("SaveKit/String Identifier")]
    public sealed class StringIdentifier : IdentifierBase<string>
    {
        public override string Id => m_id;

        protected override bool IsIdAssigned => !string.IsNullOrEmpty(m_id);

        protected override string GenerateNewId()
        {
            string rawId = string.IsNullOrEmpty(m_id)
                ? gameObject.name
                : m_id;

            int suffixIndex = rawId.LastIndexOf(" (");

            if (suffixIndex >= 0 && rawId.EndsWith(")"))
            {
                string suffix = rawId.Substring(suffixIndex + 2, rawId.Length - suffixIndex - 3);

                if (int.TryParse(suffix, out _))
                    rawId = rawId.Substring(0, suffixIndex);
            }

            string id;
            int counter = 0;

            do
            {
                id = counter == 0
                    ? rawId
                    : $"{rawId} ({counter})";

                counter++;
            }
            while (s_pool.ContainsKey(id));

            return id;
        }

        protected override string GetTypedId(string idString) => idString;
    }
}
