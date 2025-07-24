﻿using System;
using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.ProximityChat.Models;

[ProtoContract(SkipConstructor = true)]
public record Language(string Name, string Description, string Prefix, string[] Syllables, string Color, bool Default = false, bool Hidden = false, string[] GrantedToClasses = null)
{
    [ProtoMember(1)]
    public string Name { get; init; } = Name;
    
    [ProtoMember(2)]
    public string Description { get; init; } = Description;
    
    [ProtoMember(3)]
    public string Prefix { get; init; } = Prefix;
    
    [ProtoMember(4)]
    public string[] Syllables { get; init; } = Syllables;
    
    [ProtoMember(5)]
    public string Color { get; init; } = Color;
    
    [ProtoMember(6)]
    public bool Default { get; set; } = Default;
    
    [ProtoMember(7)]
    public bool Hidden { get; set; } = Hidden;
    
    [ProtoMember(8)]
    public string[] GrantedToClasses { get; init; } = GrantedToClasses ?? Array.Empty<string>();
}