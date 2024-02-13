using HarmonyLib;
using KSerialization;
using UnityEngine;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace OnlyTame
{
    // Add the wild egg tag to forbidden tags if enabled. ComplexFabricator otherwise takes
    // care of everything.
    [HarmonyPatch(typeof(ComplexFabricator))]
    public class ComplexFabricator_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ForbiddenTags), MethodType.Getter)]
        public static void ForbiddenTags(ref Tag[] __result, ComplexFabricator __instance)
        {
            OnlyTameFilter onlyTameFilter = __instance.GetComponent< OnlyTameFilter >();
            if( onlyTameFilter == null )
                return;
            __result = onlyTameFilter.ForbiddenTags( __result );
        }
    }

    // Need to optionally pass the wild egg as forbidden tag when creating fetches.
    [HarmonyPatch(typeof(CreatureDeliveryPoint))]
    public class CreatureDeliveryPoint_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(RebalanceFetches))]
        public static IEnumerable<CodeInstruction> RebalanceFetches(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
                // Debug.Log("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // The function has code:
                // FetchOrder2 fetchOrder = new FetchOrder2(creatureFetch, tags, FetchChore.MatchCriteria.MatchID,
                //     GameTags.Creatures.Deliverable, RebalanceFetches_Hook( this ), component, ... );
                // Change to:
                // FetchOrder2 fetchOrder = new FetchOrder2(creatureFetch, tags, FetchChore.MatchCriteria.MatchID,
                //     GameTags.Creatures.Deliverable, null, component, ... );
                if( codes[ i ].opcode == OpCodes.Ldloc_1
                    && i + 5 < codes.Count
                    && codes[ i + 1 ].opcode == OpCodes.Ldloc_0
                    && codes[ i + 2 ].opcode == OpCodes.Ldc_I4_0
                    && codes[ i + 3 ].opcode == OpCodes.Ldsfld
                    && codes[ i + 3 ].operand.ToString().Contains( "Tag Deliverable" )
                    && codes[ i + 4 ].opcode == OpCodes.Ldnull // the null to replace
                    && codes[ i + 5 ].opcode == OpCodes.Ldloc_2 )
                {
                    codes[ i + 4 ] = new CodeInstruction( OpCodes.Ldarg_0 ); // replace null with load of 'this'
                    codes.Insert( i + 5, new CodeInstruction( OpCodes.Call,
                        typeof( CreatureDeliveryPoint_Patch ).GetMethod( nameof( RebalanceFetches_Hook ))));
                    found = true;
                    break;
                }
            }
            if(!found)
                Debug.LogWarning("OnlyTame: Failed to patch CreatureDeliveryPoint.RebalanceFetches()");
            return codes;
        }

        public static Tag[] RebalanceFetches_Hook( CreatureDeliveryPoint delivery )
        {
            OnlyTameFilter onlyTameFilter = delivery.GetComponent< OnlyTameFilter >();
            if( onlyTameFilter == null )
                return null;
            return onlyTameFilter.ForbiddenTags( null );
        }
    }

    // Eggs normally do not have any tag for wild the way critters do. Filtering on the actual
    // wildness value seems difficult, so instead tag all eggs as wild when creating them
    // and remove it for non-wild ones.
    [HarmonyPatch(typeof(EggConfig))]
    public class EggConfig_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CreateEgg))]
        public static void CreateEgg(GameObject __result)
        {
            __result.AddTag( OnlyTameFilter.WildEgg );
        }
    }

    // Called when an egg is laid, remove the wild tag if not wild.
    [HarmonyPatch(typeof(FertilityMonitor.Instance))]
    public class FertilityMonitor_Instance_Patch
    {
        private static FieldInfo eggField = AccessTools.Field( typeof(FertilityMonitor.Instance), "egg" );

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ShowEgg))]
        public static void ShowEgg(FertilityMonitor.Instance __instance)
        {
            GameObject egg = (GameObject)eggField.GetValue( __instance );
            if( egg == null )
                return;
            egg.AddTag( OnlyTameFilter.WildEggChecked );
            if( Db.Get().Amounts.Wildness.Lookup(__instance.gameObject).value == 0 )
                egg.RemoveTag( OnlyTameFilter.WildEgg );
        }
    }

    // This is for loading a game saved without the mod, as CreateEgg() is too late
    // for already existing eggs. To avoid repeatedly checking eggs, tag already
    // checked eggs with WildEggChecked tag. Possibly all this tag setting code
    // is needlessly complex, but it's the end result of me trying to figure out
    // a way that works.
    [HarmonyPatch(typeof(IncubationMonitor.Instance))]
    public class IncubationMonitor_Instance_Patch
    {
        public static List<GameObject> eggsToCheck = null;

        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(IStateMachineTarget), typeof(IncubationMonitor.Def) })]
        public static void ctor(IncubationMonitor.Instance __instance)
        {
            GameObject egg = __instance.gameObject;
            if( egg == null || !egg.HasTag(GameTags.Egg))
                return;
            if( egg.HasTag( OnlyTameFilter.WildEggChecked ))
                return;
            egg.AddTag( OnlyTameFilter.WildEggChecked );
            // Db.Get().Amounts.Wildness always claims the egg is wild at this point,
            // so save it for a check later when a valid value is available.
            if( eggsToCheck == null )
                eggsToCheck = new List<GameObject>();
            eggsToCheck.Add( egg );
        }
    }

    // This checks existing not-yet-checked eggs for whether they should get the wild tag.
    // It's called late enough for Db.Get().Amounts.Wildness to provide a valid value.
    [HarmonyPatch(typeof(IncubationMonitor))]
    public class IncubationMonitor_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(IsReadyToHatch))]
        public static void IsReadyToHatch()
        {
            if( IncubationMonitor_Instance_Patch.eggsToCheck == null )
                return;
            foreach( GameObject egg in IncubationMonitor_Instance_Patch.eggsToCheck )
            {
                if( Db.Get().Amounts.Wildness.Lookup( egg ).value > 0 )
                    egg.AddTag( OnlyTameFilter.WildEgg );
                else
                    egg.RemoveTag( OnlyTameFilter.WildEgg );
            }
            IncubationMonitor_Instance_Patch.eggsToCheck = null;
        }
    }

    [HarmonyPatch(typeof(EggCrackerConfig))]
    public class EggCrackerConfig_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(DoPostConfigureComplete))]
        public static void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<OnlyTameFilter>();
        }
    }

    [HarmonyPatch(typeof(CreatureDeliveryPointConfig))]
    public class CreatureDeliveryPointConfig_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(DoPostConfigureComplete))]
        public static void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<OnlyTameFilter>();
        }
    }

    // SingleCheckboxSideScreen does not allow overriding GetSideScreenSortOrder(),
    // so it uses the default 0 order, which puts the checkbox too high.
    [HarmonyPatch(typeof(SideScreenContent))]
    public class SideScreenContent_Patch
    {
        private static FieldInfo targetField = AccessTools.Field( typeof(SingleCheckboxSideScreen), "target" );

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GetSideScreenSortOrder))]
        public static bool GetSideScreenSortOrder(ref int __result, SideScreenContent __instance)
        {
            SingleCheckboxSideScreen checkSideScreen = __instance as SingleCheckboxSideScreen;
            if( checkSideScreen == null )
                return true;
            ICheckboxControl checkboxControl = (ICheckboxControl)targetField.GetValue( checkSideScreen );
            OnlyTameFilter onlyTame = checkboxControl as OnlyTameFilter;
            if( onlyTame == null )
                return true;
            __result = -1;
            return false; // skip original
        }
    }
}
