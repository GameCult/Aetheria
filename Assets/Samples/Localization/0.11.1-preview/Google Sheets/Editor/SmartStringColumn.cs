using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

namespace UnityEditor.Localization.Samples.Google
{
    /// <summary>
    /// This is an example of how the Smart String property can be synchronized.
    /// Any value in the column will cause the value to be marked as smart and leaving the field empty will indicate it should not be smart.
    /// </summary>
    public class SmartStringColumn : LocaleMetadataColumn<SmartFormatTag>
    {
        public override PushFields PushFields => PushFields.Value;

        public override void PullMetadata(StringTableEntry entry, SmartFormatTag metadata, string cellValue, string cellNote)
        {
            entry.IsSmart = !string.IsNullOrEmpty(cellValue);
        }

        public override void PushHeader(StringTableCollection collection, out string header, out string headerNote)
        {
            header = $"{LocaleIdentifier.ToString()} - Is Smart String";
            headerNote = null;
        }

        public override void PushMetadata(SmartFormatTag metadata, out string value, out string note)
        {
            value = "x"; // We mark here with an x but it could be anything.
            note = null;
        }
    }
}
