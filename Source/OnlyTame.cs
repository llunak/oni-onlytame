using HarmonyLib;
using KSerialization;
using UnityEngine;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace OnlyTame
{
    public class OnlyTameFilter : KMonoBehaviour, ICheckboxControl
    {
        public static readonly Tag WildEgg = TagManager.Create("OnlyTame:WildEgg");
        public static readonly Tag WildEggChecked = TagManager.Create("OnlyTame:WildEggChecked");

        private static MethodInfo fabricatorCancelFetches = AccessTools.Method( typeof( ComplexFabricator ), "CancelFetches" );
        private static MethodInfo deliveryOnFilterChanged = AccessTools.Method( typeof( CreatureDeliveryPoint ), "OnFilterChanged" );

        [Serialize]
        private bool onlyTameEnabled = false;

        public bool OnlyTameEnabled
        {
            get
            {
                return onlyTameEnabled;
            }
            set
            {
                if( onlyTameEnabled == value )
                    return;
                onlyTameEnabled = value;
                // Need to force the building to possibly discard now-invalid fetches,
                // cancelling all fetches is the best I can reasonably come up.
                ComplexFabricator fabricator = GetComponent< ComplexFabricator >();
                if( fabricator != null )
                {
                    fabricatorCancelFetches.Invoke( fabricator, null );
                    fabricator.SetQueueDirty();
                }
                CreatureDeliveryPoint delivery = GetComponent< CreatureDeliveryPoint >();
                if( delivery != null )
                    deliveryOnFilterChanged.Invoke( delivery, new Type[] { null } );
            }
        }

        private static readonly EventSystem.IntraObjectHandler<OnlyTameFilter> OnCopySettingsDelegate
            = new EventSystem.IntraObjectHandler<OnlyTameFilter>(delegate(OnlyTameFilter component, object data)
        {
            component.OnCopySettings(data);
        });

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe((int)GameHashes.CopySettings, OnCopySettingsDelegate);
        }

        public void OnCopySettings( object data )
        {
            GameObject gameObject = (GameObject)data;
            if( gameObject == null )
                return;
            OnlyTameFilter component = gameObject.GetComponent< OnlyTameFilter >();
            if( component == null )
                return;
            OnlyTameEnabled = component.OnlyTameEnabled;
        }

        private static Tag[] wildForbiddenTags = new Tag[ 2 ] { GameTags.Creatures.Wild, WildEgg };
        private static Tag[] wildMutatedSeedForbiddenTags = new Tag[ 3 ] { GameTags.Creatures.Wild, WildEgg, GameTags.MutatedSeed };

        public Tag[] ForbiddenTags( Tag[] fabricatorForbiddenTags )
        {
            if( !onlyTameEnabled )
                return fabricatorForbiddenTags;
            if( fabricatorForbiddenTags == null )
                return wildForbiddenTags;
            // Currently ComplexFabricator possibly forbids only mutated seeds, so optimize for that case.
            if( fabricatorForbiddenTags.Length == 1 && fabricatorForbiddenTags[ 0 ] == GameTags.MutatedSeed )
                return wildMutatedSeedForbiddenTags;
            return fabricatorForbiddenTags.Append( wildForbiddenTags );
        }

        // ICheckboxControl
        public string CheckboxTitleKey => "";
        public string CheckboxLabel => STRINGS.ONLYTAME.TEXT;
        public string CheckboxTooltip => STRINGS.ONLYTAME.TOOLTIP;

        public bool GetCheckboxValue() => OnlyTameEnabled;
        public void SetCheckboxValue(bool value)
        {
            OnlyTameEnabled = value;
        }
    }
}