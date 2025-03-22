using System;
using thebasics.Models;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace thebasics.ModSystems.ChatUiSystem
{
    public class CharacterSheetDialog : GuiDialog
    {
        private CharacterSheetModel characterSheet;
        private GuiElementTextInput heightInput;
        private GuiElementTextInput weightInput;
        private GuiElementTextInput demeanorInput;
        private GuiElementTextInput appearanceInput;
        private GuiElementTextInput backgroundInput;
        private Action onSave;

        public override string ToggleKeyCombinationCode => "characterdialog";

        public CharacterSheetDialog(ICoreClientAPI capi, CharacterSheetModel sheet, Action onSaveCallback = null) : base(capi)
        {
            this.characterSheet = sheet ?? new CharacterSheetModel();
            this.onSave = onSaveCallback;
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedPosition(0, 0);

            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 400, 30);

            // Create a new composition
            SingleComposer = capi.Gui
                .CreateCompo("characterSheetDialog", dialogBounds)
                .AddDialogTitleBar("Character Sheet", OnTitleBarCloseClicked);

            ElementBounds leftColumn = ElementBounds.Fixed(20, 40, 140, 25);
            ElementBounds rightColumn = ElementBounds.Fixed(170, 40, 200, 25);

            // Height
            SingleComposer
                .AddStaticText("Height (cm):", CairoFont.WhiteDetailText(), leftColumn)
                .AddTextInput(rightColumn, OnHeightChanged, CairoFont.WhiteDetailText(), "height");
            heightInput = SingleComposer.GetTextInput("height");
            heightInput.SetValue(characterSheet.HeightCm.ToString());

            // Weight
            leftColumn = leftColumn.BelowCopy(0, 35);
            rightColumn = rightColumn.BelowCopy(0, 35);
            SingleComposer
                .AddStaticText("Weight (kg):", CairoFont.WhiteDetailText(), leftColumn)
                .AddTextInput(rightColumn, OnWeightChanged, CairoFont.WhiteDetailText(), "weight");
            weightInput = SingleComposer.GetTextInput("weight");
            weightInput.SetValue(characterSheet.WeightKg.ToString());

            // Demeanor
            leftColumn = leftColumn.BelowCopy(0, 35);
            rightColumn = rightColumn.BelowCopy(0, 35).WithFixedHeight(60);
            SingleComposer
                .AddStaticText("Demeanor:", CairoFont.WhiteDetailText(), leftColumn)
                .AddTextInput(rightColumn, OnDemeanorChanged, CairoFont.WhiteDetailText(), "demeanor");
            demeanorInput = SingleComposer.GetTextInput("demeanor");
            demeanorInput.SetValue(characterSheet.Demeanor);

            // Appearance
            leftColumn = leftColumn.BelowCopy(0, 70);
            rightColumn = rightColumn.BelowCopy(0, 70);
            SingleComposer
                .AddStaticText("Appearance:", CairoFont.WhiteDetailText(), leftColumn)
                .AddTextInput(rightColumn, OnAppearanceChanged, CairoFont.WhiteDetailText(), "appearance");
            appearanceInput = SingleComposer.GetTextInput("appearance");
            appearanceInput.SetValue(characterSheet.PhysicalAppearance);

            // Background
            leftColumn = leftColumn.BelowCopy(0, 70);
            rightColumn = rightColumn.BelowCopy(0, 70);
            SingleComposer
                .AddStaticText("Background:", CairoFont.WhiteDetailText(), leftColumn)
                .AddTextInput(rightColumn, OnBackgroundChanged, CairoFont.WhiteDetailText(), "background");
            backgroundInput = SingleComposer.GetTextInput("background");
            backgroundInput.SetValue(characterSheet.Background);

            // Save button
            ElementBounds buttonBounds = ElementBounds.Fixed(0, 0, 60, 25);
            buttonBounds.WithAlignment(EnumDialogArea.RightBottom).WithFixedPadding(10, 2);

            SingleComposer
                .AddSmallButton("Save", OnSaveClicked, buttonBounds);

            SingleComposer.Compose();
        }

        private void OnHeightChanged(string value)
        {
            if (float.TryParse(value, out float height))
            {
                characterSheet.HeightCm = (int)height;
            }
        }

        private void OnWeightChanged(string value)
        {
            if (float.TryParse(value, out float weight))
            {
                characterSheet.WeightKg = (int)weight;
            }
        }

        private void OnDemeanorChanged(string value)
        {
            characterSheet.Demeanor = value;
        }

        private void OnAppearanceChanged(string value)
        {
            characterSheet.PhysicalAppearance = value;
        }

        private void OnBackgroundChanged(string value)
        {
            characterSheet.Background = value;
        }

        private bool OnSaveClicked()
        {
            onSave?.Invoke();
            TryClose();
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
} 