using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Surgery.UI
{
    /// <summary>
    /// A utility class for building dialogs to display to players
    /// </summary>
    public class DialogBuilder
    {
        private readonly string title;
        private readonly string text;
        private readonly List<DialogButtonData> buttons = new List<DialogButtonData>();
        private Entity entityToTrack;
        private bool manualClose = false;
        
        /// <summary>
        /// Creates a new dialog builder with the specified title and text
        /// </summary>
        public DialogBuilder(string title, string text)
        {
            this.title = title;
            this.text = text;
        }
        
        /// <summary>
        /// Sets the entity to track in the dialog (the dialog will follow this entity)
        /// </summary>
        public DialogBuilder SetThingToTrack(Entity entity)
        {
            this.entityToTrack = entity;
            return this;
        }
        
        /// <summary>
        /// Sets whether the dialog can be closed manually by the player
        /// </summary>
        public DialogBuilder SetManualClose(bool manualClose)
        {
            this.manualClose = manualClose;
            return this;
        }
        
        /// <summary>
        /// Adds a button to the dialog
        /// </summary>
        /// <param name="text">The text to display on the button</param>
        /// <param name="onClick">The action to perform when the button is clicked</param>
        public DialogBuilder AddButton(string text, Action onClick)
        {
            buttons.Add(new DialogButtonData { Text = text, Action = onClick });
            return this;
        }
        
        /// <summary>
        /// Sends the dialog to the specified player
        /// </summary>
        public void SendTo(IServerPlayer player)
        {
            // Create dialog ID
            string dialogId = $"surgery-dialog-{DateTime.Now.Ticks}";
            
            // Build button data array
            DialogButton[] dialogButtons = new DialogButton[buttons.Count];
            
            for (int i = 0; i < buttons.Count; i++)
            {
                int index = i; // Capture for lambda
                dialogButtons[i] = new DialogButton
                {
                    Text = buttons[i].Text,
                    EventCode = $"button-{index}",
                    Enabled = true
                };
            }
            
            // Create dialog using the standard dialog texture from the game
            LoadedTexture texture = player.Entity.World.Api.Assets.Get(new AssetLocation("game:textures/gui/dialog/standarddialog.png")).ToTexture();
            
            IDialog dialog = new Dialog
            {
                DialogId = dialogId,
                Alignment = EnumDialogArea.CenterMiddle,
                ModalTransparency = 0.4f,
                CloseOnEscapePressed = true,
                ManualClose = manualClose,
                BackgroundTexture = texture,
                Title = title,
                Text = text,
                Buttons = dialogButtons
            };
            
            // Set entity to track if provided
            if (entityToTrack != null)
            {
                dialog.Track = entityToTrack;
                dialog.TrackingOffsetY = 2.0; // Offset above entity
            }
            
            // Register callback for button clicks
            player.Entity.World.Api.Event.RegisterCallback((id) => {
                if (id.StartsWith(dialogId + "|button-"))
                {
                    // Get button index from event ID
                    string indexStr = id.Substring((dialogId + "|button-").Length);
                    if (int.TryParse(indexStr, out int buttonIndex) && buttonIndex < buttons.Count)
                    {
                        // Execute button action
                        buttons[buttonIndex].Action?.Invoke();
                    }
                    
                    // Close the dialog
                    player.CloseDialog(dialogId);
                }
            }, 0.5);
            
            // Show dialog to player
            player.ShowDialog(dialog);
        }
        
        /// <summary>
        /// Data for a dialog button
        /// </summary>
        private class DialogButtonData
        {
            public string Text { get; set; }
            public Action Action { get; set; }
        }
    }
} 