using HarmonyLib;
using UnityEngine;
using System.Reflection;
using PeterHan.PLib.UI;
using STRINGS;

namespace OnlyTame
{
    // SingleCheckboxSideScreen cannot be used, because for critter drop-off
    // it clashes with the auto-wrangle checkbox, apparently there can be only
    // one checkbox handled by it.
    public class OnlyTameSideScreen : SideScreenContent
    {
        private GameObject checkbox;

        private OnlyTameFilter target;

        protected override void OnPrefabInit()
        {
            var margin = new RectOffset(8, 8, 4, 4);
            var baseLayout = gameObject.GetComponent<BoxLayoutGroup>();
            if (baseLayout != null)
                baseLayout.Params = new BoxLayoutParams()
                {
                    Alignment = TextAnchor.MiddleLeft,
                    Margin = margin,
                };
            PPanel panel = new PPanel("MainPanel")
            {
                Direction = PanelDirection.Horizontal,
                Alignment = TextAnchor.MiddleLeft,
                Margin = margin,
                Spacing = 4,
                FlexSize = Vector2.right
            };
            // PLib doesn't seem to have a color matching what is needed.
            Color checkboxColor = new Color32( 0x4f, 0x4f, 0x4f, 255 );
            ColorStyleSetting checkColors = PUITuning.Colors.ComponentLightStyle;
            checkColors.activeColor = checkboxColor;
            checkColors.inactiveColor = checkboxColor;
            checkColors.hoverColor = checkboxColor;
            PCheckBox checkboxField = new PCheckBox( "checkbox" )
            {
                    Text = STRINGS.ONLYTAME.CHECKBOX,
                    ToolTip = STRINGS.ONLYTAME.CHECKBOX_TOOLTIP,
                    OnChecked = OnCheck,
                    TextStyle = PUITuning.Fonts.TextDarkStyle,
                    CheckColor = checkColors,
                    CheckSize = new Vector2f( 26, 26 ),
            };
            checkboxField.AddOnRealize((obj) => checkbox = obj);
            panel.AddChild( checkboxField );
            panel.AddTo( gameObject );
            ContentContainer = gameObject;
            base.OnPrefabInit();
            UpdateState();
        }

        public override bool IsValidForTarget(GameObject target)
        {
            return target.GetComponent<OnlyTameFilter>() != null;
        }

        public override void SetTarget(GameObject new_target)
        {
            if (new_target == null)
            {
                Debug.LogError("Invalid gameObject received");
                return;
            }
            target = new_target.GetComponent<OnlyTameFilter>();
            if (target == null)
            {
                Debug.LogError("The gameObject received does not contain a ThresholdsBase component");
                return;
            }
            UpdateState();
        }

        public void UpdateState()
        {
            if( target == null || checkbox == null )
                return;
            PCheckBox.SetCheckState( checkbox, target.OnlyTameEnabled
                ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED );
        }

        public void OnCheck( GameObject source, int state )
        {
            int newState = state == PCheckBox.STATE_CHECKED ? PCheckBox.STATE_UNCHECKED : PCheckBox.STATE_CHECKED;
            PCheckBox.SetCheckState( checkbox, newState );
            KFMOD.PlayUISound(WidgetSoundPlayer.getSoundPath(ToggleSoundPlayer.default_values[state]));
            target.OnlyTameEnabled = ( newState == PCheckBox.STATE_CHECKED );
        }

        public override string GetTitle()
        {
            return "";
        }

        public override int GetSideScreenSortOrder()
        {
            return -1; // Put the checkbox lower.
        }
    }

    [HarmonyPatch(typeof(DetailsScreen))]
    [HarmonyPatch("OnPrefabInit")]
    public static class DetailsScreen_OnPrefabInit_Patch
    {
        public static void Postfix()
        {
            PUIUtils.AddSideScreenContent<OnlyTameSideScreen>();
        }
    }
}
