using System;
using System.Collections.Generic;

namespace LightSide
{
    /// <summary>
    /// Which text kind every TMP-typed member in the project holds, keyed by the type that declares
    /// it, so a rewrite can resolve a receiver whose declaration lives in another file.
    /// </summary>
    /// <remarks>
    /// A key two declarations give different kinds answers nothing from then on: the index resolves
    /// only where the project is unambiguous, leaving everything else to be reported rather than
    /// guessed.
    /// </remarks>
    internal sealed class ScriptMemberIndex
    {
        private readonly Dictionary<string, MigrationMapping.TmpTypeKind> values =
            new(StringComparer.Ordinal);

        private readonly Dictionary<string, MigrationMapping.TmpTypeKind> collections =
            new(StringComparer.Ordinal);

        private readonly HashSet<string> contested = new(StringComparer.Ordinal);

        /// <summary>Records that <paramref name="declaringType"/> declares <paramref name="member"/> as that kind.</summary>
        public void Add(string declaringType, string member, MigrationMapping.TmpTypeKind kind,
            bool isCollection)
        {
            if (string.IsNullOrEmpty(declaringType) || string.IsNullOrEmpty(member)) return;

            var key = declaringType + "." + member;
            var table = isCollection ? collections : values;
            if (table.TryGetValue(key, out var existing))
            {
                if (existing != kind) contested.Add(key);
                return;
            }

            table[key] = kind;
        }

        /// <summary>
        /// The kind <paramref name="declaringType"/> gives <paramref name="member"/> across the
        /// project. False where nothing declares it, or where declarations disagree.
        /// </summary>
        public bool TryGetMember(string declaringType, string member, bool isCollection,
            out MigrationMapping.TmpTypeKind kind)
        {
            kind = default;
            if (string.IsNullOrEmpty(declaringType) || string.IsNullOrEmpty(member)) return false;

            var key = declaringType + "." + member;
            if (contested.Contains(key)) return false;
            return (isCollection ? collections : values).TryGetValue(key, out kind);
        }
    }
}
