using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace BasicConfig;

public sealed class BasicConfigClientController
{
    private readonly BasicConfigClientOptions _options;
    private readonly Dictionary<string, string> _draft = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _loadedDraft = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _reviewedKeys = new(StringComparer.OrdinalIgnoreCase);
    private string _statusMessage;
    private string _selectedGroup;
    private ConfirmingBasicConfigDialog _dialog;
    private GuiDialogConfirm _unsavedCloseConfirm;

    public BasicConfigClientController(BasicConfigClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void OnOpenMessage(BasicConfigOpenMessage message)
    {
        if (!IsForThisConfig(message?.ConfigId))
        {
            return;
        }

        UpdateDraft(message.Values, message.ReviewedKeys, message.StatusMessage);
        OpenDialog();
    }

    public void OnResultMessage(BasicConfigResultMessage message)
    {
        if (!IsForThisConfig(message?.ConfigId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(message.Message))
        {
            _options.Api.ShowChatMessage(message.Message);
        }

        UpdateDraft(message.Values, message.ReviewedKeys, message.Message);
        OpenDialog();
    }

    private bool IsForThisConfig(string configId)
    {
        return string.Equals(configId, _options.ConfigId, StringComparison.Ordinal);
    }

    private void UpdateDraft(IEnumerable<BasicConfigSettingValue> values, IEnumerable<string> reviewedKeys, string statusMessage)
    {
        _draft.Clear();
        foreach (var setting in _options.Settings)
        {
            _draft[setting.Key] = string.Empty;
        }

        if (values != null)
        {
            foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value?.Key)))
            {
                _draft[value.Key] = value.Value ?? string.Empty;
            }
        }

        _reviewedKeys = new HashSet<string>(reviewedKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _statusMessage = statusMessage;
        _loadedDraft = new Dictionary<string, string>(_draft, StringComparer.OrdinalIgnoreCase);
    }

    private void OpenDialog()
    {
        _dialog?.TryCloseWithoutPrompt();
        _dialog = new ConfirmingBasicConfigDialog(BuildDialogSettings(), _options.Api, false, HasUnsavedChanges, ConfirmDiscard, OnDialogClosed);
        _dialog.TryOpen(withFocus: false);
    }

    public void CloseWithoutPrompt()
    {
        _dialog?.TryCloseWithoutPrompt();
    }

    public void Reopen()
    {
        OpenDialog();
    }

    private void OnDialogClosed()
    {
        _dialog = null;
        _unsavedCloseConfirm?.TryClose();
        _unsavedCloseConfirm = null;
    }

