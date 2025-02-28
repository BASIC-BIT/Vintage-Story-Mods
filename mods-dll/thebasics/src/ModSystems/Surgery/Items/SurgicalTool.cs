using System;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.Surgery.Items
{
    public class SurgicalTool : Item
    {
        public string ToolCode { get; private set; }
        
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            
            // Get the tool code from attributes
            if (Attributes != null && Attributes.HasAttribute("surgicalToolCode"))
            {
                ToolCode = Attributes["surgicalToolCode"].AsString();
            }
            else
            {
                // Try to extract from the item code (e.g., surgicaltools-scalpel-iron)
                string path = Code.Path;
                string[] parts = path.Split('-');
                
                if (parts.Length >= 2)
                {
                    // Assuming "surgicaltools" is first part, tool type is second part
                    ToolCode = parts[1];
                }
                else
                {
                    // Fallback to the last part of the code
                    ToolCode = path;
                }
            }
        }
        
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // If targeting an entity and that entity is a player, let the BedSurgeryHandler deal with it
            if (entitySel != null && entitySel.Entity != null)
            {
                // Let event system handle it
                return;
            }
            
            // Continue with base behavior
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
} 