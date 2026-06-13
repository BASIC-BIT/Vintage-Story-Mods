#nullable enable

using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.ProximityChat.Semantics;

[ProtoContract]
public class SemanticLanguageMemoryStore
{
    [ProtoMember(1)]
    public List<SemanticLanguageMemory> Languages { get; set; } = new List<SemanticLanguageMemory>();
}

[ProtoContract]
public class SemanticLanguageMemory
{
    [ProtoMember(1)]
    public string LanguageName { get; set; } = string.Empty;

    [ProtoMember(2)]
    public List<SemanticLanguageAtlasBucketCoverage> AtlasBuckets { get; set; } = new List<SemanticLanguageAtlasBucketCoverage>();
}

[ProtoContract]
public class SemanticLanguageAtlasBucketCoverage
{
    [ProtoMember(1)]
    public string BucketId { get; set; } = string.Empty;

    [ProtoMember(2)]
    public int Confidence { get; set; }

    [ProtoMember(3)]
    public int ExposureCount { get; set; }

    [ProtoMember(4)]
    public long LastUpdatedUnixSeconds { get; set; }

    [ProtoMember(5)]
    public long LearnedAtUnixSeconds { get; set; }
}