    private bool HasUnsavedChanges()
    {
        foreach (var key in _options.Settings.Select(setting => setting.Key))
        {
            _draft.TryGetValue(key, out var draftValue);
            _loadedDraft.TryGetValue(key, out var loadedValue);
            if (!string.Equals(draftValue ?? string.Empty, loadedValue ?? string.Empty, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ConfirmDiscard(Action onDiscard)
    {
        if (_unsavedCloseConfirm?.IsOpened() == true)
        {
            return;
        }

        _unsavedCloseConfirm = new GuiDialogConfirm(_options.Api, $"Discard unsaved {_options.DisplayName} config changes?", ok =>
        {
            if (ok)
            {
                onDiscard?.Invoke();
            }
        });
        _unsavedCloseConfirm.TryOpen();
    }

    private JsonDialogSettings BuildDialogSettings()
    {
        var rows = new List<DialogRow>
        {
            new(new DialogElement
            {
                Code = "title",
                Type = EnumDialogElementType.Text,
                Text = _options.Title ?? $"{_options.DisplayName} Config",
                Width = 720,
                Height = 32,
                FontSize = 18
            })
        };

        AddStatusRow(rows);
        AddGroupSelectorRow(rows);
        AddShortcutRow(rows);
        AddSettingRows(rows);
        AddActionRows(rows);

        return new JsonDialogSettings
        {
            Code = _options.DialogCode ?? _options.ConfigId + "-basicconfig",
            Alignment = EnumDialogArea.CenterMiddle,
            Rows = rows.ToArray(),
            SizeMultiplier = 0.9,
            Padding = 16,
            DisableWorldInteract = true,
            OnGet = GetValue,
            OnSet = SetValue
        };
    }

    private void AddStatusRow(List<DialogRow> rows)
    {
        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            rows.Add(new DialogRow(new DialogElement
            {
                Code = "status",
                Type = EnumDialogElementType.Text,
                Text = _statusMessage,
                Width = 720,
                Height = 42,
                FontSize = 13
            }));
        }
    }

    private void AddGroupSelectorRow(List<DialogRow> rows)
    {
        var groups = GetGroups();
        _selectedGroup = NormalizeGroup(_selectedGroup, groups);
        rows.Add(new DialogRow(new DialogElement
        {
            Code = "group-select",
            Label = "Group",
            Tooltip = "Choose a setting group.",
            Type = EnumDialogElementType.Select,
            Mode = EnumDialogElementMode.DropDown,
            Values = groups.ToArray(),
            Names = groups.ToArray(),
            Width = 720,
            Height = 28
        })
        {
            TopPadding = 8,
            BottomPadding = 6
        });
    }

    private void AddShortcutRow(List<DialogRow> rows)
    {
        var shortcuts = _options.Shortcuts ?? Array.Empty<BasicConfigClientShortcut>();
        if (shortcuts.Count == 0)
        {
            return;
        }

        rows.Add(new DialogRow(shortcuts.Select(shortcut => CreateButton(shortcut.Code, shortcut.Label, shortcut.Tooltip)).ToArray())
        {
            TopPadding = 4,
            BottomPadding = 8
        });
    }

    private void AddSettingRows(List<DialogRow> rows)
    {
        foreach (var group in _options.Settings.Where(setting => string.Equals(setting.Group, _selectedGroup, StringComparison.OrdinalIgnoreCase)).GroupBy(setting => setting.Group))
        {
            rows.Add(new DialogRow(new DialogElement
            {
                Code = "group-" + group.Key,
                Type = EnumDialogElementType.Text,
                Text = group.Key,
                Width = 720,
                Height = 26,
                FontSize = 15
            })
            {
                TopPadding = 8,
                BottomPadding = 2
            });

            foreach (var setting in group.OrderBy(setting => _reviewedKeys.Contains(setting.Key) ? 1 : 0).ThenBy(setting => setting.Label))
            {
                rows.Add(new DialogRow(CreateSettingElement(setting))
                {
                    BottomPadding = 4
                });
            }
        }
    }

    private void AddActionRows(List<DialogRow> rows)
    {
        rows.Add(new DialogRow(
            CreateButton("save", "Save", "Save config changes."),
            CreateButton("mark-reviewed", "Mark Reviewed", "Mark all current settings as reviewed."),
            CreateButton("reload", "Reload", "Reload config from disk."))
        {
            TopPadding = 12
        });

        rows.Add(new DialogRow(CreateButton("close", "Close", "Close this dialog."))
        {
            TopPadding = 4
        });
    }

    private List<string> GetGroups()
    {
        return _options.Settings.Select(setting => setting.Group).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeGroup(string group, IReadOnlyList<string> groups)
    {
        if (groups.Count == 0)
        {
            return string.Empty;
        }

        return groups.FirstOrDefault(candidate => string.Equals(candidate, group, StringComparison.OrdinalIgnoreCase)) ?? groups[0];
    }

    private DialogElement CreateSettingElement(IBasicConfigSettingDefinition setting)
    {
        var label = _reviewedKeys.Contains(setting.Key) ? setting.Label : "NEW: " + setting.Label;
        var reloadNote = setting.ReloadBehavior == BasicConfigReloadBehavior.Live ? "Live-applied." : "Requires restart.";
        var element = new DialogElement
        {
            Code = setting.Key,
            Label = label,
            Tooltip = $"{setting.Description} {reloadNote}",
            Width = 720,
            Height = 28
        };

        switch (setting.Kind)
        {
            case BasicConfigSettingKind.Boolean:
                element.Type = EnumDialogElementType.Switch;
                break;
            case BasicConfigSettingKind.Integer:
            case BasicConfigSettingKind.Decimal:
                element.Type = EnumDialogElementType.NumberInput;
                break;
            case BasicConfigSettingKind.Select:
                element.Type = EnumDialogElementType.Select;
                element.Mode = EnumDialogElementMode.DropDown;
                element.Values = setting.Options.ToArray();
                element.Names = setting.OptionNames.ToArray();
                break;
            default:
                element.Type = EnumDialogElementType.Input;
                break;
        }

        return element;
    }

    private static DialogElement CreateButton(string code, string text, string tooltip)
    {
        return new DialogElement
        {
            Code = code,
            Type = EnumDialogElementType.Button,
            Text = text,
            Tooltip = tooltip,
            Width = 150,
            Height = 34,
            FontSize = 13
        };
    }

    private string GetValue(string code)
    {
        if (code == "group-select")
        {
            return _selectedGroup ?? string.Empty;
        }

        return _draft.TryGetValue(code, out var value) ? value : string.Empty;
    }

    private void SetValue(string code, string value)
    {
        if (TryHandleAction(code))
        {
            return;
        }

        if (code == "group-select")
        {
            _selectedGroup = NormalizeGroup(value, GetGroups());
            OpenDialog();
            return;
        }

        if (_options.Settings.Any(setting => string.Equals(setting.Key, code, StringComparison.OrdinalIgnoreCase)))
        {
            _draft[code] = value ?? string.Empty;
        }
    }

    private bool TryHandleAction(string code)
    {
        switch (code)
        {
            case "save":
                SendSave();
                return true;
            case "mark-reviewed":
                SendMarkReviewed();
                return true;
            case "reload":
                ReloadOrConfirmDiscard();
                return true;
            case "close":
                _dialog?.TryClose();
                return true;
            default:
                var shortcut = _options.Shortcuts?.FirstOrDefault(candidate => string.Equals(candidate.Code, code, StringComparison.Ordinal));
                if (shortcut != null)
                {
                    shortcut.OnClick?.Invoke();
                    return true;
                }

                return false;
        }
    }

    private void ReloadOrConfirmDiscard()
    {
        if (HasUnsavedChanges())
        {
            ConfirmDiscard(SendReload);
            return;
        }

        SendReload();
    }

    private void SendSave()
    {
        _options.SendPacket(new BasicConfigSaveMessage
        {
            ConfigId = _options.ConfigId,
            Values = _draft.Select(kvp => new BasicConfigSettingValue { Key = kvp.Key, Value = kvp.Value }).ToList()
        });
    }

    private void SendMarkReviewed()
    {
        _options.SendPacket(new BasicConfigSaveMessage
        {
            ConfigId = _options.ConfigId,
            MarkReviewedKeys = _options.Settings.Select(setting => setting.Key).ToList()
        });
    }

    private void SendReload()
    {
        _options.SendPacket(new BasicConfigSaveMessage
        {
            ConfigId = _options.ConfigId,
            ReloadFromDisk = true
        });
    }

    private sealed class ConfirmingBasicConfigDialog : GuiJsonDialog
    {
        private readonly Func<bool> _hasUnsavedChanges;
        private readonly Action<Action> _confirmDiscard;
        private readonly Action _onClosed;
        private bool _forceClose;

        public ConfirmingBasicConfigDialog(JsonDialogSettings settings, ICoreClientAPI capi, bool focusFirstElement, Func<bool> hasUnsavedChanges, Action<Action> confirmDiscard, Action onClosed)
            : base(settings, capi, focusFirstElement)
        {
            _hasUnsavedChanges = hasUnsavedChanges;
            _confirmDiscard = confirmDiscard;
            _onClosed = onClosed;
        }

        public override bool TryClose()
        {
            if (!_forceClose && _hasUnsavedChanges?.Invoke() == true)
            {
                _confirmDiscard?.Invoke(TryCloseWithoutPrompt);
                return false;
            }

            var closed = base.TryClose();
            if (closed)
            {
                _onClosed?.Invoke();
            }

            return closed;
        }

        public void TryCloseWithoutPrompt()
        {
            _forceClose = true;
            try
            {
                var closed = base.TryClose();
                if (closed)
                {
                    _onClosed?.Invoke();
                }
            }
            finally
            {
                _forceClose = false;
            }
        }
    }
}

public sealed class BasicConfigClientOptions
{
    public string ConfigId { get; set; }
    public string DisplayName { get; set; }
    public string Title { get; set; }
    public string DialogCode { get; set; }
    public ICoreClientAPI Api { get; set; }
    public IReadOnlyList<IBasicConfigSettingDefinition> Settings { get; set; }
    public Action<object> SendPacket { get; set; }
    public IReadOnlyList<BasicConfigClientShortcut> Shortcuts { get; set; } = Array.Empty<BasicConfigClientShortcut>();
}

public sealed class BasicConfigClientShortcut
{
    public string Code { get; set; }
    public string Label { get; set; }
    public string Tooltip { get; set; }
    public Action OnClick { get; set; }
}
