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
    }
}
