using System;

namespace Wikidown.Vs
{
    /// <summary>Stable GUIDs shared across the Wikidown VS extension.</summary>
    internal static class PackageGuids
    {
        /// <summary>VSIX AsyncPackage identity.</summary>
        public const string PackageGuidString = "5f8b2e3a-c4d7-4e9f-a0b1-234567890abc";
        public static readonly Guid Package = new Guid(PackageGuidString);

        /// <summary>
        /// Project type GUID registered in .sln files.
        /// Used as the first GUID in the Project() declaration.
        /// </summary>
        public const string ProjectTypeGuidString = "6a9c3f4b-d5e8-4f0a-b1c2-345678901bcd";
        public static readonly Guid ProjectType = new Guid(ProjectTypeGuidString);

        /// <summary>Command set for the wiki node context-menu commands (matches WikidownCommands.vsct).</summary>
        public const string CmdSetGuidString = "8b7f3d5a-2c41-4e8f-9a6b-1d2e3f405162";
        public static readonly Guid CmdSet = new Guid(CmdSetGuidString);

        public const uint CmdIdMovePageUp       = 0x0100;
        public const uint CmdIdMovePageDown     = 0x0101;
        public const uint CmdIdEditPageOrder    = 0x0102;
        public const uint CmdIdAddPage          = 0x0103;
        public const uint CmdIdAddFolder        = 0x0104;
        public const uint CmdIdAddExistingPages = 0x0105;
        public const uint CmdIdDeletePage       = 0x0106;
        public const uint CmdIdDeleteFolder     = 0x0107;
        public const uint CmdIdExportPdf        = 0x0108;
    }
}
