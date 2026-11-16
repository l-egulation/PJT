using Deviant.Combat;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        LocalInput guiInput;
        LocalInputUIBinding inputUI;
        LocalInput Controls
        {
            get
            {
                if (host && host.Arena && host.Arena.Player) guiInput = PlayerControls.For(host.Arena.Player);
                else if (village && village.Ready) guiInput = PlayerControls.For(village.World.arena.Player);
                else if (oldTitle && !guiInput) guiInput = GetComponent<LocalInput>() ?? gameObject.AddComponent<LocalInput>();
                return guiInput;
            }
        }
        void SyncGuiInput()
        {
            RefreshAbandonRunQuestion();
            var input = Controls;
            if (!input || !EventSystem.current) return;
            if (!inputUI) inputUI = gameObject.AddComponent<LocalInputUIBinding>();
            inputUI.FixedMenuKeys=!oldTitle||approvedSettings&&approvedSettings.activeInHierarchy||shortcutPanelOpen;
            inputUI.Bind(input, EventSystem.current);
            input.Poll();
            if(GuardLoadingUi())return;
            inputUI.KeyboardScrollOverride=village&&hubOpen==workbench&&benchDetails?benchDetails.parent.GetComponent<UnityEngine.UI.ScrollRect>():null;
            RefreshPromptLabels();
            RefreshTutorialKeys();
            // Rebinding owns the opening key and keeps ownership until the newly assigned key is released.
            if(shortcutCapture)
            {
                inputUI.SetPanel(null);EventSystem.current.sendNavigationEvents=false;
                input.SuppressGameplay();return;
            }
            if(shortcutResetQuestion)
            {
                TickShortcutResetQuestion();
                inputUI.SetPanel(shortcutResetQuestion ? shortcutResetQuestion : shortcutPanel);
                EventSystem.current.sendNavigationEvents=false;
                return;
            }
            if(abandonRunQuestion){inputUI.SetPanel(abandonRunQuestion);return;}
            if(TickApprovedSettings()){inputUI.SetPanel(displayQuestion?displayQuestion:settingsDraftQuestion?settingsDraftQuestion:approvedSettings);EventSystem.current.sendNavigationEvents=false;return;}
            if(TickApprovedCoreInput())
            {
                // The rename field needs UpdateSelected events for typing, while
                // our core handler still owns navigation and confirmation.
                inputUI.SetPanel(liveRenamePanel ? liveRenamePanel.gameObject : null);
                EventSystem.current.sendNavigationEvents=false;
                return;
            }
            var panel = shortcutPanelOpen&&!shortcutsInSettings?shortcutPanel:approvedSettings&&approvedSettings.activeInHierarchy?approvedSettings:oldTitle ? (menu && menu.activeInHierarchy ? menu : wellTitle ? wellTitle.InputPanel : null) : focusedPanel;
            // The map owns pan/zoom and legend inputs; generic menu scrolling must not consume the pan stick.
            if(map&&map.IsOpen&&panel==mapPanel){inputUI.SetPanel(null);return;}
            inputUI.SetPanel(panel && panel.activeInHierarchy && !(village && hubOpen == record) ? panel : null);
            if (village && (VillageInkModalShowing || storyActive || notebookOpen || ghostLesson != null || LessonShowing != null || bubbleAdvance)) input.SuppressGameplay();
        }
        bool TakeBack()
        {
            var input = Controls;
            if (!input || settingsInputFrame==Time.frameCount || liveInputFrame==Time.frameCount || abandonRunClosedFrame==Time.frameCount || shortcutResetClosedFrame==Time.frameCount) return false;
            bool pause = input.Take(GameAction.Pause);
            bool cancelAllowed = focusedPanel && focusedPanel.activeInHierarchy || village && (hubOpen || hubPaused || storyActive);
            bool cancel = cancelAllowed && (input.Take(GameAction.Cancel) || !oldTitle && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame);
            if (pause) input.Take(GameAction.Cancel); // Escape belongs to both bindings.
            if (!pause && !cancel) return false;
            input.SuppressGameplay(); return true;
        }
        bool TakeAdvance()
        {
            var input = Controls;
            if (!input || !input.Take(GameAction.Advance)) return false;
            input.SuppressGameplay(); return true;
        }
        string KeyLabel(GameAction action) => Controls ? Controls.Label(action) : action.ToString();
        string Prompt(string template) => Controls ? Controls.FormatPrompt(template) : template;
        readonly System.Collections.Generic.List<(UnityEngine.UI.Text view, string template)> promptLabels = new System.Collections.Generic.List<(UnityEngine.UI.Text, string)>();
        UnityEngine.UI.Text BindPrompt(UnityEngine.UI.Text view, string template)
        {
            promptLabels.Add((view, template)); view.text = Prompt(template); FitApprovedKeycap(view); return view;
        }
        readonly System.Collections.Generic.List<(UnityEngine.UI.Text view, string keyboard, GameAction action)> menuKeyLabels = new System.Collections.Generic.List<(UnityEngine.UI.Text, string, GameAction)>();
        void BindMenuKey(UnityEngine.UI.Text view, string keyboard, GameAction action)
        { menuKeyLabels.Add((view,keyboard,action)); view.text=MenuKey(keyboard,action);FitApprovedKeycap(view); }
        void RefreshPromptLabels()
        {
            promptLabels.RemoveAll(entry => !entry.view);
            menuKeyLabels.RemoveAll(entry => !entry.view);
            foreach (var entry in menuKeyLabels)
                if(entry.view.gameObject.activeInHierarchy) {string text=MenuKey(entry.keyboard,entry.action);if(entry.view.text!=text){entry.view.text=text;FitApprovedKeycap(entry.view);}}
            foreach (var entry in promptLabels)
                if (entry.view && entry.view.gameObject.activeInHierarchy)
                { string text = Prompt(entry.template); if (entry.view.text != text) {entry.view.text = text;FitApprovedKeycap(entry.view);} }
            RefreshMapFooterPrompts();
        }
        string[] lessonDisplayKeys, bannerDisplayKeys, ghostDisplayKeys;
        string lessonKeySignature, bannerKeySignature, ghostKeySignature;
        string KeySignature(string[] keys) => keys == null ? "" : string.Join("|", System.Array.ConvertAll(keys, LessonKey));
        void RefreshTutorialKeys()
        {
            if (lessonCard && lessonCard.gameObject.activeSelf && lessonKeySignature != KeySignature(lessonDisplayKeys)) LayoutLessonKeys();
            if (banner && banner.gameObject.activeSelf && bannerKeySignature != KeySignature(bannerDisplayKeys)) LayoutBannerKeys();
            if (ghostLesson != null && ghostKeySignature != KeySignature(ghostDisplayKeys)) LayoutGhostKeys();
        }
        string LessonKey(string key)
        {
            // Authored tutorial key identifiers resolve through the current action profile.
            if (!Controls) return key;
            if (System.Enum.TryParse<GameAction>(key, out var action)) return KeyLabel(action);
            // Older recorded lessons keep their authored key IDs; resolve those through actions too.
            switch (key)
            {
                case "Space": return KeyLabel(GameAction.Jump);
                case "Shift": return KeyLabel(GameAction.Dash);
                case "Q": return KeyLabel(GameAction.Weapon1Basic);
                case "W": return KeyLabel(GameAction.Weapon2Basic);
                case "E": return KeyLabel(GameAction.Weapon1Special);
                case "R": return KeyLabel(GameAction.Weapon2Special);
                case "F": return KeyLabel(GameAction.Interact);
                case "↑": return LessonDirection("up", key);
                case "↓": return LessonDirection("down", key);
                case "←": return LessonDirection("left", key);
                case "→": return LessonDirection("right", key);
                default: return key;
            }
        }
        string LessonDirection(string direction, string arrow)
        {
            // Use the compact arrow glyph for default directions; display live labels for other keys and pads.
            if (!Controls.UsingGamepad)
                foreach (var binding in Controls.Source.GetAction(GameAction.Move).bindings)
                    if (binding.isPartOfComposite && binding.name == direction &&
                        string.Equals(binding.effectivePath, "<Keyboard>/" + direction + "Arrow", System.StringComparison.OrdinalIgnoreCase)) return arrow;
            return Controls.DirectionLabel(direction);
        }
    }
}
