using System;
using Vintagestory.API.Common;

namespace thebasics.Models
{
    public class CharacterSheetModel
    {
        public int HeightCm { get; set; }
        public int WeightKg { get; set; }
        public string Demeanor { get; set; }
        public string PhysicalAppearance { get; set; }
        public string Background { get; set; }

        public CharacterSheetModel()
        {
            // Default values
            HeightCm = 170;
            WeightKg = 70;
            Demeanor = "";
            PhysicalAppearance = "";
            Background = "";
        }
    }
} 